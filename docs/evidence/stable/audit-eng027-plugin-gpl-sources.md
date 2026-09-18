# ENG-027 — Los complementos GPL de LibVLC, leídos en sus fuentes / The GPL LibVLC plugins, read from their sources

- Fecha / Date: 2026-09-18
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `69856abb`
- Entorno / Environment: Windows 11 x64; fuentes de VLC en el tag `3.0.23` (commit
  `578d28f6c9f2379164516e689418f92ac74a3445`, espejo `github.com/videolan/vlc`); paquete NuGet
  `VideoLAN.LibVLC.Windows 3.0.23.1` restaurado en la caché local
- IDs: `ENG-027` (abierta y cerrada aquí), `ENG-013` (cambia lo que tiene que hacer), `ENG-024`
  (pierde su vía preferida)

## Veredicto / Verdict

**No son tres complementos GPL: son catorce en x64 y once en ARM64, por tres causas distintas.** Los
tres que ya estaban nombrados siguen ahí. En x64 faltaban **once**: diez módulos del propio VLC con
fuentes GPL, y el demultiplexor de `.ts`, que lleva dentro una biblioteca GPL enlazada. ARM64 no trae
`glspectrum` ni las dos conversiones de color por MMX/SSE2, y por eso se queda en once en total. / **Not three GPL plugins but fourteen on x64 and eleven on ARM64, for three different
reasons.** The three already named are still there; the missing ones are VLC's own modules with GPL
source files, plus the MPEG-TS demuxer with a GPL library linked in.

**Y cambia el veredicto del spike de `ENG-013`.** Aquel build se declaró «sin GPL» porque ninguno de
sus 330 complementos llevaba `--enable-gpl`. Esa cadena sólo la escribe FFmpeg, y **el configure de
VLC no tiene interruptor de GPL**: `contrib/bootstrap --disable-gpl` quita bibliotecas de terceros y
nada más, así que los módulos propios con fuente GPL se compilan igual. Buscado en `configure.ac`:
cero apariciones de `gpl`, sin distinguir mayúsculas. / The ENG-013 spike's "no GPL" verdict does not
hold: VLC's configure has no GPL switch, so its own GPL modules are built either way.

**No se ha distribuido nada.** El repositorio tiene cero releases (`gh api repos/{owner}/{repo}/releases
--jq length` → `0`; la misma consulta sobre `cli/cli` → `1`, control positivo). / Nothing has been
distributed: zero releases, with a positive control.

## Las tres causas / The three causes

| Causa / Cause | Cómo se ve / How it shows | Complementos / Plugins |
| --- | --- | --- |
| FFmpeg compilado con `--enable-gpl` | la cadena, dentro del binario | `libavcodec`, `libswscale` |
| Fuente GPL del propio VLC | la cabecera de un fichero fuente o de un `#include` | `libx26410b`, `liblua`, `libdeinterlace`, `libhqdn3d`, `libheadphone_channel_mixer`, `libdolby_surround_decoder`, `libvisual`, `libremoteosd`; sólo x64: `libglspectrum`, `libi420_rgb_mmx`, `libi420_rgb_sse2` |
| Biblioteca GPL enlazada estáticamente | sus propios mensajes, dentro del binario | `libts` (aribb24) |

Qué fichero es GPL en cada caso, porque eso decide la salida:

- `libdeinterlace`: **sólo** `yadif.h` y `yadif_template.h`, que vienen de FFmpeg. El resto de
  algoritmos del módulo es LGPL, y el modo `auto` elige `x` (`deinterlace.c:390`), así que quitar
  yadif no cambia lo que se ve por defecto.
- `libi420_rgb_sse2`: no lista ningún fichero GPL. **Incluye** `i420_rgb_mmx.h`, que sí lo es. Sin
  seguir los `#include` no aparece, y la primera versión del escáner no lo vio.
- `libhqdn3d`: `hqdn3d.h`, que viene de MPlayer. Es el reductor de ruido que `ENG-024` proponía.
- `liblua`: todo el módulo. `libx26410b`: `codec/x264.c`, además de la licencia de x264.
- `libts`: su fuente es LGPL. Lleva dentro los textos «arib parser was created» y «arib decoder was
  created», que **no existen en ninguna fuente de VLC** (buscados en `modules/`, cero) y sí en la
  biblioteca aribb24, cuya receta en `contrib/src/aribb24/rules.mak` dice `$(REQUIRE_GPL)`. Es el
  mismo caso en x64 y ARM64.

## Método / Method

El escáner vive en el repositorio y se corre así:

```powershell
git clone --depth 1 --branch 3.0.23 https://github.com/videolan/vlc.git vlc-src
pwsh -NoProfile -File eng/libvlc/scan-plugin-licenses.ps1 -VlcSource vlc-src `
  -PluginDir "$env:USERPROFILE\.nuget\packages\videolan.libvlc.windows\3.0.23.1\build\x64"
```

Lee los `Makefile.am` de `modules/` para saber de qué fuentes sale cada complemento. Sigue los
`#include "..."` dentro del árbol y clasifica cada fichero por su cabecera: GPL si dice «GNU General
Public» y no «Lesser». Busca además `--enable-gpl` en el binario.

