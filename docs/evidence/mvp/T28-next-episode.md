# T28 — Siguiente episodio con cuenta atrás / Next episode countdown

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `9d72373`
- Commit de tarea / Task commit: `feat: play the next available episode after countdown`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1
- IDs: `PLY-011=VERIFIED`, `PLY-014=VERIFIED`, `LIB-010=VERIFIED`

## RED y GREEN / RED and GREEN

`NextEpisodePolicyTests`, `NextEpisodeCountdownTests` y `NextEpisodeOverlayTests` se escribieron antes
que la política, los casos de uso y el overlay. RED falló porque `EpisodeSequenceEntry`,
`NextEpisodePolicy`, `StartNextEpisodeCountdown`, `NextEpisodeViewModel` y `NextEpisodeOverlay` no
existían; la salida está en `artifacts/test-results/T28/red/`. / The three plan-named test files were
written first and RED failed on the missing types.

GREEN ejecuta **566 pruebas con 0 fallos y 0 omitidas** en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T28/green/`. `dotnet format --verify-no-changes` no informa cambios y
ambas compilaciones terminan con 0 advertencias. / GREEN runs 566 tests with zero failures and zero
skips.

## Alcance de la persistencia: puerto sin adaptador todavía / Scope: a port without an adapter yet

El plan de T28 no nombra ninguna migración ni ningún repositorio de infraestructura, y el esquema
actual no relaciona `episodes` con `media_files`. Esta tarea define por tanto el puerto
`IEpisodeSequenceRepository` en el dominio y prueba el caso de uso contra un repositorio en memoria,
**sin añadir migración ni adaptador SQLite**. Es el mismo precedente que T15 dejó establecido con
`IMediaVersionGroupRepository`, cuyo adaptador tampoco existe todavía. Se declara aquí para que la
ausencia sea una decisión visible y no un olvido: la vinculación episodio-archivo en la base de datos
llega con Inicio y las fichas completas en T30. / The task defines the port and tests against an
in-memory repository because the plan names no migration or adapter here; this mirrors the precedent
T15 set, and the database link arrives with T30.

## El orden / The order

| Regla / Rule | Comportamiento verificado / Verified behaviour |
|---|---|
| Orden estándar / Standard order | temporada ascendente y episodio ascendente, con los especiales —temporada 0— **al final** |
| Siguiente normal / Ordinary next | el episodio que sigue en la misma temporada |
| Final de temporada / End of season | continúa en el primer episodio de la temporada siguiente |
| Hueco de numeración / Numbering gap | se salta: de `1x01` a `1x04` sin detener la cadena |
| No disponible o sin archivo / Unavailable or fileless | se salta y se ofrece el siguiente que sí se puede reproducir |
| Especiales explícitos / Explicit specials | un episodio normal **nunca** encadena a un especial, y un especial sólo continúa en otro especial |
| Último de la serie / Last of the series | no hay siguiente |
| Episodio ajeno a la serie / Episode not in the series | no hay siguiente, en lugar de adivinar |
| Resto no disponible / Remainder unavailable | no hay siguiente cuando todo lo que queda falta |

## La cuenta atrás / The countdown

- Anuncia **cada segundo**: `10, 9, …, 1, 0`, y sólo entonces abre.
- `0 s` significa que la cadena está **desactivada**: no se ofrece nada y no se abre nada.
- La longitud se limita al rango aprobado —`-5` pasa a `0`, `600` pasa a `60`, `25` se acepta— y
  sobrevive a una instancia nueva del comando porque se guarda en el almacén local de ajustes.
- Se cancela desde **cualquier** método de entrada: teclado, ratón y tecla multimedia, enrutados por
  `InputCommandRouter`, que es el mismo componente que T24 verificó para no duplicar acciones.
- Con un segundo por delante, retirar el archivo hace que la revalidación en cero lo detecte: el
  resultado es «no disponible» y **no se abre nada**.
- Si el motor rechaza la apertura, se informa como no disponible en lugar de aparentar que empezó.
- Sin siguiente reproducible, el resultado es «no hay siguiente», que es lo que devuelve la interfaz a
  la ficha de la serie.

**Tres episodios encadenados y ninguna sesión doble:** el coordinador registra un máximo de **una**
sesión simultánea a lo largo de las tres aperturas. / Three chained episodes never hold two sessions.

## El overlay / The overlay

Oculto hasta que hay algo que ofrecer; muestra el episodio y los segundos, y los dígitos cambian con
cada tick; los dos botones —reproducir ahora y cancelar— cierran la espera e informan la elección;
el overlay se anuncia como región activa **cortés**, así que el lector de pantalla lo lee sin robar el
foco; cada control tiene nombre de automatización. Capturado en español e inglés en
`artifacts/ui-captures/T28/`. / The overlay is polite rather than intrusive, named, and captured in
both languages.

## Cobertura / Coverage

| Archivo / File | Líneas / Lines |
|---|---:|
| `Domain/Continuity/NextEpisodePolicy.cs` | 34/34 — 100 % |
| `Application/Continuity/GetNextEpisode.cs` | 5/5 — 100 % |
| `Application/Continuity/StartNextEpisodeCountdown.cs` | 48/48 — 100 % |
| `Presentation/Player/NextEpisodeViewModel.cs` | 43/45 — 95,56 % |
| **Total del código nuevo / New code total** | **130/132 — 98,48 %** |

Las ramas de `NextEpisodePolicy` están cubiertas al **100 %** (16/16). / Full branch coverage on the
domain policy.

## Privacidad y límites / Privacy and boundaries

T28 no añade red ni telemetría. El overlay muestra la etiqueta que le pasa quien lo abre, no una ruta.
No se modifica ningún archivo multimedia: la cadena abre otra ruta ya catalogada. / No network, no
telemetry, no paths on screen, and no media writes.

`PLY-011` pasa a `VERIFIED`. `PLY-014` ya estaba verificado en T24; aquí se comprueba además que la
cancelación funciona desde los tres orígenes de entrada. `LIB-010` ya estaba verificado en T5, T8 y T9;
aquí se comprueba que una unidad retirada durante la cuenta atrás impide la apertura en vez de fallarla.
/ The countdown identifier verifies; the input and availability identifiers gain the evidence this task
produces.
