// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ApSolutions.LocalMedia.Tests.Updates;

/// <summary>
/// An update source that really exists: a socket, a TLS handshake, and bytes on the wire.
/// </summary>
/// <remarks>
/// A message handler would have been shorter, but three of the things this suite has to show — a
/// download that dies halfway, a resumed range, a redirect that tries to leave HTTPS — are properties
/// of a connection rather than of a response object. Faking the response would have made the test
/// agree with itself.
/// <para>
/// The certificate is generated in memory and trusted by nothing: the client this server hands out
/// accepts exactly this one certificate, by thumbprint, and no store on the machine is touched.
/// </para>
/// </remarks>
internal sealed class FakeReleaseServer : IDisposable
{
    private readonly TcpListener _listener;
    private readonly X509Certificate2 _certificate;
    private readonly Dictionary<string, Func<FakeRequest, FakeResponse>> _routes = new(StringComparer.Ordinal);
    private readonly List<FakeRequest> _requests = [];
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stopping = new();
    private bool _stopped;

    public FakeReleaseServer()
    {
        _certificate = CreateCertificate();
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        BaseAddress = new Uri(string.Create(
            CultureInfo.InvariantCulture,
            $"https://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/"));
        _ = Task.Run(AcceptAsync);
    }

    public Uri BaseAddress { get; }

    public IReadOnlyList<FakeRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Answers one path. The last registration for a path wins, so a case can rewrite one.</summary>
    public FakeReleaseServer Map(string path, Func<FakeRequest, FakeResponse> handler)
    {
        lock (_gate)
        {
            _routes[path] = handler;
        }

        return this;
    }

    /// <summary>
    /// A client that trusts this server's certificate and nothing else. The callback compares the
    /// thumbprint rather than returning true, so a case cannot accidentally pass against some other
    /// endpoint that happened to answer.
    /// </summary>
    public HttpClient CreateClient(TimeSpan? timeout = null)
    {
        var expected = _certificate.Thumbprint;
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) =>
                certificate is not null
                && string.Equals(certificate.Thumbprint, expected, StringComparison.OrdinalIgnoreCase),
        };
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = BaseAddress,
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
        };
    }

    /// <summary>
    /// Stopping is idempotent: one case shuts the server down on purpose, to produce a source that
    /// cannot be reached, and the enclosing `using` then shuts it down again.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
        }

        _stopping.Cancel();
        _listener.Dispose();
        _stopping.Dispose();
        _certificate.Dispose();
    }

    private static X509Certificate2 CreateCertificate()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddIpAddress(IPAddress.Loopback);
        alternativeNames.AddDnsName("localhost");
        request.CertificateExtensions.Add(alternativeNames.Build());
        using var selfSigned = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        // Windows cannot use the ephemeral key a self-signed request produces for a TLS handshake, so
        // the certificate makes one round trip through PKCS#12 in memory. Nothing is written to disk
        // and nothing is added to a certificate store.
        return X509CertificateLoader.LoadPkcs12(selfSigned.Export(X509ContentType.Pfx), password: null);
    }

    private async Task AcceptAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stopping.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
                when (exception is SocketException or ObjectDisposedException or OperationCanceledException)
            {
                return;
            }

            _ = Task.Run(() => ServeAsync(client));
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                using var tls = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
                await tls.AuthenticateAsServerAsync(_certificate, false, checkCertificateRevocation: false)
                    .ConfigureAwait(false);
                var request = await ReadRequestAsync(tls).ConfigureAwait(false);
                if (request is null)
                {
                    return;
                }

                lock (_gate)
                {
                    _requests.Add(request);
                }

                await WriteResponseAsync(tls, Route(request)).ConfigureAwait(false);
            }
            catch (Exception exception)
                when (exception is IOException or SocketException or ObjectDisposedException or AuthenticationException)
            {
                // A client that walked away mid-response is one of the cases this server exists to
                // produce. It is not a failure of the server.
            }
        }
    }

    private FakeResponse Route(FakeRequest request)
    {
        Func<FakeRequest, FakeResponse>? handler;
        lock (_gate)
        {
            _ = _routes.TryGetValue(request.Path, out handler);
        }

        return handler?.Invoke(request) ?? FakeResponse.Status(HttpStatusCode.NotFound);
    }

    private static async Task<FakeRequest?> ReadRequestAsync(Stream stream)
    {
        var buffer = new byte[8192];
        var received = 0;
        while (received < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(received)).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            received += read;
            var text = Encoding.ASCII.GetString(buffer, 0, received);
            if (text.Contains("\r\n\r\n", StringComparison.Ordinal))
            {
                return Parse(text);
            }
        }

        return null;
    }

    private static FakeRequest Parse(string text)
    {
        var lines = text.Split("\r\n");
        var target = lines[0].Split(' ', 3);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            if (line.Length == 0)
            {
                break;
            }

            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator > 0)
            {
                headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        var path = target.Length > 1 ? target[1] : "/";
        return new FakeRequest(target[0], path.Split('?', 2)[0], headers, ReadRangeStart(headers));
    }

    private static long? ReadRangeStart(Dictionary<string, string> headers)
    {
        if (!headers.TryGetValue("Range", out var value)
            || !value.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var start = value["bytes=".Length..].Split('-', 2)[0];
        return long.TryParse(start, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static async Task WriteResponseAsync(Stream stream, FakeResponse response)
    {
        if (response.DelayMilliseconds > 0)
        {
            await Task.Delay(response.DelayMilliseconds).ConfigureAwait(false);
        }

        var head = new StringBuilder();
        head.Append(CultureInfo.InvariantCulture, $"HTTP/1.1 {(int)response.StatusCode} {response.StatusCode}\r\n");
        head.Append(
            CultureInfo.InvariantCulture,
            $"Content-Length: {response.DeclaredLength ?? response.Body.Length}\r\n");
        foreach (var (name, value) in response.Headers)
        {
            head.Append(CultureInfo.InvariantCulture, $"{name}: {value}\r\n");
        }

        head.Append("Connection: close\r\n\r\n");
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head.ToString())).ConfigureAwait(false);

        // A body that arrives in two goes with a pause between them is what a slow connection looks
        // like from the client's side, and it is the only way to find out whether a timeout meant
        // for the headers is quietly also a limit on how long a download may take.
        if (response.SplitDelayMilliseconds > 0)
        {
            var half = response.Body.Length / 2;
            await stream.WriteAsync(response.Body.AsMemory(0, half)).ConfigureAwait(false);
            await stream.FlushAsync().ConfigureAwait(false);
            await Task.Delay(response.SplitDelayMilliseconds).ConfigureAwait(false);
            await stream.WriteAsync(response.Body.AsMemory(half)).ConfigureAwait(false);
        }
        else
        {
            await stream.WriteAsync(response.Body).ConfigureAwait(false);
        }

        await stream.FlushAsync().ConfigureAwait(false);
    }
}

