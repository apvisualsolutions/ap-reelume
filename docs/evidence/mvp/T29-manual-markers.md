# T29 — Marcas manuales de introducción y créditos / Manual intro and credits markers

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `f36dcea`
- Commit de tarea / Task commit: `feat: edit and use manual intro and credits markers`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1, SQLite en WAL
- IDs: `PLY-012=VERIFIED`, `PLY-013=PLANNED` (sigue siendo objetivo `STABLE` / still targets `STABLE`)

## RED y GREEN / RED and GREEN

`MarkerPolicyTests`, `ManualMarkerTests` y `MarkerUiTests` se escribieron antes que el modelo, la
política, los comandos, el repositorio y las dos superficies. RED falló porque `IntroMarker`,
`MarkerKind`, `MarkerPolicy`, `SaveManualMarker`, `DeleteManualMarker`, `MarkerEditorViewModel` y
`SkipMarkerViewModel` no existían; la salida está en `artifacts/test-results/T29/red/`. / The three
plan-named test files were written first and RED failed on the missing types.

`IntroMarkerRepositoryTests` se añadió después, como parte del paso de verificación y no como prueba
RED: comprueba el adaptador SQLite igual que `PlaybackPreferenceRepositoryTests` comprobó el de T20. Se
declara aquí para no presentarlo como algo que existiera antes de su código. / The repository test was
added during the verification step rather than as a RED test, and this is stated plainly.

GREEN ejecuta **611 pruebas con 0 fallos y 0 omitidas** en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T29/green/`. `dotnet format --verify-no-changes` no informa cambios y
ambas compilaciones terminan con 0 advertencias. / GREEN runs 611 tests with zero failures and zero
skips.

## Adaptación del número de migración / Migration number adaptation

El plan nombra `0010_intro_markers.sql`, pero ese número lo ocupó `0010_playback_preferences.sql` en
T20 y el `0011` lo ocupó `watch_state` en T25. Esta tarea usa **`0012_intro_markers.sql` con
`"version": 12`**, sin renumerar ni sobrescribir nada, con el `sha256` calculado sobre el texto UTF-8
del archivo como hace `MigrationRunner`. `SqliteBootstrapTests` se actualizó a 12 migraciones, 12
copias previas y las listas de nombres y tablas con `intro_markers`. / The plan's number was taken
twice over, so this task uses migration twelve without renumbering anything.

## Rangos y solapamiento / Ranges and overlap

| Regla / Rule | Comportamiento verificado / Verified behaviour |
|---|---|
| Rango válido / Valid range | `0 ≤ inicio < fin ≤ duración`: `-1` se rechaza, `90→90` se rechaza, `120→90` se rechaza y `0→3000` en un episodio de 3000 s se acepta |
| Fin más allá del episodio / End past the episode | rechazado, tanto en la política como en el comando |
| Duración desconocida / Unknown duration | sólo se comprueba el orden de los dos puntos |
| Solapamiento por tipo / Overlap by kind | dos rangos del **mismo** tipo no pueden solaparse, en cualquiera de las cuatro formas de solapar |
| Bordes que se tocan / Touching edges | `30→120` y `120→180` **no** se solapan |
| Tipos distintos / Different kinds | un resumen puede ocupar exactamente los mismos segundos que una introducción |
| Edición / Editing | un rango no colisiona consigo mismo al editarlo |
| Por serie / Per series | los rangos de una serie no aparecen ni colisionan en otra |
| Botón / Button | visible en `30`, `90` y `119`; no en `29`, `120` ni `200` |
| Salto / Skip | aterriza exactamente en el fin del rango |

## Sólo manual, y con el modelo listo para después / Manual only, with the model ready for later

- `SaveManualMarkerCommand` **no tiene campo de origen**: el comando sólo puede producir
  `Origin = Manual`, comprobado sobre los tres tipos.
- `DeleteManualMarker` **se niega** a borrar un rango con `Origin = Detected`, de modo que una futura
  detección no pueda perder sus datos por esta vía.
- El modelo conserva `Confidence` y `UserCorrected` sin usarlos, y el esquema los almacena: una prueba
  de integración guarda y recupera un rango detectado con confianza `0,72` para demostrar que la
  columna está lista.
- Ningún tipo cuyo nombre contenga «Detect» existe en el dominio ni en los casos de uso; ningún control
  del editor lo menciona; el editor no expone ninguna propiedad ni método de detección. **Ningún
  servicio de detección automática se registra en el MVP.**

/ The command cannot create anything but a manual marker, the delete command protects detected ones,
the forward-compatible columns are proven to round-trip, and no detection type or control exists.

## Las dos superficies / The two surfaces

El botón de salto aparece **sólo** dentro de su rango, comprobado con dos episodios de duraciones
distintas —uno de 50 minutos con introducción en `0:30–2:00` y créditos en `46:40–50:00`, y otro de 90
minutos con sus propios rangos—, y al pulsarlo pide exactamente el fin del rango. El editor lista los
rangos de una serie, se vacía al cambiar de serie, anuncia en texto tanto el rango inválido como el
solapamiento, añade el rango guardado a la lista y lo quita al borrarlo. Todos los controles propios
tienen nombre de automatización y aceptan foco de teclado; los elementos de la lista son enfocables.
Capturado en español e inglés en `artifacts/ui-captures/T29/`. / The button exists only inside its
range in episodes of different lengths, the editor works per series and states both errors in words,
and every control the view declares is named and focusable.

## Cobertura / Coverage

| Archivo / File | Líneas / Lines |
|---|---:|
| `Domain/Continuity/IntroMarker.cs` (modelo y política / model and policy) | 24/24 — 100 % |
| `Application/Continuity/SaveManualMarker.cs` | 38/38 — 100 % |
| `Application/Continuity/DeleteManualMarker.cs` | 8/8 — 100 % |
| `Infrastructure/Data/Repositories/IntroMarkerRepository.cs` | 82/82 — 100 % |
| `Presentation/Player/MarkerEditorViewModel.cs` | 98/104 — 94,23 % |
| **Total del código nuevo / New code total** | **250/256 — 97,66 %** |

Las ramas de `MarkerPolicy` están cubiertas al **100 %** (18/18). / Full branch coverage on the domain
policy.

## Privacidad y límites / Privacy and boundaries

T29 no añade red ni telemetría. Los rangos se guardan por identificador de serie, sin rutas ni nombres
de archivo. No se modifica ningún archivo multimedia: saltar es una búsqueda dentro del motor. /
No network, no telemetry, no paths stored, and no media writes.

`PLY-012` pasa a `VERIFIED`. `PLY-013` pasa a `PLANNED` con objetivo `STABLE`, que es exactamente lo
que su estado significa —«incluido en un plan de implementación aprobado», la Tarea 43— y lo que el
plan de esta tarea pide. **No pasa a `IMPLEMENTED` ni a `VERIFIED`**: comparte modelo con esta tarea,
pero aquí no se implementa ni se registra detección alguna, y decir otra cosa afirmaría un trabajo que
no se ha hecho. / The manual-marker identifier verifies; the detection identifier moves to planned,
which is what its state means, and no further.