**Se niega a dar una lista más corta**: falla si un complemento del directorio no está declarado en
ningún `Makefile.am`, si una fuente declarada no existe, o si no clasifica como GPL `codec/x264.c`,
que es el control positivo.

Salida, x64 (las rutas de fuente se omiten aquí; el guion las imprime):

```
Plugins scanned: 323
Plugins with GPL code: 13
  plugins/codec/libavcodec_plugin.dll  [FfmpegGplBuild]
  plugins/video_filter/libdeinterlace_plugin.dll  [SourceHeader]
  plugins/audio_filter/libdolby_surround_decoder_plugin.dll  [SourceHeader]
  plugins/visualization/libglspectrum_plugin.dll  [SourceHeader]
  plugins/audio_filter/libheadphone_channel_mixer_plugin.dll  [SourceHeader]
  plugins/video_filter/libhqdn3d_plugin.dll  [SourceHeader]
  plugins/video_chroma/libi420_rgb_mmx_plugin.dll  [SourceHeader]
  plugins/video_chroma/libi420_rgb_sse2_plugin.dll  [SourceHeader]
  plugins/lua/liblua_plugin.dll  [SourceHeader]
  plugins/spu/libremoteosd_plugin.dll  [SourceHeader]
  plugins/video_chroma/libswscale_plugin.dll  [FfmpegGplBuild]
  plugins/visualization/libvisual_plugin.dll  [SourceHeader]
  plugins/codec/libx26410b_plugin.dll  [SourceHeader]
```

ARM64: 310 complementos, 10 con código GPL (los mismos sin `libglspectrum`, `libi420_rgb_mmx` ni
`libi420_rgb_sse2`). A los dos se suma `libts` por la tercera causa, que el escáner **dice** que no
ve en vez de callarla.

## Los controles, y un instrumento que estuvo ciego / The controls, and an instrument that was blind

- **La primera versión dio cero complementos GPL**, y el cero obligó a mirar el instrumento. Buscaba
  las fuentes junto a cada `Makefile.am`. Pero esos ficheros se incluyen desde `modules/`, así que sus
  rutas son relativas a él: ninguna fuente se encontraba, y una fuente que no se encuentra no es GPL.
  Ahora una fuente declarada que no existe es un error y no un silencio. La única excepción es
  `dummy.cpp`: automake lo pide para enlazar en C++ y nunca existe.
- **Seguir los `#include` añadió uno**: `libi420_rgb_sse2`. Sin ese paso, el resultado coincidía con
  un script de prueba anterior que tampoco los seguía. Coincidir con otra medición igual de ciega no
  prueba nada.
- **Positivo**: `codec/x264.c` tiene que salir GPL o el guion falla.
- **Negativo**: 310 de 323 salen sin GPL. `libconsole_logger` sale limpio aunque el registro a fichero
  de VLC (`logger/file.c`) sea GPL, porque ese módulo **no está en el paquete**. Tampoco están
  DirectShow, la interfaz Qt, los atajos de teclado ni los demás módulos GPL del árbol.
- **Casos inválidos**: con un directorio vacío o una ruta que no es un árbol de VLC, el guion sale
  con código 1 y dice por qué.

**El núcleo está limpio.** De `src/`, `lib/` e `include/` sólo `src/win32/mta_holder.h` es GPL, y lo
incluye únicamente `modules/access/dshow/dshow.cpp`, que no va en el paquete.

## Lo que cuesta quitarlos / What removing them costs

Para quien usa el programa, **nada de lo que hoy reproduce**:

- `lua`: analizadores de listas de reproducción web y buscadores de carátulas de VLC. La aplicación
  usa TMDB.
- Visualizaciones, VNC (`remoteosd`), los filtros de auriculares y Dolby Surround: la aplicación no
  los ofrece. Buscado en `src/`: cero apariciones de `headphone`, `deinterlace` o `hqdn3d`.
- yadif se quita **con un parche al módulo**, no quitando el módulo: el desentrelazado sigue.
- MMX/SSE2 de I420→RGB: la conversión la hace también `libswscale`, que es LGPL sin `--enable-gpl`.
- `libts` sin aribb24 **sigue leyendo `.ts` y `.m2ts`**. Pierde los subtítulos ARIB, que son los de
  la televisión japonesa.

**Lo que sí cambia es un plan**: `ENG-024` proponía `libhqdn3d_plugin.dll` como reductor de ruido.
Es GPL, así que esa vía se cierra y queda la otra que la fila ya nombraba: un filtro propio dentro
del shader.

## Qué se corrigió / What was corrected

`docs/legal/LEGAL.{es,en}.md`, `docs/release/THIRD-PARTY-NOTICES.{es,en}.md` y
`docs/release/licenses/NOTICE-VideoLAN.txt`, con esta tabla como fuente. Las filas de `ENG-013` y
`ENG-024` en `docs/TAREAS.md`. La evidencia de `ENG-014` no se reescribe: su sección «Lo que esto no
mide» ya decía que un complemento GPL por licencia propia se le escaparía, y eso es lo que pasó.
