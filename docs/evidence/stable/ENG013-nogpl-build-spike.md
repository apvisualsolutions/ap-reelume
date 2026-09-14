# ENG-013 — Spike: compilar LibVLC sin GPL, x64 / Building LibVLC without GPL, x64

- Fecha / Date: 2026-09-14
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `5a1e3c56`
- Entorno / Environment: Windows 11 x64, 28 núcleos, MSYS2 + mingw-w64 gcc 16.2.0,
  nasm 2.16.03, yasm 1.3.0 (precompilado), cmake 3.31.10, make 4.4.1, VLC 3.0.23 (tag del
  repositorio de VideoLAN)
- IDs: `ENG-013` sigue abierta; esto mide su viabilidad, no la cierra

## Veredicto / Verdict

**Es viable y el núcleo funciona, medido en el binario y por decodificación real, no por la receta.**
Un LibVLC compilado con `--disable-gpl --disable-x264 --disable-x265` produce 330 complementos, de los
cuales **ninguno** contiene la cadena `--enable-gpl`, no aparece ningún `.dll` de x264/x265/postproc, y
decodifica H.264, MPEG-2 y MPEG-4 Parte 2 por la misma ruta de callbacks que usa la aplicación. El
coste de máquina es de **menos de una hora sobre 28 núcleos**; el coste real del trabajo está en las
nueve trampas de abajo, no en el tiempo de compilación. / Viable, measured in the binary and by real
decoding rather than from the recipe: 330 plugins, none carrying `--enable-gpl`, no x264/x265/postproc
DLL, and real decoding through the same callback path the application uses. Machine cost is under an
hour on 28 cores; the real cost is in the nine traps below.

**Lo que NO cierra**: ARM64 entero, la integración en CI, ocho contribs que siguen fallando, y este
build es de depuración sin optimizar. Ver "Lo que queda" al final.

## Cómo se pide, que es más simple de lo que `ENG-013` suponía

VLC 3.0.23 ya trae la palanca de fábrica. No hace falta parchear nada:

```sh
contrib/bootstrap --host=x86_64-w64-mingw32 --disable-gpl --disable-x264 --disable-x265
```

`--disable-gpl` está documentada en el propio `contrib/bootstrap` como «configure to not build viral
GPL code», y el bootstrap lo confirma por pantalla: **«Packages licensing... Lesser GPL version 3»**
en vez de «GPL version 3». El mecanismo exacto está en `contrib/src/ffmpeg/rules.mak`:

```makefile
ifdef GPL
FFMPEGCONF += --enable-gpl --enable-postproc
MAYBE_POSTPROC = libpostproc
endif
```

Por eso `avcodec` y `swscale` comparten la cadena: **salen del mismo build de FFmpeg**. Y `x264`/`x265`
son contribs independientes (`contrib/src/x264/`, `contrib/src/x265/`), GPL por licencia propia, que
no dependen de esa variable — hay que deseleccionarlos aparte, y el bootstrap lo anota como
«Manually deselected packages: ... x264 x265».

## Verificación, con el instrumento controlado

**1. Ningún complemento nuevo lleva la cadena, y el grep no está ciego:**

```
grep -rla -- "--enable-gpl" win64/modules/          → (vacío, sobre 330 .dll)
grep -rla -- "--enable-gpl" <paquete NuGet actual>  → 2 (control positivo: avcodec y swscale)
grep -aoc -- "--enable-gpl" libavcodec_plugin.dll   → 0
grep -aoc -- "--disable-"   libavcodec_plugin.dll   → 3 (control: el grep SÍ lee el binario)
```

El complemento existe y pesa 92 MB (build de depuración): **no es que la cadena falte porque el
binario falte.** La línea de compilación incrustada en el binario nuevo empieza por
`--disable-doc --disable-encoder=vorbis ...`, sin `--enable-gpl` ni `--enable-postproc`.

**2. Cero `.dll` de x264, x262, x265 o postproc.** El configure de VLC lo confirma por su lado:
`checking for libpostproc libavutil... no`, que es exactamente la pieza que `ifdef GPL` activaba.

**3. Decodifica de verdad, no sólo contiene las cadenas.** Sonda en C (`decode-probe.c`) que usa
`libvlc_video_set_callbacks` + `libvlc_video_set_format`, la misma ruta de memoria que el motor de la
aplicación, contando fotogramas entregados:

| Muestra | Esperado | Resultado |
| --- | --- | --- |
| `BUG011/engine-sample.mp4` | decodifica | **22 fotogramas, `h264`** |
| `PLY16/mpeg2-480p-noisy.mkv` | decodifica | **23 fotogramas, `mpgv`** |
| `segments/S01/S01E01.mkv` | decodifica | **22 fotogramas, `mp4v`** |
| `COLOUR/hd-red-bt709.mp4` | decodifica | **23 fotogramas, `h264`** |
| un `.mp4` que es texto plano | **0 fotogramas** | **0 fotogramas** (control negativo) |

