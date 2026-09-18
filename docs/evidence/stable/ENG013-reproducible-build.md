# ENG-013 — LibVLC sin GPL, reproducible y en CI / LibVLC without GPL, reproducible and in CI

- Fecha / Date: 2026-09-18
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commits: `5f308625` (compilación y puerta) a `e6a4b429` (referencia fijada por hash)
- Entorno / Environment: GitHub Actions, `ubuntu-24.04` dentro de las imágenes de CI de VideoLAN
  (`vlc-debian-win64-3.0:20251208092116` y `vlc-debian-llvm-ucrt:20250903131032`), y Windows 11 x64
  para la puerta y la sonda en local
- IDs: `ENG-013` (sigue abierta: falta llevar el árbol a la aplicación), `ENG-027`

## Veredicto / Verdict

**LibVLC 3.0.23 compila sin GPL para x64 y ARM64, en CI y de forma repetible, y los dos árboles
reproducen todo lo que la aplicación promete con las mismas cifras que el paquete de VideoLAN**,
medido en Windows x64 y en Windows ARM64 reales. Ningún
complemento lleva código GPL según sus fuentes. FFmpeg no lleva `--enable-gpl`. Las bibliotecas de
terceros se compilaron con la GPL apagada y FreeType bajo su licencia FTL. / LibVLC 3.0.23 builds
without GPL for x64 and ARM64, in CI and repeatably, and the x64 tree plays everything the
application promises with the same figures as VideoLAN's package.

**Lo que esto NO cierra**: la aplicación sigue cargando el paquete NuGet de VideoLAN. Cambiarlo por
este árbol toca el proyecto de infraestructura, el empaquetado, el código fuente correspondiente y
las diez suites, y es la tanda siguiente de `ENG-013`.

## Cómo se compila / How it is built

Como compila VideoLAN: su `extras/package/win32/build.sh`, dentro de las imágenes que nombra su
`extras/ci/gitlab-ci.yml` en el tag, cruzando a Windows desde Linux. Lo hace `eng/libvlc/build-nogpl.sh`
en el flujo `.github/workflows/libvlc-nogpl.yml`, que corre aparte del CI principal y sólo cuando
cambia `eng/libvlc/`. El guion de VideoLAN no se reescribe: hereda del entorno `CONTRIBFLAGS` y
`CONFIGFLAGS`, que nunca inicializa.

- **Contribs**: `--disable-gpl --disable-x264 --disable-x265 --enable-ad-clauses`.
- **Configure de VLC**: `--disable-faad --disable-lua --disable-realrtsp --disable-mpc
  --disable-update-check`. `configure.sh` pide faad por nombre, y un módulo pedido cuya biblioteca
  falta aborta el configure en vez de avisar (`m4/with_pkg.m4`).
- **Dos parches** en `eng/libvlc/patches/`, sin una sola línea GPL dentro:
  - `0001`: el desentrelazador deja de compilar yadif. El modo `auto` elige `x` y un modo
    incompatible cae en `blend` (`deinterlace.c:390-410`). Un modo que el build no tiene cae en
    `auto` en vez de dejar el filtro a medio configurar.
  - `0002`: `build.sh` añade `--enable-dvdread` **después** de nuestras banderas, y dvdread es GPL.

La primera versión del parche `0001` borraba los ficheros de yadif, y así el código GPL entraba
íntegro en nuestro repositorio como líneas borradas. Ahora sólo deja de compilarlos.

## La puerta / The gate

`eng/libvlc/verify-nogpl.ps1` monta el árbol con la forma del paquete NuGet. Retira lo que
`scan-plugin-licenses.ps1` marca y exige que un segundo escaneo salga vacío. Cada comprobación lleva un
control contra el paquete de VideoLAN, que **sí** es GPL:

| Comprobación | Control, sobre la referencia |
| --- | --- |
| Fuentes GPL de los módulos | nombra complementos |
| FFmpeg con `--enable-gpl` | `libavcodec` la lleva |
| `GPL := 1` ausente y `AD_CLAUSES := 1` presente en el `Makefile` de `contrib/bootstrap` | — la presencia exigida es el control |
| aribb24 enlazada | `libts` lleva «arib parser was created» |
| yadif en el binario del desentrelazador | la referencia nombra `yadif2x` |
| FreeType presente | — |

**Control negativo de la puerta entera**: el paquete de VideoLAN disfrazado de build propio se
rechaza por cuatro motivos, uno por clase. Son FFmpeg GPL, la falta de las opciones de contrib,
aribb24 dentro de `libts` y yadif dentro del desentrelazador.

**La referencia se fija por hash.** `eng/libvlc/fetch-reference.ps1` acepta el paquete sólo con el
SHA-512 del `.nupkg`, medido igual en nuget.org, en la caché local y en su `.nupkg.sha512`. Con un
solo carácter cambiado lo rechaza.

## Resultado / Result

| | x64 | ARM64 |
| --- | --- | --- |
| Complementos en el árbol | 315 | 305 |
| En la referencia | 323 | 310 |
| Retirados por fuente GPL | 31 | 28 |
| Faltan respecto a la referencia | 10 | 7 |
| Compilación (runner de 4 núcleos) | 28,2 y 28,4 min | 22,2 y 27,2 min |

