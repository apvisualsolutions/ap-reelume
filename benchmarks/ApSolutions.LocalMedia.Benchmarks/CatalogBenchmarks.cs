// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using ApSolutions.LocalMedia.Application.Catalog;
using ApSolutions.LocalMedia.PerformanceTests.Fixtures;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace ApSolutions.LocalMedia.Benchmarks;

[MemoryDiagnoser]
public class CatalogBenchmarks
{
    private Catalog10kBuilder? _fixture;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _fixture = await Catalog10kBuilder.CreateAsync();
        await FirstSearchPageAsync();
    }

    [Benchmark]
    public Task<CatalogPage> FirstSearchPageAsync() => Fixture.Catalog.QueryAsync(
        new CatalogQuery(Search: "amelie", PageSize: 50));

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        if (_fixture is not null)
        {
            await _fixture.DisposeAsync();
        }
    }

    private Catalog10kBuilder Fixture =>
        _fixture ?? throw new InvalidOperationException("Benchmark fixture is not initialized.");
}

public static class Program
{
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
