# T20 — Pistas, subtítulos y preferencias por ámbito / Tracks, subtitles, and scoped preferences

- Fecha / Date: 2026-08-02
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `3ef8afc`
- Commit de tarea / Task commit: `feat: persist audio and subtitle preferences by scope`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302, SQLite WAL, LibVLC 3.0.23.1,
  Avalonia 12.1.1, pantallas ASUS ProArt PA279CRV a 2560×1440 con escala 150 %
- IDs: `PLY-005=VERIFIED`, `PLY-004=IN_PROGRESS`, `A11Y-002=IN_PROGRESS`

## RED y GREEN / RED and GREEN

`PreferenceResolutionTests`, `TrackAndSubtitleTests` y `SubtitleStyleTests` se escribieron antes
que el dominio de preferencias. RED falló porque el espacio de nombres
`ApSolutions.LocalMedia.Domain.Continuity` y los tipos `PlaybackPreference`, `PreferenceScope`,
`TrackSelection`, `SubtitleStyle` y `PreferenceResolutionPolicy` no existían; la salida se conserva
en `artifacts/test-results/T20/red/`. / The three plan-named test files were written before the
preference domain existed; RED failed because the continuity namespace and its types were missing,
and the output is retained at the path above.

GREEN ejecuta 297 pruebas con 0 fallos y 0 omitidas en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T20/green/`. La cobertura combinada de líneas del código nuevo es
96,48 % (576/597) y las políticas de dominio alcanzan 97,62 % de ramas (41/42). /
GREEN runs 297 tests with zero failures and zero skips; combined new-code line coverage is 96.48%
and the domain policies reach 97.62% branch coverage.

`dotnet format --verify-no-changes` y `dotnet build -c Release -warnaserror` terminan con 0
advertencias y 0 errores. / Formatting and the Release build finish clean.

## Precedencia y selección / Precedence and selection

La resolución es **campo a campo**: `File > Series > Global > EngineDefault`. Un campo sin valor en
un ámbito cae al siguiente, de modo que una preferencia de archivo que sólo fija el idioma de audio
no borra la velocidad guardada para la serie. `ResolvedPlaybackPreference` devuelve además el ámbito
que respondió a cada campo, para que la interfaz pueda explicar por qué un valor está en vigor. /
Resolution is field by field with the stated precedence; an unset field falls through, and the
resolved value carries the scope that answered it.

La selección de pista es **por atributos**, nunca por índice: idioma y canales, luego idioma, luego
canales y, por último, la primera pista de esa clase. La preferencia por subtítulo externo sólo
reordena candidatos; si no hay ninguno externo, sigue eligiendo el interno que corresponde. La
selección nunca cruza la clase de pista. / Track selection matches attributes rather than a
position, degrades in the stated order, and never crosses the track kind.

Demostración con medios reales: dos muestras generadas anuncian las mismas dos pistas de audio en
orden opuesto —`mkv-dual-audio-spanish-first` y `mkv-dual-audio-english-first`—. Con la misma
preferencia almacenada (`spa`, 6 canales) ambas resuelven a la pista española 5.1, que es
exactamente el caso que una selección por índice acertaría en una y fallaría en la otra. La
preferencia se guardó en el ámbito de serie y se reaplicó al segundo episodio sin tocarlo. /
Two generated samples announce the same two audio tracks in opposite order; the same stored
preference resolves to the Spanish 5.1 track in both, and the series-scope preference reapplies to
the second episode untouched.

## Subtítulos internos y externos / Internal and external subtitles

| Caso / Case | Resultado / Result |
|---|---|
| Subtítulo interno SRT en Matroska / Internal SRT in Matroska | Se anuncia como pista no externa, se selecciona y se desactiva / announced, selected, and switched off |
| `.srt` UTF-8 junto al medio / beside the media | Descubierto, codificación `UTF-8`, cargado como pista externa / discovered, loaded |
| `.ass` UTF-8 | Descubierto, codificación `UTF-8`, formato `ASS` / discovered |
| `.vtt` UTF-16LE con BOM / with BOM | Descubierto, codificación `UTF-16LE`, texto legible / discovered and readable |
| Archivo homónimo fuera de la raíz / Same-named file outside the root | **No se descubre**; con la raíz ajena el resultado es vacío / never discovered |

`ExternalSubtitleDiscovery` sólo mira el directorio del propio medio, sólo acepta `.srt`, `.ass` y
`.vtt`, exige que el nombre empiece por la base del medio y comprueba que tanto el medio como el
candidato queden dentro de la raíz indicada. La codificación se determina por marca de orden de
bytes, de modo que UTF-8 y ambos órdenes de UTF-16 se leen como texto. / The discovery looks only in
the media's own directory, allows only the three extensions, requires the shared base name, and
confines both the media and the candidate to the given root; encoding comes from the byte order
mark.

## Persistencia y migración / Persistence and migration

**Adaptación documentada respecto al plan.** El plan de implementación nombra
`0008_playback_preferences.sql`. Las versiones 1 a 9 ya están ocupadas por migraciones aprobadas
—hasta `0009_rename_log.sql`— y el manifiesto es monótono y verificado por hash, así que renumerar o
sobrescribir habría roto bases de datos existentes. Se crea por tanto
`0010_playback_preferences.sql` con `"version": 10`. Una sola migración cubre T20 y T23: la tabla
incluye ya `audio_output_device_id`, que T23 usará para el dispositivo de salida persistente. /
Documented adaptation: the plan names migration `0008`, but versions one to nine are taken by
approved, hash-verified migrations, so the new file is `0010` with version 10. One migration covers
both tasks because the table already carries the audio output device column T23 needs.

`PlaybackPreferenceRepository` guarda una fila por clave de ámbito con todas las columnas anulables:
un campo sin valor se almacena como `NULL` y nunca se convierte en un valor predeterminado que
tapase al siguiente ámbito. Verificado en integración: ida y vuelta completa con una instancia nueva
del repositorio, campos sin valor que siguen sin valor tras releer, y guardado repetido del mismo
ámbito que actualiza en lugar de duplicar. / One row per scope key with every column nullable, so an
unset field never becomes a stored default; verified end to end with a fresh repository instance.

`SqliteBootstrapTests` se actualizó al nuevo estado: 10 migraciones aplicadas, 10 copias previas y
la tabla `playback_preferences` en el esquema. / The bootstrap test now expects ten migrations, ten
pre-migration copies, and the new table.

## Interfaz accesible / Accessible interface

`TrackSelectorView` presenta las pistas descritas por sus atributos —idioma, disposición de canales
y códec—, una entrada explícita para desactivar los subtítulos y una casilla para recordar la
elección en toda la serie. `SubtitleStyleView` ofrece tamaño de 50 % a 300 %, familia tipográfica de
una lista segura instalada con Windows 11, color de texto y de fondo, opacidad de fondo y grosor de
borde, con vista previa en vivo. / The track selector describes tracks by attribute, offers an
explicit "no subtitles" entry, and can remember the choice for the whole show; the style view
exposes size, safe family, colours, background opacity, and outline with a live preview.

Los seis controles declarados por la vista de estilo tienen nombre de automatización y son
enfocables, y la vista se renderizó al **100 %, 150 % y 200 %** en español e inglés sin que la vista
previa se colapse. Capturas en `artifacts/ui-captures/T20/subtitle-style-scale-100.png`, `-150.png`,
`-200.png` y `subtitle-style-high-contrast.png`. En alto contraste el texto de la vista previa
conserva altura y contenido. / All six declared controls carry an accessible name and are focusable;
the view renders at the three scalings in both languages and under high contrast without collapsing
the preview, with the captures listed above.

Todo valor almacenado se recorta en el dominio antes de aplicarse: un tamaño de 5000 % queda en
300 %, uno de 1 % queda en 50 %, una opacidad negativa queda en 0 y un borde de 99 queda en 4. Un
color que no sea `#RRGGBB` o `#AARRGGBB` se rechaza en lugar de guardarse. Ninguna preferencia
almacenada puede, por tanto, ocultar el texto. / Every stored value is clamped by the domain before
it is applied and a malformed colour is rejected, so no stored preference can hide the text.

