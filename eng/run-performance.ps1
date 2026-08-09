[CmdletBinding()]
param(
    [string]$Baseline = 'none',

    [ValidateRange(1, 20)]
    [int]$Repetitions = 1,

    [string]$Output = 'artifacts/performance/T10'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'tests/ApSolutions.LocalMedia.PerformanceTests/ApSolutions.LocalMedia.PerformanceTests.csproj'
$outputRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot $Output))
$mode = if ($Baseline -eq 'none') { 'initial' } else { 'measured' }
$runRoot = Join-Path $outputRoot $mode
New-Item -ItemType Directory -Force -Path $runRoot | Out-Null

Push-Location $repoRoot
try {
    $failedRuns = [System.Collections.Generic.List[int]]::new()
    for ($index = 1; $index -le $Repetitions; $index++) {
        $runName = "run-$index"
        $metricsDirectory = Join-Path $runRoot $runName
        New-Item -ItemType Directory -Force -Path $metricsDirectory | Out-Null
        $env:APSOLUTIONS_LOCALMEDIA_PERF_RESULTS = $metricsDirectory
        $env:APSOLUTIONS_LOCALMEDIA_PERF_RUN = $runName
        dotnet test $project -c Release --no-restore `
            --logger "trx;LogFileName=$runName.trx" `
            --results-directory $metricsDirectory
        if ($LASTEXITCODE -ne 0) {
            $failedRuns.Add($index)
        }
    }

    $metrics = Get-ChildItem -LiteralPath $runRoot -Recurse -Filter '*.json' |
        Where-Object { $_.Name -ne 'summary.json' } |
        ForEach-Object { Get-Content -LiteralPath $_.FullName -Raw | ConvertFrom-Json }
    $summary = $metrics |
        Group-Object metric |
        ForEach-Object {
            $p95Values = @($_.Group | ForEach-Object { [double]$_.p95 } | Sort-Object)
            $medianValues = @($_.Group | ForEach-Object { [double]$_.median } | Sort-Object)
            [pscustomobject]@{
                metric = $_.Name
                runs = $_.Count
                unit = [string]$_.Group[0].unit
                median = $medianValues[[int][Math]::Floor($medianValues.Count / 2)]
                p95 = $p95Values[[Math]::Max(0, [int][Math]::Ceiling($p95Values.Count * 0.95) - 1)]
                budget = [double]$_.Group[0].budget
                passed = (@($_.Group | Where-Object { -not $_.passed }).Count -eq 0)
            }
        } |
        Sort-Object metric

    if ($Baseline -ne 'none') {
        $baselinePath = [IO.Path]::GetFullPath((Join-Path $repoRoot $Baseline))
        if (-not (Test-Path -LiteralPath $baselinePath -PathType Leaf)) {
            throw "Versioned baseline was not found: $baselinePath"
        }

        $baselineMetrics = @{}
        foreach ($line in Get-Content -LiteralPath $baselinePath) {
            if ($line -match '^\| `(?<metric>[^`]+)` \| [^|]+ \| (?<median>[0-9.]+) \| (?<p95>[0-9.]+) \|') {
                $baselineMetrics[$Matches.metric] = [pscustomobject]@{
                    median = [double]::Parse($Matches.median, [Globalization.CultureInfo]::InvariantCulture)
                    p95 = [double]::Parse($Matches.p95, [Globalization.CultureInfo]::InvariantCulture)
                }
            }
        }

        foreach ($item in $summary) {
            $baselineMetric = $baselineMetrics[$item.metric]
            if ($null -ne $baselineMetric) {
                $item | Add-Member -NotePropertyName baselineP95 -NotePropertyValue $baselineMetric.p95
                $delta = if ($baselineMetric.p95 -eq 0) {
                    if ($item.p95 -eq 0) { 0 } else { [double]::PositiveInfinity }
                }
                else {
                    100 * ($item.p95 - $baselineMetric.p95) / $baselineMetric.p95
                }
                $item | Add-Member -NotePropertyName p95DeltaPercent -NotePropertyValue $delta
            }
        }
    }

    $hardware = Get-CimInstance Win32_ComputerSystem
    $processor = Get-CimInstance Win32_Processor | Select-Object -First 1
    $report = [ordered]@{
        generatedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        commit = (git rev-parse HEAD)
        baseline = $Baseline
        repetitions = $Repetitions
        hardware = [ordered]@{
            manufacturer = $hardware.Manufacturer
            model = $hardware.Model
            processor = $processor.Name
            logicalProcessors = $processor.NumberOfLogicalProcessors
            memoryBytes = [long]$hardware.TotalPhysicalMemory
        }
        metrics = @($summary)
    }
    $summaryPath = Join-Path $runRoot 'summary.json'
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $summaryPath -Encoding utf8
    $summary | Format-Table metric, runs, unit, median, p95, budget, baselineP95, p95DeltaPercent, passed -AutoSize
    Write-Output "Performance summary: $summaryPath"
    if ($failedRuns.Count -gt 0) {
        throw "Performance budgets failed in runs: $($failedRuns -join ', ')."
    }
}
finally {
    Remove-Item Env:APSOLUTIONS_LOCALMEDIA_PERF_RESULTS -ErrorAction SilentlyContinue
    Remove-Item Env:APSOLUTIONS_LOCALMEDIA_PERF_RUN -ErrorAction SilentlyContinue
    Pop-Location
}