/// <summary>One request as it arrived, with the range header already read.</summary>
internal sealed record FakeRequest(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Headers,
    long? RangeStart);

/// <summary>
/// One answer. <see cref="DeclaredLength"/> exists so a response can promise more than it sends,
/// which is what an interrupted download looks like from the client's side.
/// </summary>
internal sealed record FakeResponse(
    HttpStatusCode StatusCode,
    byte[] Body,
    IReadOnlyList<KeyValuePair<string, string>> Headers,
    long? DeclaredLength = null)
{
    /// <summary>How long the server sits on the request before answering, so a client can time out.</summary>
    public int DelayMilliseconds { get; init; }

    /// <summary>How long the body pauses halfway, so a client can time out mid-download.</summary>
    public int SplitDelayMilliseconds { get; init; }

    public static FakeResponse Status(HttpStatusCode statusCode) => new(statusCode, [], []);

    /// <summary>An answer that arrives too late to be of use to anybody.</summary>
    public static FakeResponse TooSlow() => new(HttpStatusCode.OK, [], []) { DelayMilliseconds = 5000 };

    public static FakeResponse Json(string payload) => new(
        HttpStatusCode.OK,
        Encoding.UTF8.GetBytes(payload),
        [new KeyValuePair<string, string>("Content-Type", "application/json")]);

    public static FakeResponse Bytes(byte[] payload) => new(HttpStatusCode.OK, payload, []);

    /// <summary>Answers a resumed request from <paramref name="start"/> onwards.</summary>
    public static FakeResponse Partial(byte[] payload, long start) => new(
        HttpStatusCode.PartialContent,
        payload[(int)start..],
        [new KeyValuePair<string, string>(
            "Content-Range",
            string.Create(CultureInfo.InvariantCulture, $"bytes {start}-{payload.Length - 1}/{payload.Length}"))]);

    /// <summary>Announces the whole length and sends only the first half, then hangs up.</summary>
    public static FakeResponse Truncated(byte[] payload) => new(
        HttpStatusCode.OK,
        payload[..(payload.Length / 2)],
        [],
        payload.Length);

    public static FakeResponse Redirect(string location) => new(
        HttpStatusCode.Found,
        [],
        [new KeyValuePair<string, string>("Location", location)]);
}
