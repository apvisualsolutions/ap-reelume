# T19 — Matriz legal de contenedores y códecs / Legal container and codec matrix

- Fecha / Date: 2026-08-02
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `27aef93`
- Commit de tarea / Task commit: `test: add reproducible licensed playback matrix`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302, PowerShell 7.6.3, LibVLC 3.0.23.1,
  LibVLCSharp 3.10.0, GPU NVIDIA GeForce RTX 5070, ffmpeg `2024-06-21-git-d45e20c37b-full_build`
- IDs: `PLY-002=VERIFIED`, `PLY-001=IN_PROGRESS`, `PRD-005=IN_PROGRESS`

## RED y GREEN / RED and GREEN

`CodecMatrixTests` y `CorruptMediaTests` se escribieron antes de la política de diagnóstico. RED
falló porque `PlaybackRecoveryAction`, `PlaybackDiagnosticsPolicy` y
`PlaybackFailure.RecoveryActions` no existían; la salida se conserva en
`artifacts/test-results/T19/red/T19-red-media.log`. / Both plan-named test files were written before
the diagnosis policy. RED failed because the recovery action type, the policy, and the recovery
property did not exist; the output is retained at the path above.

GREEN ejecuta 266 pruebas con 0 fallos y 0 omitidas en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T19/green/`. La cobertura combinada de líneas del código de
reproducción es 93,44 % (755/808) y las políticas de dominio `PlaybackStatePolicy` y
`PlaybackDiagnosticsPolicy` alcanzan 100 % de ramas (48/48). / GREEN runs 266 tests with zero
failures and zero skips in Release; combined playback line coverage is 93.44% and both domain
policies reach 100% branch coverage.

`dotnet format --verify-no-changes` y `dotnet build -c Release -warnaserror` terminan con 0
advertencias y 0 errores. La verificación documental pasa con 30 Markdown, 6 archivos localizados,
53 IDs y 46 IDs MVP. / Formatting and the Release build finish with zero warnings and zero errors;
documentation verification passes with the counts above.

Excepción de cobertura justificada: `ShellExternalPlaybackLauncher` queda en 30,8 % de líneas
porque la rama que entrega el archivo al shell de Windows no se ejecuta desde una prueba
automatizada —abriría una aplicación real—. Esa rama se verifica físicamente más abajo. /
Justified coverage exception: the external launcher stays at 30.8% because the branch that hands
the file to the Windows shell is not driven from an automated test; it is physically verified below.

## Procedencia de las muestras / Sample provenance

Ninguna muestra está versionada. Todas se generan durante la ejecución desde los generadores
sintéticos de ffmpeg `testsrc2` (vídeo) y `sine` (audio), bajo `artifacts/test-media/T19/`, que
`.gitignore` excluye. `eng/generate-test-media.ps1` reproduce la matriz desde el manifiesto
`tests/ApSolutions.LocalMedia.MediaTests/Fixtures/media-manifest.json` e imprime tamaño y SHA-256 de
cada archivo; la prueba de procedencia registra los mismos valores en
`artifacts/test-results/T19/green/media-provenance.json`. Ningún archivo de la biblioteca personal
se lee, se copia ni se modifica. / No sample is version-controlled. Every one is generated during
the run from ffmpeg's synthetic generators into an ignored directory; the generator script and the
provenance test record size and SHA-256 for each file. No personal library file is read, copied, or
modified.

## Matriz por muestra / Per-sample matrix

Hardware: CPU x64 con GPU NVIDIA GeForce RTX 5070; decodificación por software en las ejecuciones
automatizadas, que usan la salida de vídeo por callbacks del motor. / Software decoding through the
engine's callback video output on the hardware above.

| Muestra / Sample | Contenedor / Container | Vídeo / Video | Audio | Bytes | Esperado / Expected | Resultado / Result |
|---|---|---|---|---:|---|---|
| `mp4-h264-aac` | MP4 | H.264 | AAC | 221 126 | `Playable` | PASS: A/V arrancan, 1 pista de vídeo y 1 de audio con códec anunciado, duración dentro de ±1 s, final alcanzado / PASS |
| `mkv-hevc-eac3` | Matroska | HEVC | E-AC-3 | 154 602 | `Playable` | PASS |
| `avi-mpeg4-mp3` | AVI | MPEG-4 Parte 2 | MP3 | 219 552 | `Playable` | PASS |
| `mov-h264-pcm` | QuickTime | H.264 | PCM S16LE | 460 606 | `Playable` | PASS |
| `webm-vp9-opus` | WebM | VP9 | Opus | 97 927 | `Playable` | PASS |
| `mkv-av1-opus` | Matroska | AV1 | Opus | 58 725 | `Playable` | PASS |
| `mkv-h264-no-audio` | Matroska | H.264 | ninguno / none | 194 973 | `Playable` | PASS: reproduce y el aviso de ausencia de audio se activa / PASS with the missing-audio notice raised |
| `mkv-avs2-unsupported` | Matroska | AVS2 | ninguno / none | 49 961 | `ActionableUnsupported` | PASS: `UnsupportedCodec` con elegir otra versión y apertura externa / PASS |
| `mp4-truncated` | MP4 | H.264 | AAC | 77 394 | `ActionableUnsupported` | PASS: `CorruptedMedia` con las mismas dos acciones / PASS |
| archivo ausente / absent file | — | — | — | — | `ActionableUnsupported` | PASS: `FileNotFound` con reintentar y elegir otra versión / PASS |

Todos los codificadores requeridos estaban disponibles, así que ninguna fila se omitió. Si faltara
alguno, la fila se omite con el nombre exacto del codificador ausente y nunca se declara superada. /
Every required encoder was available, so no row skipped. A missing encoder skips its row with the
exact encoder name and is never reported as passing.

## Diagnóstico accionable / Actionable diagnosis

`PlaybackDiagnosticsPolicy` traduce lo que el motor observó a un código de dominio localizable, sin
que el adaptador decida nada:

- sin analizar o sin pistas → `CorruptedMedia`;
- con pistas pero ninguna de vídeo o audio → `NoPlayableTrack`;
- con pistas presentables y ninguna descripción de códec → `UnsupportedCodec`;
- en cualquier otro caso, reproducible.

La ausencia de decodificador se detecta porque LibVLC no devuelve descripción de códec para una
pista que no sabe decodificar; se comprobó con AVS2, que se anuncia pero no produce ningún
fotograma. / The policy turns the engine's observation into a localisable domain code without the
adapter deciding anything, and a missing decoder is detected because LibVLC returns no codec
description for a stream it cannot decode, as AVS2 demonstrates.

Las acciones ofrecidas son `Retry`, `ChooseAnotherVersion` y `OpenExternally`. La enumeración no
contiene ninguna opción destructiva y una prueba lo comprueba sobre todos los códigos. Tras cada
fallo el archivo conserva tamaño y SHA-256, y el motor sigue siendo reutilizable con una muestra
válida. / The offered actions contain no destructive option, a test asserts that over every code,
and after each failure the file keeps its size and hash while the engine stays reusable.

Textos ES/EN nuevos: `PlayerFailureUnsupportedCodec`, `PlayerFailureCorruptedMedia`,
`PlayerFailureNoPlayableTrack`, `PlayerNoticeNoAudioTrack`, `PlayerRecoveryRetry`,
`PlayerRecoveryChooseAnotherVersion`, `PlayerRecoveryOpenExternally` y
`PlayerRecoveryExternalFailed`, en paridad en `Strings.es.axaml` y `Strings.en.axaml`. / The listed
resource keys were added to both language dictionaries in parity.

## Apertura externa verificada físicamente / External playback verified physically

`ShellExternalPlaybackLauncher` entrega la ruta completa al verbo de shell registrado como argumento
único; no compone ninguna línea de comandos. Comprobación física con la muestra AVS2 que el motor
integrado no puede decodificar: Windows abrió **Media Player Classic Home Cinema** (`mpc-hc64`) con
el archivo, y tras cerrarlo el archivo conservó 49 961 bytes y el mismo SHA-256. Un archivo ausente
se rechaza sin arrancar nada y una cancelación nunca llega al shell. / The launcher passes the full
path to the registered shell verb as a single argument and composes no command line. Physically
checked with the AVS2 sample the embedded engine cannot decode: Windows opened Media Player Classic
Home Cinema and, after closing it, the file kept its exact size and hash. An absent file is refused
without starting anything and a cancelled request never reaches the shell.

## Licencias y avisos / Licences and notices

`docs/release/THIRD-PARTY-NOTICES.es.md` y `.en.md` recogen los componentes distribuidos y los de
desarrollo con la licencia declarada en el paquete restaurado: Avalonia, Microsoft.Data.Sqlite y
Microsoft.Extensions.DependencyInjection bajo MIT; LibVLCSharp y VideoLAN.LibVLC.Windows bajo
LGPL-2.1-or-later; SQLitePCLRaw bajo Apache-2.0 sobre SQLite de dominio público; xunit bajo
Apache-2.0; FsCheck y NSubstitute bajo BSD-3-Clause; FlaUI, BenchmarkDotNet y coverlet bajo MIT.
Se documentan además las dos versiones declaradas y no consumidas —`LibVLCSharp.Avalonia` y
`NetArchTest.Rules`— y que ffmpeg es una herramienta de desarrollo externa que no se redistribuye. /
Both notice files record distributed and development components with the licence declared by the
restored package, the two declared-but-unconsumed versions, and that ffmpeg is an external
development tool that is not redistributed.

`PRD-005` sigue `IN_PROGRESS`: la licencia y los avisos ya existen y se han revisado, pero el SBOM
por artefacto es un entregable de T40/T41. / The free-software identifier stays in progress because
the per-artifact SBOM remains a release-gate deliverable.

## Límites y privacidad / Boundaries and privacy

T19 no añade cliente de red ni telemetría. `eng/verify.ps1` genera la matriz antes de las pruebas y
después comprueba que `git status` no contiene artefactos ni medios; si alguno apareciera, la puerta
falla. Ningún archivo de vídeo se copia, elimina ni mueve, y ninguna acción de recuperación es
destructiva. / T19 adds no network client or telemetry. The gate generates the matrix, then fails if
any artifact or media file reaches the working tree. No video file is copied, deleted, or moved, and
no recovery action is destructive.

`PLY-002` pasa a `VERIFIED`: cada fila de la matriz aprobada reproduce o explica su
incompatibilidad. `PLY-001` continúa `IN_PROGRESS` hasta que T24 cierre los modos de ventana de la
sesión integrada. / The broad codec identifier is verified because every approved row plays or
explains its incompatibility; the embedded-player identifier stays in progress until the window
modes close in T24.
