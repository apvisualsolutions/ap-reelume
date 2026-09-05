# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later

<#
.SYNOPSIS
    TST-001: the automated coverage gate. Every source file that is new against the base ref
    must arrive with at least the minimum line and branch coverage in this run's reports.

.DESCRIPTION
    The run's Cobertura files are merged with reportgenerator and read per file. A new file is
    one that exists in HEAD and not in the base ref (a plain tree comparison, so a shallow CI
    checkout needs no merge base). A new file that never appears in the merged report has no
    instrumentable lines — interfaces and pure contracts — and passes with that stated; a file
    with real code that no test executes appears with zero hits and fails loudly.

    In this repository main advances by fast-forward together with the working branch, so on CI
    the diff is usually empty and the gate's teeth are local: it runs inside eng/verify.ps1
    before anything is pushed. On a pull request from elsewhere the diff is real and CI bites.
#>
[CmdletBinding()]
param(
    [string]$ResultsDirectory = 'artifacts/test-results/verify-win-x64',

    [string]$BaseRef = 'origin/main',

    [double]$MinimumLinePercent = 96.0,

    [double]$MinimumBranchPercent = 96.0,

    # Writes eng/coverage-debt.txt from this run instead of checking against it. The list has to be
    # produced by the same arithmetic that verifies it, or the two drift: generating it separately
    # disagreed on three files the first time it was tried, because a path can appear twice in the
    # merged report and the check takes the first match rather than merging them.
    [switch]$WriteDebt
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

# The debt floors belong to the environment that measures them, and that environment is CI. A
# hosted runner has no audio device and its clocks are not this machine's, so seven files read
# differently there — WindowsAudioDeviceCatalog.cs holds 79/61 here and reads 32/11 there, because
# half of it never runs without a device to enumerate. The floors are written from a CI run, and
# since this machine measures *more* on those files, checking them here would fail asking to raise
# a floor that CI cannot meet. So off CI the debt is reported and does not block; the watched list
# and the new-file gate below hold everywhere, because neither varies with the hardware.
$isContinuousIntegration = $env:GITHUB_ACTIONS -eq 'true'
$resultsRoot = if ([IO.Path]::IsPathRooted($ResultsDirectory)) { $ResultsDirectory } else { Join-Path $repoRoot $ResultsDirectory }

Push-Location $repoRoot
try {
    git rev-parse --verify --quiet "$BaseRef^{commit}" *> $null
    if ($LASTEXITCODE -ne 0) {
        # A gate that cannot see its base ref must say so and fail, never quietly pass.
        git fetch origin main *> $null
        git rev-parse --verify --quiet "$BaseRef^{commit}" *> $null
        if ($LASTEXITCODE -ne 0) {
            Write-Error "The base ref '$BaseRef' does not exist and could not be fetched; the coverage gate has nothing to compare against."
            exit 1
        }
    }

    $addedFiles = @(git diff --name-only --diff-filter=A $BaseRef HEAD -- src |
            Where-Object { $_ -like '*.cs' })
    if ($LASTEXITCODE -ne 0) { Write-Error 'git diff failed.'; exit 1 }

    # A new path is not the same thing as new code. Splitting a large file into partials (ARQ-006)
    # creates paths whose every line already shipped, and holding moved code to a coverage bar it
    # never had to meet would price refactoring out of the repository — the gate would be pushing
    # against the tidying it exists to make safe.
    #
    # So "new" is decided by content: a file whose non-trivial lines nearly all already existed
    # somewhere in the base ref is a move, and it is exempted out loud rather than silently. Nothing
    # is weakened by this. To smuggle uncovered code past the gate a person would have to have
    # written it into the base ref first, where this same gate would have held it.
    # Only executable code counts on either side. Comments carry no coverage, and a moved block
    # arriving with the explanation it always deserved would otherwise read as new code.
    $isCode = {
        param($text)
        $text.Length -ge 12 -and
        -not $text.StartsWith('using ') -and
        -not $text.StartsWith('//') -and
        -not $text.StartsWith('*') -and
        -not $text.StartsWith('/*')
    }

    $baseCorpus = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($tracked in @(git ls-tree -r --name-only $BaseRef -- src | Where-Object { $_ -like '*.cs' })) {
        foreach ($line in @(git show "${BaseRef}:${tracked}" 2>$null)) {
            $trimmed = $line.Trim()
            if (& $isCode $trimmed) { [void]$baseCorpus.Add($trimmed) }
        }
    }

    $newFiles = @()
    $movedFiles = @()
    foreach ($file in $addedFiles) {
        # Read the file out of HEAD, not off disk: the comparison is HEAD against the base ref, and a
        # path added in HEAD may already have been moved or deleted again in the working tree.
        $lines = @(git show "HEAD:$file" 2>$null |
                ForEach-Object { $_.Trim() } |
                Where-Object { & $isCode $_ })
        $known = @($lines | Where-Object { $baseCorpus.Contains($_) }).Count
        $movedShare = if ($lines.Count -gt 0) { 1.0 * $known / $lines.Count } else { 0.0 }
        # 0.85 rather than a rounder 0.9 because a split's unavoidable scaffolding — the partial
        # class declaration and one signature per module — is genuinely new text, and it weighs
        # proportionally more the smaller the module is. A file that is one seventh new text and
        # six sevenths lines lifted verbatim is a move.
        if ($lines.Count -gt 0 -and $movedShare -ge 0.85) {
            $movedFiles += [pscustomobject]@{ File = $file; MovedPct = [math]::Round(100.0 * $movedShare, 1) }
        }
        else {
            $newFiles += $file
        }
    }

    if ($movedFiles.Count -gt 0) {
        Write-Output "Coverage gate: these paths are new but their code is not, so they are held to the bar their code already met:"
        $movedFiles | Format-Table -AutoSize | Out-String -Width 200 | Write-Output
    }

    if ($newFiles.Count -eq 0) {
        Write-Output "Coverage gate: no source file is new against $BaseRef."
    }

    $reports = @(Get-ChildItem -LiteralPath $resultsRoot -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue)
    if ($reports.Count -eq 0) {
        Write-Error "No Cobertura report found under $resultsRoot; run the test suites with coverage first."
        exit 1
    }

    $mergedDirectory = Join-Path $resultsRoot 'coverage-gate'
    # Generated sources live under obj/ and are gone by the time reports merge; excluding them
    # keeps the merge quiet and no hand-written file matches the filter.
    dotnet tool run reportgenerator `
        "-reports:$(($reports | ForEach-Object { $_.FullName }) -join ';')" `
        "-targetdir:$mergedDirectory" `
        '-reporttypes:Cobertura' `
        '-filefilters:-*.g.cs' `
        '-verbosity:Off'
    if ($LASTEXITCODE -ne 0) { Write-Error 'reportgenerator failed to merge the coverage reports.'; exit 1 }

    # Per file: line number → executed, plus branch covered/total. Partial classes repeat a file
    # across <class> nodes, so lines merge by number and a line executed anywhere counts once.
    $coverageByFile = @{}
    $merged = [xml](Get-Content -LiteralPath (Join-Path $mergedDirectory 'Cobertura.xml') -Raw)
    foreach ($class in $merged.SelectNodes('//class')) {
        $filename = ($class.filename -replace '\\', '/')
        if (-not $coverageByFile.ContainsKey($filename)) {
            $coverageByFile[$filename] = @{ Lines = @{}; BranchesCovered = 0; BranchesTotal = 0 }
        }

        $entry = $coverageByFile[$filename]
        foreach ($line in $class.SelectNodes('lines/line')) {
            $number = [int]$line.number
            $hit = [int64]$line.hits -gt 0
            if (-not $entry.Lines.ContainsKey($number) -or $hit) {
                $entry.Lines[$number] = $hit
            }

            if ($line.'condition-coverage' -match '\((?<covered>\d+)/(?<total>\d+)\)') {
                $entry.BranchesCovered += [int]$Matches['covered']
                $entry.BranchesTotal += [int]$Matches['total']
            }
        }
    }

    # Per file, the same arithmetic the gate uses, so a watched file and a new one are judged alike.
    function Measure-File {
        param([hashtable]$Coverage, [string]$Path)

        $normalised = $Path -replace '\\', '/'
        $key = $Coverage.Keys | Where-Object { $_.EndsWith($normalised, [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
        if (-not $key) { return $null }

        $entry = $Coverage[$key]
        $totalLines = $entry.Lines.Count
        $coveredLines = @($entry.Lines.Values | Where-Object { $_ }).Count
        return [pscustomobject]@{
            LinePct   = if ($totalLines -gt 0) { 100.0 * $coveredLines / $totalLines } else { 100.0 }
            BranchPct = if ($entry.BranchesTotal -gt 0) { 100.0 * $entry.BranchesCovered / $entry.BranchesTotal } else { 100.0 }
        }
    }

    # ------------------------------------------------------------- watched files
    <#
        What the gate above cannot see. Newness is decided against the base ref, so a file that
        shipped long ago and gets worse is watched by nobody — and that is measured, not feared:
        ARQ-004 thinned PlayerVersionsViewModel, took its covered lines with it, and dropped it from
        60.61/27.27 to 45.45/14.29 without a single gate saying a word.

        Each entry carries the floor its code meets today, so the list works like the orphan list in
        ServiceConsumptionTests: a file below its floor fails, and so does one above it, because the
        floor has to be raised to what was actually measured. The debt shrinks by improvement and
        never by drift, and lowering a floor is a visible line in a diff rather than a quiet drift.

        It grows for exactly one reason, and 2026-08-30 was the first time: a file that is BORN
        below the bar. The rule was written against degradation -- a file that was at the bar and
        got worse -- and a new file is not that. Seven arrived with the Courses batch and the
        ratchet went 186 -> 193, each with its measured reason in the debt file's header. A file
        that was on the list and degrades still fails, which is what the rule was for.
    #>
    $watched = @(
        # TST-001 named all three on 2026-08-09 and all three are paid: two on 2026-08-10 and this
        # one, the last, with unit tests aimed at its decisions rather than at the scans that
        # already walked its happy path. The list stays whatever the numbers say — a file that
        # reaches the bar is watched at the bar, not dropped.
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Application/Discovery/ReconcileScannedFiles.cs'
            Lines    = 100.00
            Branches = 100.00
        }
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Infrastructure/FileSystem/CompositeFileIdentityProvider.cs'
            Lines    = 100.00
            Branches = 100.00
        }
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Presentation/Player/PlayerVersionsViewModel.cs'
            Lines    = 100.00
            Branches = 100.00
        }

        # The two files the isolation rule went through on 2026-08-16. Neither was new, so neither
        # would have been measured by the gate above, and both decide where something leaves the
        # application: which registry key a startup entry is written to, and whether an address
        # reaches a browser at all. They arrive at the top and are held there.
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Windows/AppDataPaths.cs'
            Lines    = 100.00
            Branches = 100.00
        }
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Windows/Metadata/ShellExternalLinkLauncher.cs'
            Lines    = 100.00
            Branches = 100.00
        }

        # The surface batch 5 walked end to end. It arrived at 92.13/59.26 — an old file, so watched
        # by nobody — and the rule is that a batch pays for the file it touches before the file joins
        # this list, because a low floor enshrines the debt instead of watching it. Two things it
        # cost: a pair of unreachable branches, where `as AsyncRelayCommand` could not fail but could
        # stop matching, and one branch whose two sides were exercised by two different suites and
        # therefore read as half-covered forever, because merged Cobertura keeps the better report
        # for a line rather than the union of them.
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Presentation/Review/ReviewInboxViewModel.cs'
            Lines    = 100.00
            Branches = 100.00
        }

        # The third exit the isolation rule went through, on 2026-08-16: which folder an export leaves
        # in and which archive a restore reads back. It arrives new, so the gate above already
        # measured it once — and only once, because a file stops being new the moment it merges. It
        # decides what leaves the application, so it belongs beside the other two rather than being
        # forgotten at the bar it arrived at.
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Windows/Backup/HandoffArchivePicker.cs'
            Lines    = 100.00
            Branches = 100.00
        }

        # The fourth and fifth exits, on 2026-08-17: what the recovery screen hands to Windows when
        # the database will not open — the folder a copy would be in, and the request to end. They
        # join the other three because they decide what leaves the application, and because the
        # screen they serve is the one nobody can reach from inside the shell.
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Windows/Shell/WindowsSystemHandoff.cs'
            Lines    = 100.00
            Branches = 100.00
        }
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Windows/Shell/RecordingSystemHandoff.cs'
            Lines    = 100.00
            Branches = 100.00
        }

        # The source an isolated run is offered its releases by, and the manifest it reads them from.
        # They join the list for the reason the other exits did — they decide what the application
        # reaches for — and for one of their own: the manifest is where a release's hash and size
        # come from, so a hole here would be a hole in what the download then proves.
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Windows/Updates/HandoffUpdateSource.cs'
            Lines    = 100.00
            Branches = 100.00
        }
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Windows/Updates/HandoffUpdateManifest.cs'
            Lines    = 100.00
            Branches = 100.00
        }

        # And what stands in for the network while the download itself stays the product's own. The
        # transport is the one that has to be held here: it decides which address is answered and
        # with which file, and a hole in that is a way to read this machine through a manifest.
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Windows/Updates/HandoffUpdateDownloader.cs'
            Lines    = 100.00
            Branches = 100.00
        }
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Windows/Updates/HandoffUpdateTransport.cs'
            Lines    = 100.00
            Branches = 100.00
        }

        # The ninth exit. It stands in for a launcher that starts a real process, so what has to be
        # held here is its two refusals: an extension outside the approved list and a file that is
        # not there. A recorder that wrote down a handover the real one would have refused would make
        # every probe reading that record say nothing about the real launcher.
        [pscustomobject]@{
            File     = 'src/ApSolutions.LocalMedia.Windows/Playback/RecordingExternalPlaybackLauncher.cs'
            Lines    = 100.00
            Branches = 100.00
        }
    )

    $watchRows = @()
    $watchFailures = @()
    foreach ($entry in $watched) {
        $measured = Measure-File -Coverage $coverageByFile -Path $entry.File
        if (-not $measured) {
            $watchFailures += "$($entry.File) is watched but absent from the coverage report; a watched file that cannot be measured is not watched."
            $watchRows += [pscustomobject]@{ File = $entry.File; LinePct = 'absent'; BranchPct = 'absent'; Floor = "$($entry.Lines)/$($entry.Branches)"; Verdict = 'FAIL' }
            continue
        }

        $linePct = [math]::Round($measured.LinePct, 2)
        $branchPct = [math]::Round($measured.BranchPct, 2)
        $verdict = 'PASS'
        if ($linePct -lt $entry.Lines -or $branchPct -lt $entry.Branches) {
            $verdict = 'FAIL (below its floor)'
            $watchFailures += "$($entry.File) fell to $linePct% lines / $branchPct% branches, below the $($entry.Lines)/$($entry.Branches) floor it is held to."
        }
        elseif ($linePct -gt $entry.Lines -or $branchPct -gt $entry.Branches) {
            $verdict = 'FAIL (raise the floor)'
            $watchFailures += "$($entry.File) now reaches $linePct% lines / $branchPct% branches; raise its floor in eng/check-coverage.ps1 so the debt cannot come back."
        }

        $watchRows += [pscustomobject]@{
            File      = $entry.File
            LinePct   = $linePct
            BranchPct = $branchPct
            Floor     = "$($entry.Lines)/$($entry.Branches)"
            Verdict   = $verdict
        }
    }

    Write-Output "Coverage gate: watched files, held at every run whatever their age:"
    $watchRows | Format-Table -AutoSize | Out-String -Width 200 | Write-Output

    # ------------------------------------------------------------- the debt, and its ratchet
    <#
        Everything else in src/, held at the floor it meets today. The watched list above is the
        opposite promise -- files that reached the bar and are kept there -- and this one is the
        debt: 219 files on 2026-08-18, 217 on 2026-08-19, 216 on 2026-08-22 — each pinned so it
        cannot get worse while the number comes down. The 2026-08-22 move is two entries changing
        places: CatalogItemViewModel reached 100/100 when the five properties the poster card added
        stopped being read by nothing but a DataTemplate, PosterCardView.axaml arrived at the 100/50
        every view file measures, and RouteStateConverter reached the bar by losing three guards
        nothing in this repository could take. It works the way eng/walk-pending.txt worked, and that list went from
        126 to 0.

        A file leaves eng/coverage-debt.txt by reaching 96/96, never by being edited out: a floor
        below what was measured fails, exactly like a floor above it, so the file always says what
        is true today.

        "Measured" means measured by CI. The list is copied from the coverage-debt artefact of a CI
        run, never written from a local one: seven files depend on hardware a hosted runner does not
        have, so a floor measured here is a floor for a machine that never verifies anything. That
        is why -WriteDebt is run by the workflow on every build, pass or fail — moving a floor is
        then copying a measurement rather than guessing at one.
    #>
    # 189 desde el 2026-09-01, y es la primera vez que sube. La excepcion la autoriza esta misma
    # puerta por escrito —«add it with the reason and raise the ratchet in the same change»— y la
    # razon es estructural, no deuda: LessonsPanelView.axaml mide 100/50 porque esa es la UNICA rama
    # que el compilador de Avalonia genera para un .axaml, en la linea del elemento raiz, y todas las
    # vistas del arbol miden exactamente eso. Una vista nueva sube este numero en uno; cualquier otra
    # cosa que lo suba hay que discutirla.
    #
    # Los otros cuatro archivos que CRS-004 trajo a la lista salieron de ella mejorando, que es el
    # unico camino que admite: las ramas que les faltaban se nombraron con el JSON de coverlet y se
    # cubrieron con pruebas antes de escribir el archivo.
    #
    # 189 desde el 2026-09-02, y esta vez BAJA: WindowsAudioEndpointConfigurator.cs entro en la lista
    # a 23/20 —hardware ausente— y salio de ella a 100/100 el mismo dia, sin tocar esta puerta.
    #
    # Lo que lo movio no fue un suelo sino un seam. La clase era COM de arriba abajo, asi que la
    # aritmetica que decide cuantos canales salen por los altavoces solo podia ejecutarse en una
    # maquina con la tarjeta; detras de dos interfaces —IEndpointFormatStore e IEndpointFormatProbe—
    # se ejecuta en cualquier sitio, y diecisiete pruebas la recorren. Lo unico que queda fuera es la
    # CREACION de los objetos del sistema y sus catch, marcada con ExcludeFromCodeCoverage, que es lo
    # que coverlet documenta para «metodos dificiles o imposibles de probar directamente».
    #
    # La regla que sale de esto: un archivo nuevo que no llega al liston porque depende de hardware
    # casi siempre tiene dentro dos cosas distintas —lo que habla con la maquina y lo que decide—, y
    # separarlas cuesta menos que discutir la puerta.
    #
    # Y LibVlcAudioOutputAdapter.cs NO baja de 86/87 aunque el artefacto del run que lo destapo dijera
    # 77/75: ese run midio el codigo nuevo antes de que existieran las cuatro pruebas que lo cubren,
    # que entran en el mismo cambio. Medido aqui con la aritmetica de esta puerta: 88/87. Un suelo que
    # baja es una bajada, y la salida a una bajada es cubrir, no rebajar.
    # 189 desde el 2026-09-05, y por la unica razon que esta puerta acepta por escrito: una vista
    # nueva. PlaybackSettingsView.axaml mide 100/50 igual que las otras sesenta, porque esa mitad es
    # la unica rama que el compilador de Avalonia genera para un .axaml, en la linea del elemento
    # raiz. No es deuda: es lo que vale un .axaml.
    #
    # Su ViewModel llego en el mismo cambio y NO esta en la lista. El run lo midio a 90/95 y la
    # salida fue cubrirlo, no aparcarlo: el JSON de coverlet nombro las cuatro lineas y las dos ramas
    # -los dos topes del deslizador, leer la duracion con la cuenta atras apagada, y escribirla en
    # ese mismo estado-, y con sus tres pruebas quedo en 100/100. Un archivo sale de esta lista
    # mejorando, y uno nuevo solo entra cuando no puede mejorar.
    $debtRatchet = 189
    $debtFile = Join-Path $PSScriptRoot 'coverage-debt.txt'

    # Every file in src/ that this run measures below the bar, with the floor it would be given.
    # -WriteDebt prints these; the check below compares them against the list.
    $belowTheBar = @()
    foreach ($key in ($coverageByFile.Keys | Sort-Object)) {
        $relative = $key -replace '^.*?/(src/)', '$1'
        if (-not $relative.StartsWith('src/')) { continue }
        # The first key wins, exactly as Measure-File picks it, so what is written is what will
        # be read back.
        if ($belowTheBar.File -contains $relative) { continue }
        $m = Measure-File -Coverage $coverageByFile -Path $relative
        if (-not $m) { continue }
        $lineFloor = [math]::Floor($m.LinePct)
        $branchFloor = [math]::Floor($m.BranchPct)
        if ($lineFloor -ge 96 -and $branchFloor -ge 96) { continue }
        $belowTheBar += [pscustomobject]@{ File = $relative; Lines = $lineFloor; Branches = $branchFloor }
    }

    if ($WriteDebt) {
        $emitted = $belowTheBar
        $width = ($emitted.File | Measure-Object -Property Length -Maximum).Maximum + 2
        $lines = foreach ($e in $emitted) {
            '{0}{1,3} {2,3}' -f $e.File.PadRight($width), $e.Lines, $e.Branches
        }

        $head = Get-Content -LiteralPath $debtFile | Where-Object { $_.TrimStart().StartsWith('#') -or -not $_.Trim() }
        Set-Content -LiteralPath $debtFile -Value (@($head) + @($lines)) -Encoding utf8
        Write-Output "Coverage gate: wrote $($emitted.Count) file(s) to eng/coverage-debt.txt."
        exit 0
    }
    $debt = @(Get-Content -LiteralPath $debtFile |
        Where-Object { $_.Trim() -and -not $_.TrimStart().StartsWith('#') } |
        ForEach-Object {
            $parts = $_ -split '\s+'
            [pscustomobject]@{ File = $parts[0]; Lines = [int]$parts[1]; Branches = [int]$parts[2] }
        })

    $debtFailures = @()
    if ($debt.Count -gt $debtRatchet) {
        $debtFailures += ("eng/coverage-debt.txt holds $($debt.Count) files and the ratchet is " +
            "$debtRatchet. The list shrinks by improvement; it grows only for files born below the " +
            "bar, and then the ratchet moves in the same change with the reason written down.")
    }

    <#
        The crack this gate had until 2026-08-28, and it is the same one the watched list above was
        invented for — one level up.

        The loop below holds every file that IS on the list. A file that reached the bar and then got
        worse is on neither list and is watched by nobody: the new-file gate cannot see it, because
        newness is decided against the base ref and the file shipped long ago, and the debt loop
        cannot see it, because it only iterates over names already written down. Measured, not
        feared: CI measured 216 files below the bar while eng/coverage-debt.txt named 212, and the
        four it did not name had been degraded for days — PlayerView.axaml.cs at 65/41, which is the
        worst number in the whole tree.

        So the list is now asked to be COMPLETE rather than merely accurate. Anything under the bar
        that is not on it is named here, which is the only way a degradation announces itself.

        Off CI this reports and does not block, exactly like the floors above and for the same
        reason: seven files read differently on a hosted runner than they do on a development
        machine, so a file that is above the bar here can be under it there. The list belongs to what
        CI measures.
    #>
    # The watched list counts as a list. Those files are held to an exact floor by the loop above —
    # a run that measures one of them below its floor already fails, by name — so asking for them
    # here as well would be asking for the same file to be written down twice. Verified against the
    # coverage-debt artefact of three CI runs: not one watched file measures under the bar there.
    $listed = @($debt.File) + @($watched.File)
    $unlisted = @($belowTheBar | Where-Object { $listed -notcontains $_.File })
    foreach ($entry in $unlisted) {
        $debtFailures += ("$($entry.File) measures $($entry.Lines)/$($entry.Branches), under the " +
            "96/96 bar, and is on no list. It reached the bar once and got worse, which nothing " +
            "watches. Bring it back to the bar, or add it to eng/coverage-debt.txt with the reason " +
            "and raise the ratchet in the same change.")
    }

    $improved = 0
    foreach ($entry in $debt) {
        $measured = Measure-File -Coverage $coverageByFile -Path $entry.File
        if (-not $measured) { continue }

        $linePct = [math]::Floor($measured.LinePct)
        $branchPct = [math]::Floor($measured.BranchPct)
        if ($linePct -lt $entry.Lines -or $branchPct -lt $entry.Branches) {
            $debtFailures += ("$($entry.File) fell to $linePct/$branchPct, below the " +
                "$($entry.Lines)/$($entry.Branches) it held. Coverage does not go backwards.")
        }
        elseif ($linePct -ge 96 -and $branchPct -ge 96) {
            $improved++
            $debtFailures += ("$($entry.File) now reaches $linePct/$branchPct and is at the bar. " +
                "Take it out of eng/coverage-debt.txt and lower the ratchet by one.")
        }
        elseif ($linePct -gt $entry.Lines -or $branchPct -gt $entry.Branches) {
            $improved++
            $debtFailures += ("$($entry.File) now reaches $linePct/$branchPct; raise its floor in " +
                "eng/coverage-debt.txt so the debt cannot come back.")
        }
    }

    Write-Output ("Coverage gate: {0} file(s) still short of 96/96, ratchet {1}, {2} measured under the bar{3}." -f
        $debt.Count,
        $debtRatchet,
        $belowTheBar.Count,
        $(if ($improved) { ", $improved improved" } else { '' }))
    if ($debtFailures) {
        $debtVerdict = $debtFailures -join [Environment]::NewLine
        if ($isContinuousIntegration) { throw $debtVerdict }

        Write-Output $debtVerdict
        Write-Warning ('The debt floors are held by CI, which measures them; this run only reports ' +
            'them. Take the coverage-debt artefact from the CI run to move a floor.')
    }

    $rows = @()
    $failures = @()
    foreach ($file in $newFiles) {
        $normalised = $file -replace '\\', '/'
        $key = $coverageByFile.Keys | Where-Object { $_.EndsWith($normalised, [StringComparison]::OrdinalIgnoreCase) } | Select-Object -First 1
        if (-not $key) {
            $rows += [pscustomobject]@{ File = $file; LinePct = 'n/a'; BranchPct = 'n/a'; Verdict = 'PASS (no instrumentable lines)' }
            continue
        }

        $entry = $coverageByFile[$key]
        $totalLines = $entry.Lines.Count
        $coveredLines = @($entry.Lines.Values | Where-Object { $_ }).Count
        $linePct = if ($totalLines -gt 0) { 100.0 * $coveredLines / $totalLines } else { 100.0 }
        $branchPct = if ($entry.BranchesTotal -gt 0) { 100.0 * $entry.BranchesCovered / $entry.BranchesTotal } else { 100.0 }

        $passes = $linePct -ge $MinimumLinePercent -and $branchPct -ge $MinimumBranchPercent
        if (-not $passes) { $failures += $file }
        $rows += [pscustomobject]@{
            File      = $file
            LinePct   = [math]::Round($linePct, 2)
            BranchPct = [math]::Round($branchPct, 2)
            Verdict   = if ($passes) { 'PASS' } else { 'FAIL' }
        }
    }

    if ($rows.Count -gt 0) {
        $rows | Format-Table -AutoSize | Out-String -Width 200 | Write-Output
    }

    @{ new = $rows; watched = $watchRows } | ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath (Join-Path $resultsRoot 'coverage-gate.json') -Encoding utf8NoBOM

    foreach ($problem in $watchFailures) { Write-Error $problem }
    if ($failures.Count -gt 0) {
        Write-Error ("New files below {0}% lines / {1}% branches against {2}: {3}" -f
            $MinimumLinePercent, $MinimumBranchPercent, $BaseRef, ($failures -join ', '))
    }

    if ($watchFailures.Count -gt 0 -or $failures.Count -gt 0) { exit 1 }

    Write-Output ("Coverage gate: {0} new file(s) against {1} and {2} watched file(s) are where they have to be." -f
        $newFiles.Count, $BaseRef, $watched.Count)
    exit 0
}
finally {
    Pop-Location
}
