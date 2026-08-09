# T4 — SQLite, migraciones e integridad / SQLite, migrations, and integrity

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit de tarea / Task commit: `feat: persist local data with WAL migrations`
- IDs: `PRD-001=IN_PROGRESS`, `DAT-001=IMPLEMENTED`, `PRD-004=VERIFIED`

Este informe es bilingüe. `DAT-001` queda implementado, no verificado: la
restauración completa y la última copia válida se certifican en I6. / This
report is bilingual. `DAT-001` is implemented, not verified: full restore and
last-valid-copy guarantees are certified in I6.

## Resultado RED / RED result

Las pruebas se escribieron antes de contratos, adaptadores, esquema o vista. Las
ejecuciones válidas están en `artifacts/test-results/T4/red/`: / Tests were
written before contracts, adapters, schema, or view. Valid runs are under
`artifacts/test-results/T4/red/`:

| Suite | RED esperado / Expected RED |
|---|---|
| `SqliteBootstrapTests|MigrationFailureTests` | 1/6; sólo pasa el fixture hijo inerte, 5 fallan por factory/runner ausentes / only the inert child fixture passes; 5 fail on missing factory/runner |
| `SqliteIsolationTests` inicial / initial | 1/3; faltan cuatro contratos y `AppDataPaths` / four contracts and `AppDataPaths` absent |
| `DatabaseRecoveryViewTests` | 0/1; falta la vista segura / safe view absent |
| Regresión de composición DI / DI composition regression | 0/1; el constructor del manifiesto no estaba fijado explícitamente / manifest constructor was not selected explicitly |

Los códigos 1 proceden de aserciones del comportamiento ausente. / Exit code 1
comes from assertions about absent behavior.

## Resultado GREEN / GREEN result

| Verificación / Check | Resultado / Result |
|---|---|
| Integración SQLite Release / SQLite integration Release | PASS, 7/7 |
| Aislamiento arquitectónico Release / architecture isolation Release | PASS, 4/4 |
| Recuperación UI Release / recovery UI Release | PASS, 1/1 |
| Build Release `-warnaserror` | PASS, 0 warnings, 0 errors |
| Base real temporal / real temporary database | PASS, `integrity_check=ok` |
| Proceso hijo terminado por fuerza / force-terminated child process | PASS; transacción confirmada conservada / committed transaction preserved |
| Ruta real de aplicación / real app path | PASS, `%LOCALAPPDATA%\APSolutions\LocalMedia\library.db` |
| Arranque x64 corregido / corrected x64 startup | PASS, 5 s estable e idempotente / stable and idempotent for 5 s |

La regresión de composición detectó que DI podía elegir el constructor público
de migraciones inyectadas con una lista vacía. El host ahora registra
explícitamente el constructor que carga el manifiesto; una prueba bloquea la
regresión. / The composition regression found that DI could select the public
injected-migration constructor with an empty list. The host now explicitly
registers the manifest-loading constructor, with a test preventing recurrence.

## Configuración y esquema / Configuration and schema

Cada conexión creada por `SqliteConnectionFactory` aplica: / Every factory
connection applies:

```text
journal_mode = wal
foreign_keys = 1
busy_timeout >= 5000 ms
synchronous = FULL
```

La migración inicial crea únicamente: / The initial migration creates only:

```sql
CREATE TABLE schema_history (
    version INTEGER NOT NULL PRIMARY KEY,
    name TEXT NOT NULL,
    applied_utc TEXT NOT NULL,
    checksum TEXT NOT NULL
) STRICT;
```

- SQL SHA-256: `289957B02996FAEB2326A72A96C3D10F33236FAC569E9071262253F860F03D42`.
- Manifest SHA-256: `062A75D53C7A03F1C2B0A1B16CE88749706D226104BC075847943092647877B8`.
- Segunda ejecución / second execution: `schema_history=1`, cero copias nuevas / zero new copies.
- No existen tablas funcionales de I1. / No I1 functional tables exist.

## Migración fallida / Failed migration

La prueba inyecta V2 con DDL válido seguido de SQL inválido. Antes de comenzar
se crea una copia mediante la API de backup SQLite. Tras el fallo: / The test
injects V2 with valid DDL followed by invalid SQL. A SQLite API backup is made
first. After failure:

| Comprobación / Check | Activa / Active | Copia / Backup |
|---|---:|---:|
| `integrity_check` | `ok` | `ok` |
| Centinela confirmado / committed sentinel | conservado / preserved | conservado / preserved |
| `schema_history` | 1 | 1 |
| Tabla parcial V2 / partial V2 table | ausente / absent | ausente / absent |

## Recuperación segura / Safe recovery

Una base corrupta devuelve `IsValid=false` sin modificar sus bytes. El host
muestra `DatabaseRecoveryView` con la ruta activa, la copia previa y únicamente
las acciones `OpenBackupFolder` y `Exit`; `CanOverwriteBackup=false`. / A
corrupt database returns false without changing its bytes. The host presents
the active and backup paths with only the two safe actions.

La captura `artifacts/ui-captures/T4/database-recovery.png` mide 840×560 y tiene
SHA-256
`19165CB37F2AFB244101E861B8D54BD92D9286B79F0E459D7C95F5FDC2042141`.
/ The 840×560 recovery capture has the SHA-256 shown above and was visually
inspected.
