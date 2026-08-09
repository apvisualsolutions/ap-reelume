# ADR-0004 — Sellar el paquete con MakeAppx en lugar de un `.wapproj` / Seal the Package with MakeAppx Instead of a `.wapproj`

- Estado / Status: `ACCEPTED`
- Fecha / Date: 2026-08-03
- Decisor / Decision owner: Engineering, para revisión del Product Owner / for Product Owner review
- Relacionado / Related: [`FEATURES.md`](../FEATURES.md), [T40](../evidence/mvp/T40-x64-packaging.md),
  Tarea 40 del [plan](../superpowers/plans/2026-08-01-ap-reelume-windows-mvp-implementation.md)

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be updated together.

---

## Español

### Contexto

El plan de T40 nombra `src/ApSolutions.LocalMedia.Windows.Package/ApSolutions.LocalMedia.Windows.Package.wapproj`
como el proyecto que produce el MSIX. Un `.wapproj` se construye con `Microsoft.DesktopBridge.targets`,
que no viaja con el SDK de .NET sino con una carga de trabajo de Visual Studio.

Medido en este equipo:

| Herramienta | Presente |
|---|---|
| SDK de .NET 10.0.302 | sí |
| Visual Studio Build Tools 2019 y 2026 | sí |
| `Microsoft.DesktopBridge.targets` en cualquiera de los dos | **no** |
| `MakeAppx.exe` (SDK de Windows 10.0.26100.0) | sí |

Añadir un `.wapproj` que este repositorio no puede construir dejaría el artefacto sin ninguna prueba
que lo examine: un fichero que describe un paquete que nadie produce. La regla del proyecto es la
contraria —la puerta es una prueba, no una lista— y una superficie que no se puede ejercitar no está
entregada, exactamente como decidió [ADR-0003](0003-assemble-the-application-before-packaging-it.md).

### Decisión

**El diseño del paquete se versiona; el ensamblado lo hace un script.**

- `src/ApSolutions.LocalMedia.Windows.Package/` guarda lo que define el paquete: `Package.appxmanifest`
  y las cinco imágenes de mosaico. No hay fichero de proyecto.
- `eng/package-x64.ps1` publica, monta el layout, lo sella con `MakeAppx.exe` y escribe el ZIP
  independiente, los hashes, el SBOM y un inventario del artefacto.
- `eng/verify-package.ps1` recorre el ciclo de vida sobre el paquete y compara dos compilaciones.
- Las cuatro suites de `tests/ApSolutions.LocalMedia.PackagingTests` leen el manifiesto versionado, el
  artefacto construido y los informes, y fallan si alguno falta.

### Consecuencias

- El paquete se construye con el SDK de .NET y el SDK de Windows, sin Visual Studio. CI no necesita
  una carga de trabajo adicional.
- El manifiesto es XML válido y completo, sin sustitución de plantillas: una prueba lo lee tal cual y
  compara su versión con `Directory.Build.props`.
- Se pierde la integración de Visual Studio para depurar la aplicación empaquetada. No se usa: la
  verificación física recorre el ejecutable publicado.
- Si algún día hace falta el `.wapproj` —por ejemplo para enviar a la Store en S4— este ADR se
  reemplaza, y el manifiesto ya está donde ese proyecto lo esperaría.
- La firma sigue fuera: `MakeAppx` sella pero no firma, y no hay certificado. Eso no cambia con esta
  decisión y se documenta en [SMARTSCREEN.es.md](../release/SMARTSCREEN.es.md).

### Alternativas descartadas

- **Instalar la carga de trabajo de Visual Studio.** Ata la construcción a una instalación de varios
  gigabytes que el runner de CI no tiene y que nadie más necesita para trabajar en el repositorio.
- **Versionar el `.wapproj` sin poder construirlo.** Es la opción que ADR-0003 declaró inaceptable en
  otro contexto: entregar un fichero que nada ejercita.

---

## English

### Context

T40's plan names `src/ApSolutions.LocalMedia.Windows.Package/ApSolutions.LocalMedia.Windows.Package.wapproj`
as the project that produces the MSIX. A `.wapproj` is built by `Microsoft.DesktopBridge.targets`,
which ships with a Visual Studio workload rather than with the .NET SDK.

Measured on this hardware:

| Tool | Present |
|---|---|
| .NET SDK 10.0.302 | yes |
| Visual Studio Build Tools 2019 and 2026 | yes |
| `Microsoft.DesktopBridge.targets` in either | **no** |
| `MakeAppx.exe` (Windows SDK 10.0.26100.0) | yes |

Adding a `.wapproj` this repository cannot build would leave the artifact with no suite examining it:
a file describing a package nobody produces. The project's rule is the opposite one — the gate is a
test, not a checklist — and a surface that cannot be exercised is not delivered, exactly as
[ADR-0003](0003-assemble-the-application-before-packaging-it.md) decided.

### Decision

**The package design is versioned; a script assembles it.**

- `src/ApSolutions.LocalMedia.Windows.Package/` holds what defines the package: `Package.appxmanifest`
  and the five tile images. There is no project file.
- `eng/package-x64.ps1` publishes, assembles the layout, seals it with `MakeAppx.exe`, and writes the
  independent ZIP, the hashes, the SBOM, and an inventory of the artifact.
- `eng/verify-package.ps1` walks the lifecycle over the package and compares two builds.
- The four suites in `tests/ApSolutions.LocalMedia.PackagingTests` read the versioned manifest, the
  built artifact, and the reports, and fail when any of them is missing.

### Consequences

- The package builds with the .NET SDK and the Windows SDK, without Visual Studio. CI needs no extra
  workload.
- The manifest is complete, valid XML with no template substitution: a test reads it as it stands and
  compares its version with `Directory.Build.props`.
- Visual Studio's packaged-application debugging is lost. It is not used: physical verification walks
  the published executable.
- If the `.wapproj` is ever needed — for the Store submission in S4, say — this ADR is superseded, and
  the manifest is already where that project would expect it.
- Signing stays out of scope: `MakeAppx` seals but does not sign, and there is no certificate. That is
  unchanged by this decision and documented in [SMARTSCREEN.en.md](../release/SMARTSCREEN.en.md).

### Alternatives rejected

- **Install the Visual Studio workload.** It ties the build to a multi-gigabyte installation the CI
  runner does not have and nobody else needs to work in the repository.
- **Version the `.wapproj` without being able to build it.** That is the option ADR-0003 declared
  unacceptable in another context: shipping a file nothing exercises.
