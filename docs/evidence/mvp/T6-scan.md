# T6 — Escaneo cancelable e incremental / Cancelable incremental scan

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit de tarea / Task commit: `feat: scan media incrementally with cancellation`
- IDs: `LIB-002=IMPLEMENTED`, `LIB-004=IN_PROGRESS`

Este informe conserva español e inglés en cada sección. `LIB-002` queda
implementado, no verificado: T6 entrega escaneo inicial/manual/incremental,
cancelación y reanudación; T9 debe cerrar vigilancia/recuperación y T10 el
presupuesto de UI. `LIB-004` permanece en progreso hasta catálogo, búsqueda y
presupuestos completos de T7/T10. / This report keeps Spanish and English in
every section. `LIB-002` is implemented, not verified: T6 delivers
initial/manual/incremental scanning, cancellation, and resume; T9 must close
watch/recovery and T10 the UI budget. `LIB-004` remains in progress until the
T7/T10 catalog, search, and full budgets are complete.

## Resultado RED / RED result

Las pruebas se escribieron antes del comportamiento propietario. Los TRX están
en `artifacts/test-results/T6/red/`: / Tests were written before their owning
behavior. TRX files are under `artifacts/test-results/T6/red/`:

| Prueba / Test | RED demostrado / Proven RED |
|---|---|
| Contratos de aplicación e infraestructura / application and infrastructure contracts | faltaban tipos y esquema; 0/3 / missing types and schema; 0/3 |
| `ScanCoordinatorTests` | 4/4 fallaron con `NotSupportedException` del coordinador / all 4 failed on the coordinator stub |
| Escritura por lotes / batched writes | se esperaban 8 lotes y se observaron 0 / expected 8 batches, observed 0 |
| Conteo de sonda fallida / failed-probe count | se esperaban 2 intentos y se registró 1 / expected 2 attempts, observed 1 |
| `IncrementalScanTests` | 2/3 fallaron porque el coordinador no existía funcionalmente / 2/3 failed because coordinator behavior was absent |
| Progreso de UI / UI progress | faltaba `Library.ScanProgressViewModel` / view model was absent |
| Sonda real / real probe | se esperaba `InvalidDataException`, pero el stub devolvió `NotSupportedException` / expected real invalid-media rejection, got the stub exception |

Todos los RED fueron fallos funcionales con proyectos compilables; no fueron
fallos de SDK, restore ni arnés. / Every RED was a functional failure in a
compilable harness, not an SDK, restore, or harness failure.

## Resultado GREEN / GREEN result

| Verificación / Check | Resultado / Result |
|---|---|
| Coordinador T6 / T6 coordinator | PASS, 5/5 |
| Integración T6 / T6 integration | PASS, 6/6 |
| Suite Release completa / full Release suite | PASS, 57/57 |
| Build Release `-warnaserror` | PASS, 0 warnings, 0 errors |
| Arquitectura, UI, accesibilidad y documentación / architecture, UI, accessibility, docs | PASS |
| Migraciones e integridad T4–T6 / T4–T6 migrations and integrity | PASS, esquema / schema v3 |

Los resultados están en `artifacts/test-results/T6/green/` y la verificación
transversal en `artifacts/test-results/verify-win-x64/`. / Results are under
the paths shown above.

La unión por archivo/línea de las coberturas focales T6 cubre 431/500 líneas
instrumentables nuevas (86,20 %). T6 no introduce una política de dominio con
ramificaciones: sus modelos de dominio son IDs y registros de datos, por lo que
el umbral de ramas de política no aplica en esta tarea. / The per-file/line
union of focused T6 coverage covers 431/500 new instrumentable lines (86.20%).
T6 adds no branching domain policy—its domain additions are IDs and data
records—so the domain-policy branch threshold is not applicable to this task.

## Casos y conteos / Cases and counts