**Lo que falta respecto a la referencia es exactamente lo previsto**: los módulos GPL que el paquete
de VideoLAN llevaba (`dolby_surround_decoder`, `headphone_channel_mixer`, `hqdn3d`, `remoteosd`,
`visual`, y en x64 `glspectrum` e `i420_rgb_mmx`/`sse2`) más `lua` y `x264`, que ya no se compilan.
`libts` y `libdeinterlace` **siguen**, sin aribb24 y sin yadif. Los demás retirados son módulos GPL
que el configure compila por defecto y que VideoLAN ya quitaba a mano de su paquete: DirectShow,
atajos de teclado, registro a fichero, demultiplexor Real, RTP, podcasts y veinte más.

## Reproducción / Playback

`eng/libvlc/decode-probe.ps1` carga el árbol en su propio proceso, porque el proceso de pruebas ya
tiene cargado el LibVLC de NuGet y un proceso no puede tener dos. Lo recorre por la misma ruta de
memoria que el motor, con vídeo RV32 y audio S16N. x64, en local:

| Muestra | Referencia | Sin GPL |
| --- | --- | --- |
| H.264 / HEVC / MPEG-2 / MPEG-4 / VP9 / AV1 | 50 / 50 / 49 / 49 / 44 / 49 fotogramas | 50 / 50 / 49 / 49 / 44 / 49 |
| AAC / AC-3 / E-AC-3 / DTS | 97.280 / 95.232 / 95.232 / 96.256 muestras | 97.280 / 96.768 / 96.768 / 96.256 |
| MP3 / FLAC / Opus / PCM | 97.920 / 96.000 / 96.960 / 96.000 | 97.920 / 96.000 / 96.960 / 96.000 |
| `.mp4` que es texto | 0 | 0 |
| Subtítulo, píxeles claros con / sin | 645 / 0 | 645 / 0 |

ARM64, en el runner `windows-11-arm` del run de `e6a4b429` (las seis fases en verde):

| Muestra | Referencia | Sin GPL |
| --- | --- | --- |
| H.264 / HEVC / MPEG-2 / MPEG-4 / VP9 / AV1 | 50 / 50 / 49 / 49 / 46 / 49 fotogramas | 50 / 50 / 49 / 50 / 46 / 49 |
| AAC / AC-3 / E-AC-3 / DTS | 97.280 / 95.232 / 95.232 / 96.256 muestras | 97.280 / 96.768 / 96.768 / 96.256 |
| MP3 / FLAC / Opus / PCM | 97.920 / 96.000 / 96.960 / 96.000 | 97.920 / 96.000 / 96.960 / 96.000 |
| `.mp4` que es texto | 0 | 0 |
| Subtítulo, píxeles claros con / sin | 644 / 0 | 644 / 0 |

AC-3, DTS, AAC y MP3 importan porque sus bibliotecas (a52, dca, faad2, mad) eran GPL y desaparecen:
ahora los decodifica `libavcodec`. Las pequeñas diferencias de AC-3 y E-AC-3 se deben a que el
decodificador es otro, y el número de muestras en dos segundos depende de dónde corte la reproducción.

**Mutación**: la misma sonda sobre la referencia **sin** `libfreetype_plugin.dll` falla sólo por el
subtítulo, con 0 píxeles contra 0. Una pista de subtítulos que se demultiplexa y no se dibuja no
pasaría.

**Los errores de conversión que imprime el motor** («Failed to create video converter», «Too high
level of recursion») salen en la muestra AV1, con el mismo patrón, **también en la referencia**.
Atribuidos marcando cada muestra en el registro.

## Lo que costó, y conviene no repetir / What it cost

- **Un espejo de SourceForge muerto.** `contrib/src/main.mak` fija `SF :=
  https://netcologne.dl.sourceforge.net/`, que no resuelve desde los runners. Los reintentos no lo
  arreglaban, porque fallaba siempre el mismo host. Se sobrescribe `SF` en la línea de `make` con la
  entrada general de SourceForge, y los `SHA512SUMS` del contrib siguen verificando cada tarball.
- **Leer las opciones en el fichero equivocado.** `bootstrap` escribe `GPL := 1` en su `Makefile`, y
  la puerta miraba `config.mak`, que son las banderas de `build.sh`: «GPL ausente» pasaba siempre. Lo
  delató la mitad que exige `AD_CLAUSES`.
- **El `contentHash` del lock no es el hash del paquete** cuando el paquete va firmado: NuGet lo
  calcula sin la firma.
- **`-r` en `build.sh` se salta `-o`**: activa el empaquetado de release, que se evalúa antes que la
  ruta de instalación. Se pasa `-i none`.
- **Con dos flujos sobre un commit, `watch-ci.ps1` tomaba el primer run**, fuera del flujo que
  fuera. Ahora pregunta por `CI`, y la prueba que lo exige se vio fallar antes.

## Lo que queda / What is left

- Sustituir `VideoLAN.LibVLC.Windows` por este árbol en la aplicación y en el MSIX, con su código
  fuente correspondiente (el tag, los dos parches y los guiones).
- Publicar el árbol como asset fijado por hash. Eso publica algo en GitHub y lo decide el propietario.
- LGPLv3: el `Makefile` de contrib lleva `GNUV3 := 1`, así que entran bibliotecas LGPL versión 3
  (gmp, nettle, live555). Son LGPL y enlazan como bibliotecas, pero la versión 3 añade condiciones y
  hay que leerlas contra `ADR-0013` antes de distribuir.
