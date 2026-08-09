# T8 — Identidad, movimiento y disponibilidad / Identity, movement, and availability

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit de tarea / Task commit: `feat: reconcile moved and unavailable media safely`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302, SQLite WAL/FTS5, NTFS local
- IDs: `LIB-009=VERIFIED`, `LIB-010=VERIFIED`, `PLY-010=IN_PROGRESS`

Este informe es bilingüe en cada sección. T8 entrega identidad estable NTFS,
firma ligera v1, reconciliación exacta/probable, confirmación manual y estado
no disponible. La transferencia proporcional de progreso entre versiones no
pertenece a T8 y mantiene `PLY-010` en progreso. / This report is bilingual in
every section. T8 delivers stable NTFS identity, lightweight fingerprint v1,
exact/probable reconciliation, manual confirmation, and unavailable state.
Proportional progress transfer across versions is outside T8, so `PLY-010`
remains in progress.

## Resultado RED / RED result

Las pruebas se escribieron antes del comportamiento y sus TRX se conservan en
`artifacts/test-results/T8/red/`. / Tests preceded the behavior and their TRX
files are retained under the path above.

| Suite | RED demostrado / Proven RED |
|---|---|
| Contrato de dominio / domain contract | 0/1: faltaban identidad, proveedor, política y decisiones / identity, provider, policy, and decisions were absent |
| Contrato integrado / integration contract | 0/1: faltaban proveedores, caso de uso, UI y tabla / providers, use case, UI, and table were absent |
| Política / policy | 0/4: `NotSupportedException` antes de implementar decisiones / before decision behavior existed |
| Movimiento y pérdida de dispositivo / move and device loss | 0/4: identidad NTFS, huella y persistencia eran stubs / NTFS identity, fingerprinting, and persistence were stubs |

Todos fueron fallos funcionales por comportamiento ausente, con SDK y arnés
operativos. / All were functional failures caused by missing behavior with a
working SDK and harness.

## Resultado GREEN / GREEN result

| Verificación / Check | Resultado / Result |
|---|---|
| `FileReconciliationTests` | PASS, 9/9 |
| `MoveAndDeviceLossTests` | PASS, 10/10 |
| Suite Release completa / full Release suite | PASS, 90/90 |
| Cobertura focal de líneas / focused line coverage | 415/500, 83,0 % |
| Ramas de `FileReconciliationPolicy` / policy branches | 22/22, 100 % |
| Build, analizadores, formato, arquitectura, localización y documentación / build, analyzers, format, architecture, localization, and docs | PASS |

Los resultados GREEN y Cobertura están en
`artifacts/test-results/T8/green/` y `artifacts/test-results/T8/coverage/`.
La verificación transversal final de la tarea se conserva en
`artifacts/test-results/verify-win-x64/`. / GREEN and coverage results are
under the paths above; final cross-cutting verification is retained under the
verification path.

## Identidad y atomicidad / Identity and atomicity

- En NTFS, `GetFileInformationByHandle` recibe un `SafeFileHandle` abierto en
  sólo lectura y devuelve número de volumen + índice de archivo; mover el
  archivo temporal conserva ambos valores. / On NTFS, the safe-handle call is
  read-only and returns volume serial + file index; moving the temporary file
  preserves both values.
- La firma `sha256:v1` incorpora tamaño, duración, contenedor, códecs,
  resolución y hasta 64 KiB de inicio, centro y final. El contador automatizado
  nunca supera 196.608 bytes para el archivo de 1 MiB. / The fingerprint hashes
  the approved technical fields and at most three 64 KiB samples; its automated
  counter never exceeds 196,608 bytes for the 1 MiB fixture.
- Una huella única reasigna automáticamente; dos candidatos producen
  `Probable`, conservan la ruta anterior y exigen confirmación. La confirmación
  sólo acepta un candidato del conjunto y actualiza ruta, disponibilidad e
  identidad en una transacción SQLite. / A unique fingerprint reassigns
  automatically; two candidates yield `Probable`, retain the old path, and
  require confirmation. Confirmation accepts only a matching candidate and
  updates path, availability, and identity in one SQLite transaction.
- La entidad conserva el mismo `MediaFileId`, que es la referencia estable para
  progreso presente y futuro. El caso de colisión mantiene dos filas antes y
  después de confirmar: cero fusiones o duplicados accidentales. / The entity
  keeps the same `MediaFileId`, the stable reference for current and future
  progress. The collision fixture retains two rows before and after confirmation:
  zero accidental merges or duplicates.

La migración `0005_file_identity.sql` usa SHA-256
`E735E8464EBDA99E9C42C9EFE68D81F32B39CD1FBDD600007700DCF7B59D8F01` y
añade una unicidad parcial para identidad estable más un índice de huella. /
The migration uses the SHA-256 above and adds a partial stable-identity unique
constraint plus a fingerprint index.

## Matriz local, USB y UNC / Local, USB, and UNC matrix

| Origen / Source | Estímulo / Stimulus | Identidad / Identity | Resultado / Result | Duplicados / Duplicates |
|---|---|---|---|---:|
| Local NTFS real temporal / real temporary local NTFS | renombrar `before.mkv` a `after.mkv` / rename | volumen + ID de archivo / volume + file ID | exacto, mismos IDs / exact, same IDs | 0 |
| Local lógico / logical local | marcar ausente y reaparecer en otra ruta / mark unavailable and return at another path | firma v1 / v1 fingerprint | misma entidad disponible / same available entity | 0 |
| USB simulada / simulated USB | `R:` desconectada y reconectada como `T:` / disconnected and reconnected | firma v1 / v1 fingerprint | una fila, nueva ruta / one row, new path | 0 |
| UNC simulada / simulated UNC | `\\nas-old\Media` reaparece como `\\nas-new\Media` / returns at new share path | firma v1 / v1 fingerprint | catálogo conservado y recuperado / catalog retained and recovered | 0 |

La pérdida durante enumeración se representa mediante
`MarkRootUnavailableAsync`: todas las filas de la raíz se conservan y pasan a
no disponibles. La pérdida entre sonda y commit no publica una ruta probable:
la ruta antigua permanece hasta la transacción confirmada. La reconexión
revalida la misma entidad y vuelve a `IsAvailable=true`. / Loss during
enumeration is represented by `MarkRootUnavailableAsync`: every root row is
retained and marked unavailable. Loss between probe and commit never publishes
a probable path: the old path remains until the confirmed transaction. Recovery
revalidates the same entity and restores `IsAvailable=true`.

## Seguridad de archivos y red / File and network safety

Los adaptadores de identidad abren multimedia exclusivamente con
`FileAccess.Read` y `FileShare.ReadWrite | FileShare.Delete`; no existe llamada
de copia, movimiento, borrado ni escritura en producción T8. La única mutación
de bytes ocurre dentro del propio test sobre una muestra generada, para demostrar
que cambia la huella central. T8 no contiene cliente HTTP ni realiza tráfico de
red; las rutas UNC de la matriz son identificadores simulados y SQLite sólo
almacena sus rutas. / Identity adapters open media read-only; production T8 has
no copy, move, delete, or write call. The sole byte mutation is performed by the
test harness on a generated fixture to prove the middle sample changes. T8 has
no HTTP client or network traffic; UNC matrix paths are simulated identifiers
stored only in SQLite.
