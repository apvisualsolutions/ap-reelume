# Developing AP Reelume on Windows

This guide describes the reproducible x64 MVP environment. Technical
identifiers remain under `ApSolutions.LocalMedia`; the public name is
**AP Reelume by AP Solutions**.

## Requirements

- Windows 11 x64 and PowerShell 7.
- .NET SDK `10.0.302`, pinned in `global.json`.
- Visual Studio 2026 Build Tools and Windows 11 SDK 22621 or later become
  mandatory for MSIX; I0 only requires the .NET SDK.
- Git with long-path support is recommended.

Check the SDK with `dotnet --info`. If it is missing, use the official .NET 10
LTS installer and confirm that `dotnet --version` returns `10.0.302`.

## Restore and build

From the repository root:

```powershell
dotnet tool restore
dotnet restore ApSolutions.LocalMedia.sln --locked-mode
dotnet build ApSolutions.LocalMedia.sln -c Debug --no-restore -warnaserror
```

NuGet versions are managed only in `Directory.Packages.props`. Every project
keeps its `packages.lock.json`; floating ranges are not allowed.

## Tests and verification

```powershell
dotnet test ApSolutions.LocalMedia.sln -c Debug --no-build -m:1 --settings eng/test.runsettings
pwsh ./eng/verify-docs.ps1
pwsh ./eng/verify.ps1 -Configuration Release -Runtime win-x64
pwsh ./eng/run-accessibility.ps1 -Mode Verify -Passes 2
pwsh ./eng/check-walk-coverage.ps1
```

`check-walk-coverage.ps1` compares the command controls the views declare against
the ones the autonomous walk **actually pressed with the mouse**, and whatever is
not pressed yet lives in [`eng/walk-pending.txt`](../../eng/walk-pending.txt) with
its reason. That list **may only shrink**.

`verify.ps1` builds **both packages** — x64 and ARM64 — and walks the first one's
lifecycle before running the tests, because the packaging suites read the sealed
artifacts and the reports `verify-package.ps1` produces: without them they would
be silently unenforceable. The ARM64 one is built even when x64 is what is being
verified, for the same reason: an artifact produced only when somebody remembers
to ask for it is a suite that stops being enforced. The reproducibility comparison makes two clean copies of the tree
with `git stash create`, so **stage what you are about to release before running
it**; an untracked file does not exist to a clean copy and the script stops when
it finds one. `-SkipPackaging` exists for the release workflow, which has already
done it, and not to shorten a local verification.

`generate-verification-manifest.ps1` produces the evidence manifest from
`docs/FEATURES.md` and refuses to write it when an unsettled commitment declares
no block. `generate-package-assets.ps1` redraws the package images from the theme
tokens; it is run by hand and its output committed, because a release must not be
able to change its own icons.

The accessibility audit has two modes and the difference matters. `Audit`
inventories every finding in one pass and always exits zero, which is how a red
cycle collects the whole list. `Verify` is the gate: any critical or major
defect fails it, and it must pass **twice in a row**. Neither mode can suppress
a check or lower a severity. `-RealApp` adds the UIA tree of the real
executable captured with FlaUI, which is what a screen reader actually reads.

Four traps in that audit, all found by measuring:

- `DesiredSize` **includes the margin** and `Bounds` does not. Comparing them raw
  reports every margined label as clipped; subtract the margin first.
- A prefixed attached attribute reaches `XDocument` under its compound local
  name — `AutomationProperties.LiveSetting`, not `LiveSetting` — so an equality
  match hides regions that do exist.
- Only what the application declares is its own surface. Controls whose
  `TemplatedParent` is not null are parts the theme generates, are not reachable
  by keyboard, and auditing them produces noise rather than defects.
- Windows **does not hand the foreground** to a process started in the
  background, so a synthetic tab walk over the real application ends up in some
  other window. Asking each control for focus through UIA does work, which is
  what a screen reader does; tab order itself is verified headless.

Avalonia 12.1 offers no environment variable to force a scale factor, so real
200 % cannot be simulated without changing the system scale: the scaling matrix
is covered with `SetRenderScaling` in automation, the same mechanism real DPI
uses.

When a production file reports 0 % with green tests, suspect the profiler
pitfall first, but **also check whether it simply has no tests**: a new adapter
without its own suite reads exactly the same.

`-m:1` is mandatory for solution-wide runs: without it MSBuild schedules one
invocation per project, the test hosts start together, and that destabilises
the native video library and kills a host on its connection timeout.

Reproducible results are written under `artifacts/test-results/`, which Git
ignores. Compiler and analyzer warnings are treated as errors.

Tests that **measure process resources** — handles, memory — or that kill a
process on purpose start a child from the test project's own executable,
because the shared host adds more noise than signal. **Every** child a test
starts must clear the profiler variables (`CORECLR_ENABLE_PROFILING`,
`CORECLR_PROFILER*`, and their `COR_*` equivalents), without exception and even
when the child measures nothing: inheriting them overwrites the parent's
coverage data and code that actually ran is reported as uncovered. That failure
is silent — the tests pass and the coverage lies — so suspect it whenever a file
with green tests reports zero. Its two pipes must also be drained together;
reading one to the end while the other fills deadlocks the child.

Five more pitfalls, all found by running the real application:

