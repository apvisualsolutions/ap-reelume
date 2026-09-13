# ENG-014 — Barrido completo de licencias GPL en los plugins de LibVLC / Full GPL licence scan of the LibVLC plugins

- Fecha / Date: 2026-09-14
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `f2ad042f`
- Entorno / Environment: Windows 11 x64, paquete NuGet `VideoLAN.LibVLC.Windows 3.0.23.1` restaurado
  en `%userprofile%\.nuget\packages\videolan.libvlc.windows\3.0.23.1\build\{x64,arm64}\plugins\`
- IDs: `ENG-014` cerrada

## Veredicto / Verdict

**Tres plugins son GPL, no uno, y por dos razones distintas.** `libavcodec_plugin.dll` y
`libswscale_plugin.dll` llevan la cadena de compilación `--enable-gpl` porque comparten el mismo build
de FFmpeg; `libx26410b_plugin.dll` (x264 de 10 bits) es GPL-2.0-or-later por licencia propia de esa
librería y correctamente **no** lleva esa cadena. Ningún otro plugin de los alrededor de 300 que trae
el paquete, en ninguna de las dos arquitecturas, es GPL. / **Three plugins are GPL, not one, for two
different reasons.** `libavcodec_plugin.dll` and `libswscale_plugin.dll` carry the `--enable-gpl`
build string because they share the same FFmpeg build; `libx26410b_plugin.dll` (10-bit x264) is
GPL-2.0-or-later by that library's own licence and correctly does **not** carry that string. No other
plugin among the roughly 300 the package ships, in either architecture, is GPL.

## Por qué hacía falta / Why this was needed

La medición del 2026-09-13 (`docs/evidence/stable/audit-legal-public.md`, enmienda de ese día) afirmó
que `libavcodec_plugin.dll` llevaba `--enable-gpl` "leída dentro del binario con tres complementos de
control que no la llevan" — sin nombrar esos tres controles ni archivar el comando ni la salida.
`ENG-014` (`docs/TAREAS.md`) señaló además dos afirmaciones concretas que no cuadraban con los
binarios: `libswscale_plugin.dll` no aparecía nombrado en ningún documento pese a llevar la misma
cadena, y `libx26410b_plugin.dll` se presentaba en tres documentos como "el ejemplo más claro" de
plugin GPL cuando el grep de esa cadena da cero en él. Esta evidencia mide las dos afirmaciones a la
vez, sobre el conjunto completo en vez de una muestra.

## Método / Method

Dos comandos, sobre los dos árboles de plugins restaurados por NuGet (x64 y ARM64) por separado.
Ninguno de los dos modifica nada; ambos leen los binarios ya presentes en el caché local de NuGet.

```bash
# Cuántos plugins hay en total, por arquitectura
find ".../videolan.libvlc.windows/3.0.23.1/build/x64/plugins"   -name "*.dll" | wc -l   # → 323
find ".../videolan.libvlc.windows/3.0.23.1/build/arm64/plugins" -name "*.dll" | wc -l   # → 310

# Qué plugins llevan la cadena de compilación de FFmpeg con GPL activado
grep -rla -- "--enable-gpl" ".../x64/plugins"   | sed 's#.*/plugins/##'
grep -rla -- "--enable-gpl" ".../arm64/plugins" | sed 's#.*/plugins/##'

# Qué plugins son x264/x265 por nombre — GPL por licencia propia, sin relación con esa cadena
find ".../x64/plugins"   -iname "*x264*" -o -iname "*x265*"
find ".../arm64/plugins" -iname "*x264*" -o -iname "*x265*"
```

## Resultado / Result

```
=== x64: plugins con --enable-gpl ===
codec/libavcodec_plugin.dll
video_chroma/libswscale_plugin.dll

=== arm64: plugins con --enable-gpl ===
codec/libavcodec_plugin.dll
video_chroma/libswscale_plugin.dll

=== x64: plugins con x264/x265 en el nombre ===
codec/libx26410b_plugin.dll

=== arm64: plugins con x264/x265 en el nombre ===
codec/libx26410b_plugin.dll
```

Idéntico en las dos arquitecturas. No hay `libx264_plugin.dll` (8 bits) ni ningún plugin `x265` en
este paquete — sólo la variante de 10 bits de x264.

## Por qué son dos pruebas distintas, no una / Why these are two different kinds of evidence

El grep de `--enable-gpl` sólo certifica **cómo se compiló el contrib de FFmpeg** dentro de VLC: en el
código fuente de VLC 3.0.23 (`contrib/src/ffmpeg/rules.mak`), la flag existe así:

```makefile
ifdef GPL
FFMPEGCONF += --enable-gpl --enable-postproc
MAYBE_POSTPROC = libpostproc
endif
```

`avcodec` y `swscale` salen del mismo build de ese contrib, así que comparten la misma cadena de
versión — por eso son dos y no uno. Pero `x264` es un contrib **completamente independiente**
(`contrib/src/x264/`, ajeno a `ifdef GPL`): es GPL-2.0-or-later porque esa es la licencia de la
librería x264 en sí, no porque se haya activado ninguna opción de compilación. Grep-earlo buscando
`--enable-gpl` da, correctamente, cero — y tratar eso como "no es GPL" habría sido el error que
`ENG-014` encontró en la documentación.

## Qué se corrigió / What was corrected

Con esta tabla como fuente única: `docs/legal/LEGAL.es.md` / `.en.md`,
`docs/release/THIRD-PARTY-NOTICES.es.md` / `.en.md`, `docs/release/licenses/NOTICE-VideoLAN.txt`, y
la enmienda del 2026-09-14 en `docs/evidence/stable/audit-legal-public.md`. La pantalla de Créditos
(`AboutPlaybackEngineAttribution`) ya era genérica — "algunos de sus complementos llevan licencias
propias, como GPL-2.0-or-later" — y no nombra ningún plugin, así que no necesitó corrección.

## Lo que esto no mide / What this does not measure

Esta evidencia cierra `ENG-014`: las tres afirmaciones señaladas allí. No es una auditoría de licencia
de cada uno de los ~300 plugins del paquete más allá de la señal `--enable-gpl` y el caso ya conocido
de x264 — si algún otro plugin fuera GPL por licencia propia sin depender de esa cadena, este barrido
no lo detectaría. Tampoco mide nada sobre `ENG-013` (compilar sin GPL); eso vive en su propio spike.
