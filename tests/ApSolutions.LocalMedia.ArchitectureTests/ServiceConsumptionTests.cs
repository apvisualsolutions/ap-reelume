// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.RegularExpressions;

namespace ApSolutions.LocalMedia.ArchitectureTests;

/// <summary>
/// The registered→consumed gate. The deep audit of 2026-08-08 found one defect wearing ten faces:
/// components built, registered, and tested that the assembled application never invokes. A
/// registration nothing resolves is a promise the application does not keep, so this suite requires
/// every service registered in <c>CompositionRoot</c> to be resolved at least once outside its own
/// registration — through <c>GetRequiredService</c> in the composition root itself, or by riding as
/// a constructor dependency of a service that is.
/// </summary>
/// <remarks>
/// Consumption is transitive and starts from the resolutions the running application performs: a
/// service resolved only from inside the registration of another orphan is still an orphan. The
/// analysis is textual on purpose — it is the accepted style until WP-6 converts the assembly
/// assertions to <c>IServiceCollection</c> — and it reads three things: the registration chain, the
/// <c>GetRequiredService</c> calls and where they sit, and the constructor parameters of each
/// registered implementation. A <c>new</c> next to a registration is not a resolution: the double
/// ownership ARQ-008 found stays visible to this gate.
/// </remarks>
public sealed class ServiceConsumptionTests
{
    /// <summary>
    /// The services the audit found registered and never fed, each waiting for its own WP-2 wiring
    /// task. An entry here is a debt with a name on it, not an exemption: the moment its wiring
    /// lands, <see cref="The_pending_wiring_list_names_only_services_still_unconsumed"/> forces the
    /// entry out, so this list can only shrink truthfully.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> PendingWiring =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
        };

    [Fact]
    public void Every_registered_service_is_resolved_outside_its_own_registration()
    {
        var graph = CompositionGraph.Load();

        var orphans = graph.RegisteredServices
            .Where(service => !graph.IsConsumed(service))
            .Where(service => !PendingWiring.ContainsKey(service))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            orphans.Length == 0,
            "These services are registered in CompositionRoot and nothing ever resolves them, so "
            + "whatever they promise does not exist in the application: "
            + string.Join(", ", orphans));
    }

    [Fact]
    public void The_pending_wiring_list_names_only_services_still_unconsumed()
    {
        var graph = CompositionGraph.Load();

        var stale = PendingWiring.Keys
            .Where(service => !graph.RegisteredServices.Contains(service) || graph.IsConsumed(service))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            stale.Length == 0,
            "These pending-wiring entries are already consumed or no longer registered; remove them "
            + "so the debt list stays true: "
            + string.Join(", ", stale));
    }

    /// <summary>
    /// The parser's own floor. A refactor that changes the registration idiom enough to blind this
    /// analysis would otherwise turn the gate green by silence.
    /// </summary>
    [Fact]
    public void The_analysis_still_sees_the_composition_it_guards()
    {
        var graph = CompositionGraph.Load();

        Assert.True(
            graph.RegisteredServices.Count > 100,
            $"Only {graph.RegisteredServices.Count} registrations were parsed; the composition root "
            + "registers far more, so the analysis has gone blind rather than the application small.");
        Assert.True(
            graph.RootResolutionCount > 20,
            $"Only {graph.RootResolutionCount} resolutions were found outside registrations; the "
            + "composition root performs far more, so the analysis has gone blind.");
    }

    /// <summary>
    /// What the composition root registers, what it resolves, and what rides in as a constructor
    /// dependency — read from the text the accepted way.
    /// </summary>
    private sealed class CompositionGraph
    {
        private readonly HashSet<string> _consumed;

        private CompositionGraph(
            IReadOnlySet<string> registeredServices,
            HashSet<string> consumed,
            int rootResolutionCount)
        {
            RegisteredServices = registeredServices;
            _consumed = consumed;
            RootResolutionCount = rootResolutionCount;
        }

        public IReadOnlySet<string> RegisteredServices { get; }

        public int RootResolutionCount { get; }

        public bool IsConsumed(string service) => _consumed.Contains(service);

        public static CompositionGraph Load()
        {
            var compositionPath = RepositoryLayout.PathFromRoot(
                "src/ApSolutions.LocalMedia.Windows/CompositionRoot.cs");
            Assert.True(File.Exists(compositionPath), "CompositionRoot.cs was not found where the host keeps it.");
            var text = File.ReadAllText(compositionPath);

            var registrations = ParseRegistrations(text);
            var services = registrations.Select(registration => registration.Service)
                .ToHashSet(StringComparer.Ordinal);

            // Every resolution the composition root performs. One that sits inside a registration's
            // factory only counts once that registration itself is consumed; one outside the chain
            // runs with the application and is a root.
            var edges = services.ToDictionary(
                service => service,
                _ => new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal);
            var consumed = new HashSet<string>(StringComparer.Ordinal);
            var rootResolutions = 0;
            foreach (var resolution in Regex.Matches(
                text,
                @"Get(?:Required)?Service<([A-Za-z0-9_.]+)>",
                RegexOptions.None,
                TimeSpan.FromSeconds(2)).Cast<Match>())
            {
                var resolved = resolution.Groups[1].Value;
                var owner = registrations.FirstOrDefault(registration =>
                    registration.BodyStart <= resolution.Index && resolution.Index < registration.BodyEnd);
                if (owner is null)
                {
                    rootResolutions++;
                    consumed.Add(resolved);
                }
                else if (edges.TryGetValue(owner.Service, out var dependencies))
                {
                    dependencies.Add(resolved);
                }
            }

            // A registered implementation consumes its constructor parameters: that is the half of
            // the composition the container wires without a visible GetRequiredService.
            foreach (var registration in registrations)
            {
                if (registration.Implementation is not { } implementation
                    || FindSourceFile(implementation) is not { } sourcePath)
                {
                    continue;
                }

                var parameters = ConstructorParameterText(File.ReadAllText(sourcePath), implementation);
                if (parameters.Length == 0)
                {
                    continue;
                }

                foreach (var dependency in services.Where(candidate =>
                    candidate != registration.Service
                    && candidate != implementation
                    && Regex.IsMatch(parameters, $@"\b{Regex.Escape(candidate)}\b", RegexOptions.None, TimeSpan.FromSeconds(1))))
                {
                    edges[registration.Service].Add(dependency);
                }
            }

            // The closure: consumption spreads only from services something already reaches.
            var pending = new Queue<string>(consumed);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (!edges.TryGetValue(current, out var dependencies))
                {
                    continue;
                }

                foreach (var dependency in dependencies.Where(consumed.Add))
                {
                    pending.Enqueue(dependency);
                }
            }

            return new CompositionGraph(services, consumed, rootResolutions);
        }

        /// <summary>One registration: the service it promises, the type that fulfils it, and the span of its arguments.</summary>
        private sealed record Registration(string Service, string? Implementation, int BodyStart, int BodyEnd);

        private static List<Registration> ParseRegistrations(string text)
        {
            var registrations = new List<Registration>();
            foreach (var match in Regex.Matches(
                text,
                @"\.Add(?:Singleton|Transient|Scoped)",
                RegexOptions.None,
                TimeSpan.FromSeconds(2)).Cast<Match>())
            {
                var cursor = match.Index + match.Length;
                string[] genericArguments = [];
                if (text[cursor] == '<')
                {
                    var genericEnd = MatchingDelimiter(text, cursor, '<', '>');
                    genericArguments = SplitTopLevel(text[(cursor + 1)..genericEnd]);
                    cursor = genericEnd + 1;
                }

                Assert.Equal('(', text[cursor]);
                var bodyEnd = MatchingDelimiter(text, cursor, '(', ')');
                var body = text[(cursor + 1)..bodyEnd].Trim();

                var (service, implementation) = Describe(text, genericArguments, body);
                registrations.Add(new Registration(service, implementation, cursor + 1, bodyEnd));
            }

            return registrations;
        }

        /// <summary>
        /// Names the service a registration promises. The generic argument is the promise when there
        /// is one; otherwise the factory's <c>new</c>, a static creator, a method group's return
        /// type, or a captured instance's declared type is.
        /// </summary>
        private static (string Service, string? Implementation) Describe(
            string text,
            string[] genericArguments,
            string body)
        {
            var constructed = Regex.Match(body, @"\bnew\s+([A-Z][A-Za-z0-9_]*)", RegexOptions.None, TimeSpan.FromSeconds(1));
            if (genericArguments.Length == 2)
            {
                return (genericArguments[0], genericArguments[1]);
            }

            if (genericArguments.Length == 1)
            {
                return (genericArguments[0], constructed.Success ? constructed.Groups[1].Value : genericArguments[0]);
            }

            if (body.Contains("=>", StringComparison.Ordinal))
            {
                if (constructed.Success)
                {
                    return (constructed.Groups[1].Value, constructed.Groups[1].Value);
                }

                // A factory with no new hands back what a static creator built, e.g. LibVlcFactory.CreateDefault().
                var creator = Regex.Match(
                    body,
                    @"=>\s*([A-Z][A-Za-z0-9_]*)\.",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(1));
                Assert.True(creator.Success, $"A factory registration could not be read: {body}");
                return (creator.Groups[1].Value, creator.Groups[1].Value);
            }

            if (char.IsUpper(body[0]))
            {
                // A method group: the registration promises the method's return type.
                var method = Regex.Match(
                    text,
                    $@"private static (\w+) {Regex.Escape(body)}\(",
                    RegexOptions.None,
                    TimeSpan.FromSeconds(1));
                Assert.True(method.Success, $"The factory method {body} was not found in CompositionRoot.");
                return (method.Groups[1].Value, method.Groups[1].Value);
            }

            // A captured instance: its declared type is what the container hands out.
            var declaration = Regex.Match(
                text,
                $@"var {Regex.Escape(body)} = new (\w+)",
                RegexOptions.None,
                TimeSpan.FromSeconds(1));
            if (declaration.Success)
            {
                return (declaration.Groups[1].Value, declaration.Groups[1].Value);
            }

            var parameter = Regex.Match(
                text,
                $@"([A-Z]\w*) {Regex.Escape(body)}[,)\s]",
                RegexOptions.None,
                TimeSpan.FromSeconds(1));
            Assert.True(parameter.Success, $"The captured instance {body} could not be typed.");
            return (parameter.Groups[1].Value, parameter.Groups[1].Value);
        }

        /// <summary>The text of every constructor parameter list the implementation declares, primary or explicit.</summary>
        private static string ConstructorParameterText(string source, string implementation)
        {
            var parameters = new System.Text.StringBuilder();
            foreach (var match in Regex.Matches(
                source,
                $@"(?:(?:public|internal)\s+|(?:class|record)\s+){Regex.Escape(implementation)}\s*\(",
                RegexOptions.None,
                TimeSpan.FromSeconds(2)).Cast<Match>())
            {
                var open = match.Index + match.Length - 1;
                var close = MatchingDelimiter(source, open, '(', ')');
                parameters.AppendLine(source[(open + 1)..close]);
            }

            return parameters.ToString();
        }

        private static string? FindSourceFile(string typeName)
        {
            var sourceRoot = RepositoryLayout.PathFromRoot("src");
            return Directory.EnumerateFiles(sourceRoot, typeName + ".cs", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateFiles(sourceRoot, typeName + ".axaml.cs", SearchOption.AllDirectories))
                .FirstOrDefault(path => !path.Contains(
                    $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                    StringComparison.Ordinal));
        }

        private static int MatchingDelimiter(string text, int openIndex, char open, char close)
        {
            var depth = 0;
            for (var index = openIndex; index < text.Length; index++)
            {
                if (text[index] == open)
                {
                    depth++;
                }
                else if (text[index] == close && --depth == 0)
                {
                    return index;
                }
            }

            throw new InvalidOperationException($"Unbalanced {open}{close} from index {openIndex}.");
        }

        private static string[] SplitTopLevel(string genericArguments)
        {
            var parts = new List<string>();
            var depth = 0;
            var start = 0;
            for (var index = 0; index < genericArguments.Length; index++)
            {
                switch (genericArguments[index])
                {
                    case '<':
                        depth++;
                        break;
                    case '>':
                        depth--;
                        break;
                    case ',' when depth == 0:
                        parts.Add(genericArguments[start..index].Trim());
                        start = index + 1;
                        break;
                }
            }

            parts.Add(genericArguments[start..].Trim());
            return [.. parts];
        }
    }
}