- A button bound to an `ICommand` **asks once and waits to be told**. A command
  whose `CanExecuteChanged` never fires leaves its button exactly as it was when
  it was built: disabled forever if it started that way. Headless tests do not
  see it because they call `CanExecute` directly, so the test has to **count the
  event's notifications**.
- UIA automation over the real application needs the window **maximised**: at
  the default size, anything below the visible area is absent from the tree and
  a physical click cannot reach it. Invoking through the `Invoke` pattern rather
  than clicking also removes the dependency on position.
- `HttpListener` **resolves a name while binding**, so a canary server built on
  it pollutes the very resolution measurement being taken. A `TcpListener` that
  writes one HTTP line by hand does not.
- Two suites that start **child test hosts** cannot run at once: each child is a
  full host, and the second one blows through timeouts that are generous for the
  first. The fix is a `DisableParallelization` collection, not a longer timeout.
- `File.Move` onto a destination that is a directory throws
  `UnauthorizedAccessException`, not `IOException`. The assertion has to name
  what actually happens.

Visual regression is pinned by a **versioned structural baseline** under
`tests/ApSolutions.LocalMedia.UiTests/Baselines/<task>/*.json`, not by images:
`artifacts/` is ignored, a binary PNG cannot be reviewed in a diff, and
headless rendering varies between machines. The baseline records the logical
viewport, the first focus, the focus order, the visibility of each surface, and
the edges that matter; the PNG is still captured under `artifacts/ui-captures/`
as visual proof. When a task changes the surface on purpose, the baseline is
regenerated, the diff reviewed, and re-approved **in the same commit** that
changes it: a baseline that does not follow the interface protects nothing.

## Run the x64 shell

The host creates no accounts and uses no remote services. Build and run it as
follows:

```powershell
dotnet build src/ApSolutions.LocalMedia.Windows -c Release --no-restore
./src/ApSolutions.LocalMedia.Windows/bin/Release/net10.0-windows10.0.22621.0/ApSolutions.LocalMedia.Windows.exe
```

The UI starts in Spanish with Home selected. All five destinations are
keyboard operable. Alternate English resources live in
`Resources/Strings.en.axaml`; every new visible string must be added to both
the Spanish and English dictionaries under the same key.

## Theme and local preferences

The `System`, `Light`, or `Dark` preference is written atomically to
`%LOCALAPPDATA%\APSolutions\LocalMedia\settings.json`. Colors, spacing, focus,
and motion live in `Theme/DesignTokens.axaml`; do not embed product colors in
views. The player must always request the dark variant, and every new animation
must consult `IReducedMotionService`. Mica is implemented only in the Windows
host and retains a solid fallback when unavailable.

## Local database and migrations

The database is created at
`%LOCALAPPDATA%\APSolutions\LocalMedia\library.db` — or wherever
`AP_LOCALMEDIA_DATA_ROOT` says, when it is set — with WAL, foreign keys, a
5-second busy timeout, and a startup integrity check. Migrations live under
`Infrastructure/Data/Migrations/`, are listed in `Manifest.json`, and must keep
the SHA-256 of the embedded SQL resource. Every pending version first creates a
valid SQLite copy and then runs in one transaction. Never edit a published
migration or add tables before their owning vertical task. On integrity or
migration failure, the app shows the preserved paths and never offers to
replace the pre-migration copy.

When adding a migration: take the **next free number** even when the plan cites
one that is already taken, and record that in the task's evidence. A temporary
gap in the numbering is valid: `MigrationRunner` only requires unique positive
versions, applies the missing ones in ascending order, and records them by
number, so an already-migrated database accepts an intermediate version later.
The manifest's `sha256` is computed over the file's UTF-8 text exactly as
`MigrationRunner` reads it; `.gitattributes` enforces `LF`, so the hash is only
stable across machines when the file is saved without a BOM and with `LF`.
`SqliteBootstrapTests` pins the migration count, the name list, the table list,
and the number of pre-migration copies: update it in the same commit.

**Isolating a run.** `AP_LOCALMEDIA_DATA_ROOT` names the folder the application
keeps everything in: database, settings, backups, artwork, and diagnostics. It is
read once at startup and a blank value is the same as not setting it. It exists so
a lifecycle check — install, launch, upgrade, uninstall — can run without touching
the profile folder of whoever is running it. `LOCALAPPDATA` does **not** work for
this: .NET resolves the folder through `SHGetFolderPath` and never reads that
variable, so redirecting it redirects nothing and the application writes to the
real folder anyway.

## Architecture

The dependency rule is `Presentation → Application → Domain ←
Infrastructure`. The Windows host composes all four projects. Domain uses no
packages; Application references neither Infrastructure, Avalonia, nor
Windows; and Presentation never references Infrastructure.

## Identity, privacy, and secrets

- Root namespace: `ApSolutions.LocalMedia`.
- Persistent package identity: `APSolutions.LocalMedia`.
- URI scheme: `apsolutions-localmedia`.
- Never add tokens, private paths, local databases, or user videos.
- The .NET CLI runs with telemetry disabled during local/CI verification.

## Contribution workflow

Every behavior follows RED→GREEN→refactor, preserves its TRX output, and
updates bilingual evidence. Run cross-cutting verification before the task
commit and do not mix work from the next increment.
