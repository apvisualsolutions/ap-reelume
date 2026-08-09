# T2 — Shell y localización / Shell and localization

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit de tarea / Task commit: `feat: add bilingual AP Reelume application shell`
- IDs: `PRD-001=IN_PROGRESS`, `UX-002=IN_PROGRESS`, `UX-004=IN_PROGRESS`, `REL-004=DESIGN_APPROVED`, `DOC-001=IN_PROGRESS`

Este informe conserva español e inglés en cada sección. La autorización formal
de marca de `REL-004` no forma parte de I0 y permanece sin verificar. / This
report keeps Spanish and English in every section. Formal `REL-004` trademark
clearance is outside I0 and remains unverified.

## Resultado RED / RED result

Las pruebas se escribieron antes del código de producción. Las ejecuciones
válidas de `artifacts/test-results/T2/red/` compilaron el arnés y fallaron por
el comportamiento ausente: / Tests were written before production code. The
valid runs under `artifacts/test-results/T2/red/` compiled the harness and
failed because the behavior was absent:

| Suite | RED esperado / Expected RED |
|---|---|
| `ShellLocalizationTests` | 0/5; faltaban `App`, shell, rutas y diccionarios / missing `App`, shell, routes, and dictionaries |
| `ShellAutomationTests` | 0/2; faltaban la aplicación y el árbol accesible / missing application and accessible tree |

Los códigos de salida fueron 1 por aserciones funcionales, no por errores del
SDK, restore o compilación. / Exit codes were 1 because functional assertions
failed, not because of SDK, restore, or compilation errors.

## Resultado GREEN / GREEN result

| Verificación / Check | Resultado / Result |
|---|---|
| UI headless del shell / headless shell UI | PASS, 5/5 |
| Accesibilidad del shell / shell accessibility | PASS, 2/2 |
| Build Release x64 `-warnaserror` | PASS, 0 warnings, 0 errors |
| Arranque controlado del `.exe` durante 5 s / controlled `.exe` startup for 5 s | PASS, proceso x64 estable / stable x64 process |
| `Text="[^{]` en XAML visible / visible XAML | PASS, 0 coincidencias / matches |
| `Content|Header|Title="[^{]` en XAML visible / visible XAML | PASS, 0 coincidencias / matches |
| Paridad de claves ES/EN / ES/EN key parity | PASS, 13/13 |
| Dependencias de autenticación o HTTP / authentication or HTTP dependencies | PASS, 0 |

Los TRX RED/GREEN están en `artifacts/test-results/T2/`. El shell usa
`INavigationService.Navigate(AppRoute)` y expone exactamente `Home`, `Library`,
`Review`, `Backups` y `Settings`, con `Home` predeterminado. Cada botón tiene
nombre de automatización, rol nativo de botón, estado y activación mediante
teclado. / RED/GREEN TRX files are under `artifacts/test-results/T2/`. The shell
uses the navigation contract, exposes exactly the five approved routes, and
defaults to Home. Each destination has an automation name, native button role,
state, and keyboard activation.

## Marca y localización / Brand and localization

- `ProductDisplayName`: `AP Reelume`.
- `PublisherSignature`: `by AP Solutions`.
- La firma solo se consume dentro de `AboutBrandSurface`; no forma parte de
  namespaces, ensamblados ni IDs persistentes. / The signature is consumed only
  inside `AboutBrandSurface`; it is absent from namespaces, assemblies, and
  persistent IDs.
- Español es el idioma de inicio e inglés es la alternativa dinámica. / Spanish
  is the startup language and English is the dynamic alternate language.
- El host no contiene login, perfil, `HttpClient` ni servicio remoto. / The host
  contains no login, profile, `HttpClient`, or remote service.

## Capturas exactas / Exact captures

| Idioma / Language | Archivo / File | SHA-256 |
|---|---|---|
| Español (`es-ES`) | `artifacts/ui-captures/T2/shell-es-ES.png` | `407E5D481BAF0DA61700C46F153926FAFC56A5F225C0D1FB45BA7D92DEB09916` |
| English (`en-US`) | `artifacts/ui-captures/T2/shell-en-US.png` | `4CDD98E23C4693ACF071FC141DC42BF7F713492FE2F736E0F027338181F21DA7` |

Ambas capturas son PNG de 1024×720 generadas por el renderer headless real de
Avalonia y revisadas visualmente. / Both captures are 1024×720 PNGs generated
by Avalonia's real headless renderer and visually inspected.