**4. Ningún formato de la matriz `T19` desaparece.** Comparadas las descripciones largas de codec que
FFmpeg incrusta, binario nuevo contra el que se distribuye hoy: H.264, HEVC, MPEG-4 Parte 2, VP9, AV1,
AAC, E-AC-3, MP3 y PCM s16le dan **conteos idénticos**. (La fila de Opus no vale como instrumento:
la cadena es corta y aparece en más contextos — 48 contra 30 — y eso no mide nada.)

## Las nueve trampas, que son el coste real

Siete son de **compilar código de 2014-2018 con herramientas de 2026**, y ninguna tiene relación con
la decisión de licencia:

1. **`help2man` no se extrae**: su tarball trae un enlace simbólico y Windows no lo permite sin
   privilegios. Se salta; no interviene en los binarios.
2. **CMake, por los dos lados.** El 3.17.0 que VLC compila no pasa gcc 16, y el **4.4.3 del sistema
   rompe** los paquetes con `cmake_minimum_required` antiguo («Compatibility with CMake < 3.5 has
   been removed»), que aquí eran `ebml`, `glew`, `openjpeg` y más. La salida es un **cmake 3.31.10**
   traído con `pip install "cmake<4"`, apuntado por `PATH`. **Más nuevo no es mejor.**
3. **`yasm` 1.3.0 no compila con gcc 16** (usa `false`/`true` como identificadores, que C23 reserva).
   La salida es el paquete precompilado `mingw-w64-x86_64-yasm`. **Y fingir que está construido, como
   se hizo al principio, rompe `vpx` con un error que habla de otra cosa** («more than one input file
   specified») — un atajo que miente aguas arriba se paga aguas abajo con un mensaje que no señala a
   su causa.
4. **Los contribs antiguos necesitan `-std=gnu17`** en `config.mak`: gcc 16 trata como error los
   prototipos K&R (`void free ();` llamado con un argumento). Arregló ocho paquetes de una vez.
5. **`freetype2` dice «requires the GPL license» y es engañoso.** Su receta es
   `ifndef AD_CLAUSES → $(REQUIRE_GPL)`, y la licencia real de FreeType 2.13.1 es **dual**: la FTL
   —tipo BSD con cláusula de publicidad— o la GPL-2.0, a elección, y su propio `LICENSE.TXT` dice que
   la FTL «is suited to products which don't use the GNU General Public License». **La salida es
   `--enable-ad-clauses`**, comprobado por efecto: compila e instala `libfreetype.a`, y el bootstrap
   informa «Lesser GPL version 3, **with advertisement clauses**». El precio es citar a FreeType en la
   documentación del producto, que este proyecto ya hace. **Iba a reportarse como bloqueo y era falso:
   lo desmintió leer la licencia, no razonar sobre el mensaje.**
6. **`dav1d` falla por meson**, no por licencia: «Unable to detect native OS architecture». Sin
   resolver; AV1 se queda sin su decodificador preferido (`aom` sí compiló, falta medir si cubre).
7. **VLC pide `BUILDCC`** definido aunque no haya compilación cruzada real.
8. **`extras/package/win32/configure.sh` fuerza `--enable-update-check`**, que exige `libgcrypt`; hay
   que pasar `--disable-update-check` después. Es el actualizador **de VLC**, que esta aplicación no usa.
9. **`schroedinger` y `goom` abortan el configure** si falta su contrib, mientras que veinte
   bibliotecas ausentes sólo avisan. Se excluyen con `--disable-schroedinger --disable-goom`.

## Lo que costó de reloj, medido

| Fase | Tiempo |
| --- | --- |
| Herramientas extra (tras saltar cmake y yasm) | ~2 min |
| Contribs, tres intentos (13m30 + 14m17 + 4m12) | ~32 min |
| `bootstrap` de VLC | 38 s |
| `configure` de VLC | ~5 min |
| `make` de VLC, 330 complementos | 7 m 38 s |

Un ciclo limpio, sabiendo ya las nueve trampas, cabe en **unos 25-30 minutos sobre 28 núcleos**. Un
runner hospedado tiene bastantes menos, así que esa cifra **no se traslada a CI**: hay que medirla allí.

## Lo que queda, y no se disimula

- **Ocho contribs siguen fallando**: `dav1d`, `fribidi`, `gpg-error`, `microdns`, `orc`, `regex`,
  `srt`, `zvbi`. Ninguno por licencia; todos por herramientas o por código antiguo.
- **Falta rehacer el ciclo con `--enable-ad-clauses`** para recuperar `freetype2` y con él los
  subtítulos, que esta aplicación sí dibuja. El spike lo comprobó suelto, no en el conjunto.
- **ARM64 entero.** El script acepta `-a aarch64`, pero no se ha tocado.
- **Integración en CI y mantenimiento**: dónde se compila, cada cuánto, y si se recompila en cada
  versión o se guarda el binario propio como artefacto. El presupuesto de CI no admite hoy media hora
  más sin medirlo.
- **Este build es de depuración y sin optimizar** (`--disable-optim`, `-g -O0`): 92 MB por complemento.
  No dice nada del tamaño ni de la velocidad del artefacto final.
- **La sonda `decode-probe.c` vive en el scratchpad de la sesión**, no en el árbol. Si esta vía sigue
  adelante, su sitio es una prueba del repositorio.
