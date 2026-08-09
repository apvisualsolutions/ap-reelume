# T27 — Transferencia de progreso entre versiones / Progress transfer across versions

- Fecha / Date: 2026-08-02
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `78ec82b`
- Commit de tarea / Task commit: `feat: transfer progress safely between media versions`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1
- IDs: `PLY-010=VERIFIED`, `LIB-008=VERIFIED`, `LIB-009=VERIFIED`

## RED y GREEN / RED and GREEN

`ProgressTransferPolicyTests`, `SwitchMediaVersionTests` y `VersionSwitchDialogTests` se escribieron
antes que la política, el caso de uso y el diálogo. RED falló porque `ProgressTransferPolicy`,
`SwitchMediaVersion`, `VersionSwitchViewModel` y `VersionSwitchDialog` no existían; la salida está en
`artifacts/test-results/T27/red/`. / The three plan-named test files were written first and RED failed
on the missing policy, use case, and dialog.

GREEN ejecuta **540 pruebas con 0 fallos y 0 omitidas** en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T27/green/`. `dotnet format --verify-no-changes` no informa cambios y
ambas compilaciones terminan con 0 advertencias. / GREEN runs 540 tests with zero failures and zero
skips.

## La decisión, punto por punto / The decision, boundary by boundary

| Caso / Case | Decisión verificada / Verified decision |
|---|---|
| Tolerancia / Tolerance | el mayor de `5 s` y el `1 %`: `60 s` en una película de 100 min, `5 s` en un clip de 2 min |
| Duraciones iguales / Identical durations | `Exact`, el mismo segundo |
| Diferencia dentro de la tolerancia / Difference inside the tolerance | `Exact`, incluso con estructura incompatible: si no hay que escalar nada, no hay nada que confirmar |
| Par corto / Short pair | usa el suelo de `5 s` en lugar del `1 %`: `+4 s` es exacto y `+9 s` ya es proporcional |
| Diferencia del 2 %, 5 % y 10 % / 2%, 5%, and 10% difference | `Proportional`, con el segundo escalado por la razón de duraciones |
| Diferencia mayor del 10 % / Beyond 10% | `Confirm(LargeDifference)` con el segundo proporcional como sugerencia, sin aplicarlo |
| Estructura incompatible dentro del 10 % / Incompatible structure within 10% | `Confirm(IncompatibleStructure)` |
| Duración desconocida / Unknown duration | `Confirm(UnknownDuration)` en cualquiera de los dos lados, conservando el segundo actual como sugerencia |
| Progreso trivial / Trivial progress | `Restart`: por debajo del mínimo de reanudación no hay nada que trasladar |
| Segundo fuera del nuevo final / Second past the new end | siempre limitado a la duración de la versión destino |
| Duración cero / Zero length | tratada como desconocida en lugar de dividir por cero |

Ese último caso obligó a **endurecer `ProgressPolicy`**, escrita en T25: una duración de cero o negativa
ahora significa «no observada» en vez de «termina inmediatamente», tanto al limitar la posición como al
decidir si se ofrece reanudar. Sin esa corrección, una versión sin duración conocida hacía que la
transferencia dijera «empezar de nuevo» en lugar de preguntar. / The zero-length case forced the T25
policy to treat a non-positive duration as unobserved rather than as an immediate end.

## El cambio de versión / The switch itself

- **La sesión anterior se escribe antes de abrir nada.** El registro de la prueba lo comprueba por
  orden: la escritura ocurre antes que la apertura, no después.
- Una confirmación pendiente **no abre nada y no cambia nada**: ni el segundo almacenado ni la versión
  de origen se tocan mientras la pregunta sigue en pie.
- Confirmada, abre en el segundo sugerido y **registra la versión nueva como origen** del progreso,
  junto con su duración observada.
- Una versión que **se niega a abrir** deja el progreso, la duración y el origen exactamente como
  estaban: no se escribe nada nuevo porque la escritura ocurre después de que el motor acepte.
- Una versión marcada como no disponible se rechaza **antes** de pedirle nada al motor.
- Una decisión manual de estado sobrevive al cambio de versión: la posición se traslada y el estado
  marcado a mano se conserva.
- Sin progreso utilizable, la versión nueva simplemente empieza desde cero.

/ The previous session reaches storage before the engine is asked, a pending confirmation changes
nothing, a refused open leaves progress and audit untouched, and a manual state survives the move.

## El diálogo / The dialog

Sólo aparece cuando hay una confirmación pendiente; cada motivo muestra su propia frase; el segundo
propuesto se presenta en horas, minutos y segundos; los tres botones —continuar ahí, empezar de nuevo y
cancelar— informan su elección y cierran la pregunta; todos tienen nombre de automatización. Capturado
en español e inglés en `artifacts/ui-captures/T27/`. / The dialog appears only for a pending question,
states each reason in words, shows the suggested point, and offers three named choices in both
languages.

## Cobertura / Coverage

| Archivo / File | Líneas / Lines |
|---|---:|
| `Domain/Continuity/ProgressTransferPolicy.cs` | 31/31 — 100 % |
| `Application/Continuity/SwitchMediaVersion.cs` | 67/67 — 100 % |
| `Presentation/Player/VersionSwitchViewModel.cs` | 44/45 — 97,78 % |
| **Total del código nuevo / New code total** | **142/143 — 99,30 %** |

Las ramas de `ProgressTransferPolicy` están cubiertas al **100 %** (22/22). / Full branch coverage on
the domain policy.

## Privacidad y límites / Privacy and boundaries

T27 no añade red ni telemetría y no modifica ningún archivo multimedia: cambiar de versión abre otra
ruta ya catalogada y escribe una fila de progreso. La auditoría del origen guarda el identificador
interno del archivo, nunca su ruta. / No network, no telemetry, no media writes, and the audit stores
an internal identifier rather than a path.

`PLY-010` pasa a `VERIFIED`. `LIB-008` y `LIB-009` ya estaban verificados en T15 y T8; esta tarea añade
la prueba de que el progreso viaja entre esas versiones sin corromperse y de que un fallo de apertura no
lo altera. / The transfer identifier verifies; the two library identifiers gain the progress evidence
their earlier tasks did not cover.