| Caso / Case | Resultado / Result |
|---|---|
| 1 archivo / 1 file | dos solicitudes simultáneas conservan concurrencia máxima 1 por raíz / two simultaneous requests preserve maximum concurrency 1 per root |
| 1.000 archivos falsos / 1,000 fake files | 1.000 enumerados, 1.000 sondeos, 8 transacciones de hasta 128 / 1,000 enumerated, 1,000 probes, 8 transactions of at most 128 |
| Error de archivo / file error | `AccessDenied` queda como resultado fallido aislado y los otros 3 elementos continúan / isolated failed result while the other 3 items continue |
| Cancelación / cancellation | cancelado tras 5/10, checkpoint en el quinto; reanudación procesa los 5 restantes y limpia el checkpoint / canceled after 5/10, checkpoint at item five; resume processes the remaining 5 and clears it |
| Segundo pase / second pass | 8 extensiones admitidas, 8 sin cambios, `ProbeCount=0` / 8 allowed extensions, 8 unchanged, zero probes |
| Corpus real de rutas / real path corpus | 10.000 archivos `.mkv`, lote 128, 10.000 filas persistidas; suite focal completa en 4 s / 10,000 files, batch 128, 10,000 persisted rows; full focused suite in 4 s |
| Sonda LibVLC / LibVLC probe | análisis local real; un contenedor inválido se rechaza como `InvalidDataException`, no mediante stub / real local parse; invalid container is rejected as invalid data rather than by a stub |

El máximo de despacho tipado observado por la prueba queda por debajo del
límite de 50 ms y `StartAsync` devuelve una tarea incompleta cuando la primera
enumeración está bloqueada, demostrando que no bloquea sincrónicamente al
llamador de UI. / Typed event dispatch remains below the test's 50 ms bound,
and `StartAsync` returns an incomplete task while first enumeration is gated,
proving that it does not synchronously block the UI caller.

## Persistencia, extensiones y seguridad de archivos / Persistence, extensions, and file safety

La migración `0003_media_files_scans.sql` usa SHA-256
`85693F026A7CCA110AFBEA003227AC40B0571EE11635844BAC9E8F1620CEC8FC`.
El enumerador admite exclusivamente `.mp4`, `.mkv`, `.avi`, `.mov`, `.webm`,
`.m4v`, `.ts` y `.m2ts`; `.txt` queda excluido. La comparación automatizada
SHA-256 antes/después del doble escaneo es idéntica para todos los archivos:
el pipeline sólo lee metadatos y escribe SQLite, nunca copia, mueve, renombra,
trunca ni modifica multimedia. / The migration uses the SHA-256 above. The
enumerator allows only the eight stated extensions and excludes `.txt`. The
automated before/after SHA-256 inventory is identical after two scans: the
pipeline only reads metadata and writes SQLite; it never copies, moves,
renames, truncates, or modifies media.

La sonda LibVLC se inicializa con acceso de metadatos de red desactivado y sólo
usa `ParseLocal`; las comprobaciones de tráfico de proceso se repiten en C2. /
The LibVLC probe disables network metadata access and only uses `ParseLocal`;
process-level traffic checks are repeated at C2.

## Suplemento C2 / C2 supplement

La biblioteca real reveló un fallo nativo `0xC0000005` al liberar
inmediatamente sesiones LibVLC tras MKV consecutivos. La corrección guiada por
la regresión serializa la sonda y difiere un segundo la liberación nativa en
segundo plano. Completó lotes reales de diez archivos y de la biblioteca completa, incluido el vaciado
final, sin bloquear el pipeline. La pasada sobre la biblioteca completa tardó 27.998,4153 ms,
tuvo cero errores y la reanudación informó todos los archivos sin cambios y `ProbeCount=0`. /
The real library exposed a native `0xC0000005` when LibVLC sessions were
released immediately after consecutive MKVs. The regression-driven fix
serializes probing and defers native release for one second in the background.
It completed real ten-file and whole-library batches, including final draining, without
blocking the pipeline. The whole-library pass took 27,998.4153 ms with zero errors;
resume reported every item unchanged and zero probes. Full evidence is in
[`C2-library-gate.md`](C2-library-gate.md).
