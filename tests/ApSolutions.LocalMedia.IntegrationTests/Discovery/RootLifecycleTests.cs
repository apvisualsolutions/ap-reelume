using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Input;
using ApSolutions.LocalMedia.Application.Discovery;
using ApSolutions.LocalMedia.IntegrationTests.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace ApSolutions.LocalMedia.IntegrationTests.Discovery;

public sealed class RootLifecycleTests
{
    private static readonly string[] ExpectedRootKinds = ["Local", "Unc", "Usb"];

    [Fact]
    public async Task Local_USB_and_UNC_roots_are_validated_independently_and_persisted()
    {
        using var directory = new DatabaseTestDirectory();
        await using var harness = await RootHarness.CreateAsync(directory.DatabasePath);
        var local = directory.Path;
        var usb = @"R:\Portable Media";
        var unc = @"\\media-server\library\Shows";
        harness.UsePathProbe(path => path is not null &&
            (path.Equals(local, StringComparison.OrdinalIgnoreCase)
                || path.Equals(usb, StringComparison.OrdinalIgnoreCase)
                || path.Equals(unc, StringComparison.OrdinalIgnoreCase)));

        await harness.AddAsync(local, "Local");
        await harness.AddAsync(usb, "Usb");
        await harness.AddAsync(unc, "Unc");

        var persisted = await harness.ReadRootsAsync();
        Assert.Equal(3, persisted.Count);
        Assert.Equal(ExpectedRootKinds, persisted.Select(item => item.Kind).Order().ToArray());
        Assert.All(persisted, item => Assert.Equal("Available", item.Availability));
    }

    [Fact]
    public async Task Duplicate_case_separator_and_nested_roots_are_rejected_without_affecting_other_roots()
    {
        using var directory = new DatabaseTestDirectory();
        await using var harness = await RootHarness.CreateAsync(directory.DatabasePath);
        harness.UsePathProbe(_ => true);
        await harness.AddAsync(@"C:\Media", "Local");

        var duplicate = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.AddAsync(@"c:/media/", "Local"));
        var nested = await Assert.ThrowsAsync<InvalidOperationException>(
            () => harness.AddAsync(@"C:\Media\Movies", "Local"));
        await harness.AddAsync(@"D:\Other", "Usb");

        Assert.Contains("duplicate", duplicate.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nested", nested.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, (await harness.ReadRootsAsync()).Count);
    }