Narrator no se ejerció en esta tarea: la auditoría integral de lectores de pantalla es T33 y
`A11Y-002` permanece `IN_PROGRESS` hasta entonces. Aquí se comprueba el árbol de automatización, no
la locución. / Narrator was not exercised here; the end-to-end screen-reader audit is a later task
and the identifier stays in progress.

## Límites y privacidad / Boundaries and privacy

T20 no añade cliente de red ni telemetría. Los subtítulos externos se leen, nunca se escriben ni se
mueven, y la búsqueda no puede salir de la raíz de la biblioteca. Las muestras y los archivos de
subtítulos usados en las pruebas se generan bajo `artifacts/test-media/`, ignorado por Git. Ninguna
ruta absoluta local, nombre de usuario ni nombre de equipo aparece en el código, la documentación o
este informe. / T20 adds no network client or telemetry; external subtitles are read only and the
search cannot leave the library root. Test media and subtitle files are generated under an ignored
directory, and no local absolute path, user name, or machine name appears anywhere.

`PLY-005` pasa a `VERIFIED`: audio y subtítulos internos y externos se detectan y seleccionan, y la
preferencia se reaplica al siguiente episodio. `PLY-004` continúa `IN_PROGRESS` porque la elección
de dispositivo de salida y las disposiciones multicanal cierran en T23. `A11Y-002` continúa
`IN_PROGRESS` hasta la auditoría de accesibilidad. / The tracks and subtitles identifier is verified;
the audio output identifier waits for T23 and the accessibility identifier for the later audit.
