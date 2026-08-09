# T1 — Fundación compilable / Buildable foundation

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit de tarea / Task commit: `build: establish local media architecture and verification`
- IDs: `PRD-004=VERIFIED`, `PRD-005=IN_PROGRESS`, `DOC-001=IN_PROGRESS`

Este informe es bilingüe: cada sección presenta primero español y después
inglés. / This report is bilingual: every section presents Spanish first and
English second.

## Resultado RED / RED result

Las pruebas se escribieron antes de la configuración de producción. La
ejecución válida quedó en `artifacts/test-results/T1/red/`:

- `ArchitectureDependencyTests` y `StableInternalIdentityTests`: 0/5 pasan;
  fallan porque faltan `src/` y `Directory.Build.props`.
- `BilingualDocumentationPairTests` y `PinnedDependencyTests`: 1/5 pasa y
  4/5 fallan; faltan `global.json`, versiones centrales, lockfiles y guías.
- Ambos procesos devuelven código 1 por aserciones del comportamiento ausente,
  no por SDK, restore o errores de compilación del arnés.

The tests were written before production configuration. The valid run is under
`artifacts/test-results/T1/red/`:

- `ArchitectureDependencyTests` and `StableInternalIdentityTests`: 0/5 pass;
  missing `src/` and `Directory.Build.props` cause the expected assertions.
- `BilingualDocumentationPairTests` and `PinnedDependencyTests`: 1/5 passes and
  4/5 fail; `global.json`, central versions, lock files, and guides are absent.
- Both processes exit 1 because behavior is missing, not because the SDK,
  restore, or test harness compilation is broken.

## Resultado GREEN / GREEN result

| Verificación / Check | Resultado / Result |
|---|---|
| Restore normal bloqueado / locked default restore | PASS, 16 proyectos / projects |
| Restore temprano `win-arm64` sin reescribir lockfiles / early ARM64 restore without rewriting lock files | PASS; comprobación de dependencia, no entregable / dependency check, not a deliverable |
| Build Debug `-warnaserror` | PASS, 0 warnings, 0 errors |
| Arquitectura / Architecture | PASS, 5/5 |
| Documentación / Documentation | PASS, 5/5 |
| `eng/verify-docs.ps1` | PASS, 53 IDs y / and 46 MVP IDs |
| Paquetes vulnerables / Vulnerable packages | PASS, 0 en 16 proyectos / in 16 projects |
| Paquetes obsoletos / Deprecated packages | PASS, 0 en 16 proyectos / in 16 projects |

Los TRX y Cobertura GREEN están en `artifacts/test-results/T1/green/`. No existe
código funcional de producción en T1, por lo que la cobertura de código nuevo
no aplica; las reglas de arquitectura/configuración añadidas quedan ejercidas
por diez pruebas.

GREEN TRX and Cobertura files are under `artifacts/test-results/T1/green/`.
T1 introduces no functional production code, so new-code coverage is not
applicable; ten tests exercise the added architecture/configuration rules.

## Grafo de proyectos / Project graph

```text
Presentation ──> Application ──> Domain <── Infrastructure
      │               │             ^              │
      └────────────── Windows host composition ────┘
```

- Domain: BCL únicamente / BCL only.
- Application: referencia solo Domain / references Domain only.
- Infrastructure: referencia Application y Domain / references Application and Domain.
- Presentation: referencia Application y Avalonia / references Application and Avalonia.
- Windows: compone los cuatro proyectos / composes all four projects.

Las reglas se ejecutan en
[`ArchitectureDependencyTests`](../../../tests/ApSolutions.LocalMedia.ArchitectureTests/ArchitectureDependencyTests.cs)
y los IDs estables en
[`StableInternalIdentityTests`](../../../tests/ApSolutions.LocalMedia.ArchitectureTests/StableInternalIdentityTests.cs).
/ The dependency rules and stable IDs are enforced by those tests.

## Versiones y lockfiles / Versions and lock files

| Componente / Component | Versión exacta / Exact version |
|---|---:|
| .NET SDK | `10.0.302` |
| Avalonia | `12.1.1` |
| LibVLCSharp | `3.10.0` |
| VideoLAN.LibVLC.Windows | `3.0.23.1` |
| Microsoft.Data.Sqlite | `10.0.10` |
| SQLitePCLRaw.lib.e_sqlite3 | `3.53.3` |
| xUnit v3 | `3.2.2` |
| Microsoft.NET.Test.Sdk | `18.8.1` |

La revisión transitiva SQLitePCLRaw `2.1.11` fue rechazada por el análisis
NU1903 (`GHSA-2m69-gcr7-jv3q`) y se fijó `3.53.3`, que incorpora SQLite
posterior a 3.50.2. Los 16 proyectos tienen `packages.lock.json`; no existen
rangos flotantes. / The transitive SQLitePCLRaw `2.1.11` restore was rejected by
NU1903 and pinned to `3.53.3`. All 16 projects have lock files and no floating
ranges exist.

## Licencias iniciales / Initial licenses

| Paquete / Package | Declaración NuGet / NuGet declaration |
|---|---|
| Avalonia | MIT |
| LibVLCSharp | LGPL-2.1-or-later |
| VideoLAN.LibVLC.Windows | LGPL-2.1-or-later |
| Microsoft.Data.Sqlite | MIT |
| SQLitePCLRaw.lib.e_sqlite3 | `LICENSE.txt` incluido / included |
| xUnit v3 | Apache-2.0 |
| Microsoft.NET.Test.Sdk | MIT |

El repositorio incluye el texto completo `GPL-3.0-or-later` en `LICENSE` y lo
declara en `NOTICE`. Esta es la
revisión inicial; los avisos completos, la auditoría del artefacto exacto y el
SBOM mantienen `PRD-005` en `IN_PROGRESS`. / The repository declares
`GPL-3.0-or-later` with its complete license text. Full notices,
exact-artifact audit, and SBOM keep `PRD-005` in progress.

## Entorno y reproducibilidad / Environment and reproducibility

- Windows 11 Pro `10.0.26200` x64.
- Intel Core i7-14700K.
- PowerShell `7.6.4`.
- .NET SDK `10.0.302` instalado localmente desde el ZIP oficial, SHA-512
  `7d170ed75fa9af34c00646621d92011dbd71943952e2787cd15df9be78e6452b55dadef34d7eff77b802e6af4959e071a55855ac649afeac70901c3a2a258716`.

La automatización reproducible está en
[`ci.yml`](../../../.github/workflows/ci.yml),
[`verify.ps1`](../../../eng/verify.ps1) y
[`verify-docs.ps1`](../../../eng/verify-docs.ps1). La ejecución local demuestra
el pipeline; la retención CI remota se confirmará cuando el workflow se ejecute
en el servidor. / Local execution proves the pipeline behavior; remote CI
retention will be confirmed when the workflow runs on the server.