    [Fact]
    public async Task Access_denied_error_is_actionable_and_does_not_block_an_independent_root()
    {
        using var directory = new DatabaseTestDirectory();
        await using var harness = await RootHarness.CreateAsync(directory.DatabasePath);
        harness.UsePathProbe(
            _ => true,
            path =>
            {
                if (path.Contains("Denied", StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException("Access denied by test probe.");
                }
            });

        var denied = await Assert.ThrowsAnyAsync<Exception>(
            () => harness.AddAsync(@"C:\Denied", "Local"));
        await harness.AddAsync(@"D:\Allowed", "Usb");

        var pathFailure = Assert.IsType<LibraryRootPathException>(denied);
        Assert.Equal(LibraryRootPathError.AccessDenied, pathFailure.Error);
        Assert.Equal(@"C:\Denied", pathFailure.Path);
        Assert.Equal("Local", pathFailure.Kind.ToString());
        Assert.Single(await harness.ReadRootsAsync());
    }

    [Fact]
    public async Task Adding_and_removing_a_root_never_creates_copies_moves_or_deletes_media()
    {
        using var directory = new DatabaseTestDirectory();
        var rootPath = System.IO.Path.Combine(directory.Path, "library");
        Directory.CreateDirectory(rootPath);
        await File.WriteAllBytesAsync(
            System.IO.Path.Combine(rootPath, "movie.mp4"),
            [1, 2, 3, 4],
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            System.IO.Path.Combine(rootPath, "episode.mkv"),
            [5, 6, 7],
            TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(
            System.IO.Path.Combine(rootPath, "clip.avi"),
            [8, 9],
            TestContext.Current.CancellationToken);
        var before = await HashInventoryAsync(rootPath);
        await using var harness = await RootHarness.CreateAsync(directory.DatabasePath);
        harness.UseDefaultPathNormalizer();

        var root = await harness.AddAsync(rootPath, "Local");
        var persisted = await harness.GetAsync(root);
        Assert.NotNull(persisted);
        Assert.Equal(Read(root, "Path"), Read(persisted, "Path"));
        Assert.True(RootHarness.RemoveCommandPreserveCatalogDefaultsToTrue());
        await harness.RemoveAsync(root);

        Assert.Equal(before, await HashInventoryAsync(rootPath));
        Assert.Empty(await harness.ReadRootsAsync());
        Assert.DoesNotContain(
            Directory.EnumerateFiles(directory.Path, "*", SearchOption.AllDirectories),
            path => path.EndsWith(".copy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Onboarding_requires_explicit_consent_before_the_initial_scan()
    {
        using var directory = new DatabaseTestDirectory();
        var rootPath = System.IO.Path.Combine(directory.Path, "onboarding");
        Directory.CreateDirectory(rootPath);
        await using var harness = await RootHarness.CreateAsync(directory.DatabasePath);
        harness.UsePathProbe(Directory.Exists);

        var onboarding = harness.CreateOnboardingViewModel();
        Set(onboarding, "Path", rootPath);
        var selectKind = Assert.IsAssignableFrom<ICommand>(Read(onboarding, "SelectKindCommand"));
        selectKind.Execute(RootHarness.EnumValue("RootKind", "Usb"));
        selectKind.Execute(RootHarness.EnumValue("RootKind", "Local"));
        Set(onboarding, "SelectedScanPolicy", RootHarness.EnumValue("ScanPolicy", "Startup, Manual"));
        await InvokeTaskAsync(onboarding, "AddAsync", CancellationToken.None);

        Assert.Equal(true, Read(onboarding, "InitialScanConsentRequired"));
        Assert.Equal(false, Read(onboarding, "CanStartInitialScan"));
        var grantConsent = Assert.IsAssignableFrom<ICommand>(Read(onboarding, "GrantInitialScanConsentCommand"));
        grantConsent.Execute(null);
        Assert.Equal(true, Read(onboarding, "CanStartInitialScan"));
    }

    private static async Task<IReadOnlyDictionary<string, string>> HashInventoryAsync(string rootPath)
    {
        var values = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
        {
            await using var stream = File.OpenRead(path);
            values[System.IO.Path.GetRelativePath(rootPath, path)] = Convert.ToHexString(
                await SHA256.HashDataAsync(stream));
        }

        return values;
    }

    private static object? Read(object value, string propertyName) =>
        value.GetType().GetProperty(propertyName)?.GetValue(value);

    private static void Set(object value, string propertyName, object propertyValue)
    {
        var property = value.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        property.SetValue(value, propertyValue);
    }

    private static async Task<object?> InvokeTaskAsync(object target, string methodName, params object[] arguments)
    {
        var method = target.GetType().GetMethod(methodName);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method.Invoke(target, arguments));
        await task;
        return task.GetType().GetProperty("Result")?.GetValue(task);
    }

    private sealed class RootHarness : IAsyncDisposable
    {
        private readonly object _factory;
        private readonly object _repository;
        private object? _normalizer;
        private object? _addRoot;

        private RootHarness(object factory, object repository)
        {
            _factory = factory;
            _repository = repository;
        }

        public static async Task<RootHarness> CreateAsync(string databasePath)
        {
            var factory = DatabaseTestHarness.CreateFactory(databasePath);
            await DatabaseTestHarness.MigrateAsync(DatabaseTestHarness.CreateDefaultRunner(factory));
            var repositoryType = RequireType(
                "ApSolutions.LocalMedia.Infrastructure",
                "ApSolutions.LocalMedia.Infrastructure.Data.Repositories.LibraryRootRepository");
            var repository = Activator.CreateInstance(repositoryType, factory);
            Assert.NotNull(repository);
            return new RootHarness(factory, repository);
        }

        public void UsePathProbe(Func<string, bool> exists, Action<string>? assertReadable = null)
        {
            var normalizerType = RequireType(
                "ApSolutions.LocalMedia.Infrastructure",
                "ApSolutions.LocalMedia.Infrastructure.FileSystem.WindowsPathNormalizer");
            _normalizer = Activator.CreateInstance(
                normalizerType,
                exists,
                assertReadable ?? (_ => { }));
            Assert.NotNull(_normalizer);
            var addType = RequireType(
                "ApSolutions.LocalMedia.Application",
                "ApSolutions.LocalMedia.Application.Discovery.AddLibraryRoot");
            _addRoot = Activator.CreateInstance(addType, _repository, _normalizer);
            Assert.NotNull(_addRoot);
        }

        public void UseDefaultPathNormalizer()
        {
            var normalizerType = RequireType(
                "ApSolutions.LocalMedia.Infrastructure",
                "ApSolutions.LocalMedia.Infrastructure.FileSystem.WindowsPathNormalizer");
            _normalizer = Activator.CreateInstance(normalizerType);
            Assert.NotNull(_normalizer);
            var addType = RequireType(
                "ApSolutions.LocalMedia.Application",
                "ApSolutions.LocalMedia.Application.Discovery.AddLibraryRoot");
            _addRoot = Activator.CreateInstance(addType, _repository, _normalizer);
            Assert.NotNull(_addRoot);
        }

        public async Task<object> AddAsync(string path, string kindName)
        {
            Assert.NotNull(_addRoot);
            var commandType = RequireType(
                "ApSolutions.LocalMedia.Application",
                "ApSolutions.LocalMedia.Application.Discovery.AddLibraryRootCommand");
            var command = Activator.CreateInstance(
                commandType,
                path,
                EnumValue("RootKind", kindName),
                EnumValue("ScanPolicy", "Startup, Manual"));
            Assert.NotNull(command);
            var root = await InvokeTaskAsync(_addRoot, "ExecuteAsync", command, CancellationToken.None);
            Assert.NotNull(root);
            return root;
        }

        public async Task RemoveAsync(object root)
        {
            var removeType = RequireType(
                "ApSolutions.LocalMedia.Application",
                "ApSolutions.LocalMedia.Application.Discovery.RemoveLibraryRoot");
            var remove = Activator.CreateInstance(removeType, _repository);
            Assert.NotNull(remove);
            var commandType = RequireType(
                "ApSolutions.LocalMedia.Application",
                "ApSolutions.LocalMedia.Application.Discovery.RemoveLibraryRootCommand");
            var id = Read(root, "Id");
            Assert.NotNull(id);
            var command = Activator.CreateInstance(commandType, id, true);
            Assert.NotNull(command);
            await InvokeTaskAsync(remove, "ExecuteAsync", command, CancellationToken.None);
        }

        public async Task<object?> GetAsync(object root)
        {
            var id = Read(root, "Id");
            Assert.NotNull(id);
            return await InvokeTaskAsync(_repository, "GetAsync", id, CancellationToken.None);
        }

        public static bool RemoveCommandPreserveCatalogDefaultsToTrue()
        {
            var commandType = RequireType(
                "ApSolutions.LocalMedia.Application",
                "ApSolutions.LocalMedia.Application.Discovery.RemoveLibraryRootCommand");
            var parameter = Assert.Single(commandType.GetConstructors()).GetParameters()[1];
            return parameter.HasDefaultValue && Equals(true, parameter.DefaultValue);
        }

        public static object EnumValue(string enumName, string value) => Enum.Parse(
            RequireType("ApSolutions.LocalMedia.Domain", $"ApSolutions.LocalMedia.Domain.Discovery.{enumName}"),
            value);

        public object CreateOnboardingViewModel()
        {
            Assert.NotNull(_addRoot);
            var type = RequireType(
                "ApSolutions.LocalMedia.Presentation",
                "ApSolutions.LocalMedia.Presentation.Onboarding.RootOnboardingViewModel");
            // The folder-management collaborators (LIB-A01) are optional and absent here: this
            // harness exercises adding a root, and reflection does not fill defaults on its own.
            var viewModel = Activator.CreateInstance(type, _addRoot, null, null);
            Assert.NotNull(viewModel);
            return viewModel;
        }

        public async Task<IReadOnlyList<(string Kind, string Availability)>> ReadRootsAsync()
        {
            var result = await InvokeTaskAsync(_repository, "ListAsync", CancellationToken.None);
            var roots = Assert.IsAssignableFrom<System.Collections.IEnumerable>(result);
            return roots.Cast<object>()
                .Select(root => (Read(root, "Kind")?.ToString() ?? string.Empty, Read(root, "Availability")?.ToString() ?? string.Empty))
                .ToArray();
        }

        public ValueTask DisposeAsync()
        {
            if (_factory is IDisposable disposable)
            {
                disposable.Dispose();
            }

            SqliteConnection.ClearAllPools();
            return ValueTask.CompletedTask;
        }

        private static Type RequireType(string assemblyName, string fullName)
        {
            var type = Assembly.Load(assemblyName).GetType(fullName, throwOnError: false);
            Assert.NotNull(type);
            return type;
        }
    }
}
