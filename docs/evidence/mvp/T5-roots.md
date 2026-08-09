# T5 — Gestión segura de raíces / Safe root management

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit de tarea / Task commit: `feat: manage local USB and UNC library roots`
- IDs: `LIB-001=VERIFIED`, `LIB-010=IN_PROGRESS`, `PRD-001=IN_PROGRESS`

Este informe conserva español e inglés en cada sección. `LIB-001` queda
verificado porque los tres tipos de raíz se validan de forma independiente y
añadir/quitar no modifica multimedia. La recuperación completa tras desconexión
de `LIB-010` continúa en T8–T9. / This report keeps Spanish and English in
every section. `LIB-001` is verified because all three root kinds are validated
independently and add/remove never mutates media. Full `LIB-010` disconnect
recovery continues in T8–T9.

## Resultado RED / RED result

Las pruebas se escribieron antes de entidades, comandos, adaptadores, esquema o
onboarding. Los TRX válidos están en `artifacts/test-results/T5/red/`: / Tests
were written before entities, commands, adapters, schema, or onboarding. Valid
TRX files are under `artifacts/test-results/T5/red/`:

| Suite | RED esperado / Expected RED |
|---|---|
| `LibraryRootTests` | 0/3; faltaban IDs, `LibraryRoot` y estados / IDs, root entity, and states were absent |
| `RootLifecycleTests` | 0/5; faltaban repositorio, normalizador, casos de uso y onboarding / repository, normalizer, use cases, and onboarding were absent |

Ambos procesos devolvieron 1 por aserciones funcionales con el arnés compilado,
no por SDK, restore ni errores de compilación. / Both processes returned 1 from
functional assertions with a compiled harness, not from SDK, restore, or build
errors.

## Resultado GREEN / GREEN result

| Verificación / Check | Resultado / Result |
|---|---|
| Dominio T5 / T5 domain | PASS, 3/3 |
| Integración T5 / T5 integration | PASS, 5/5 |
| SQLite T4+T5 transversal / cross-check | PASS, 12/12 |
| Suite completa Release / full Release suite | PASS, 46/46 |
| Build Release `-warnaserror` | PASS, 0 warnings, 0 errors |
| Arquitectura, UI, accesibilidad y documentación / architecture, UI, accessibility, docs | PASS |
| Paquetes vulnerables altos/críticos / high-critical vulnerable packages | 0 en / across 16 projects |
| Paquetes obsoletos / deprecated packages | 0 en / across 16 projects |
| Asignaciones de secretos sospechosas / suspicious secret assignments | 0 |

Los resultados GREEN y cobertura están en
`artifacts/test-results/T5/green/`. La unión por archivo/línea de las suites T5
cubre 177/205 líneas de código nuevo (86,34 %); las ramas ejercidas de política
de dominio quedan al 100 %. / GREEN results and coverage are under
`artifacts/test-results/T5/green/`. The file/line union of the T5 suites covers
177/205 new-code lines (86.34%); exercised domain-policy branches are 100%.

## Matriz de raíces y errores / Root and error matrix

| Caso / Case | Resultado / Result |
|---|---|
| Local temporal / temporary local | Normalizada, legible y persistida / normalized, readable, persisted |
| USB simulada con `subst R:` / simulated USB using `subst R:` | Normalizada como `RootKind.Usb` y persistida / normalized as USB and persisted |
| UNC real local `\\localhost\C$\...` / real local UNC | Normalizada como `RootKind.Unc` y persistida / normalized as UNC and persisted |
| Duplicada por mayúsculas/separadores / duplicate by case/separators | Rechazada con error `Duplicate` / rejected |
| Contenida en otra raíz / nested root | Rechazada con error `Nested` / rejected |
| Acceso denegado inyectado / injected access denial | `AccessDenied`, tipo y ruta accionables; otra raíz continúa / actionable error; independent root continues |

La demo del adaptador real persistió 3/3 raíces y terminó con 0 filas tras
quitarlas. La migración `0002_library_roots.sql` usa SHA-256
`581BADF374206CB37F744914854E6EAC307BF3403C3539FB89E639CB441642DC`.
/ The real-adapter demo persisted all 3 roots and ended with zero rows after
removal. The migration uses the SHA-256 shown above.

## Prueba de no copia ni modificación / No-copy and no-mutation proof

La prueba automatizada crea tres vídeos pequeños, calcula un inventario SHA-256,
añade y quita la raíz y vuelve a calcularlo. La demo local/USB/UNC repite la
comparación sobre tres ubicaciones. Ambos resultados son idénticos: mismo nombre,
tamaño, contenido y conteo; no aparece archivo copiado y el borrado sólo afecta
la fila `library_roots`. / The automated test creates three small videos,
calculates a SHA-256 inventory, adds/removes the root, and recalculates it. The
local/USB/UNC demo repeats the comparison across three locations. Both
inventories are identical in name, size, content, and count; no copy appears,
and removal affects only the `library_roots` row.

## Consentimiento y persistencia / Consent and persistence

`RootOnboardingViewModel` guarda la raíz pero mantiene
`CanStartInitialScan=false` hasta una confirmación explícita. El repositorio usa
la ruta persistente SQLite, comparación de rutas sin distinguir mayúsculas y no
contiene ninguna API de copia, movimiento o borrado de archivos. / The onboarding
view model saves the root while keeping `CanStartInitialScan=false` until
explicit confirmation. The repository uses persistent SQLite storage,
case-insensitive path comparison, and exposes no file copy, move, or delete API.
