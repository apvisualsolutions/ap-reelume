# ENG-013 — La aplicación lleva el motor propio sin GPL / The application ships the engine of its own, without GPL

- Fecha / Date: 2026-09-18
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Motor publicado / Published engine: prerelease `libvlc-3.0.23-nogpl.1`, promovida por el run
  `35340673630` desde el run de compilación `35336630543` (commit `574ebf7d`), cuyos seis trabajos
  —compilar, verificar y decodificar, en x64 y ARM64— terminaron en `success`
- Entorno / Environment: Windows 11 x64 para las suites y el empaquetado; GitHub Actions para compilar
  y publicar el motor
- IDs: `ENG-013` (se cierra con el CI de este cambio en verde), `ENG-028` (sigue abierta)

## Veredicto / Verdict

**La aplicación y los dos paquetes dejan de llevar `VideoLAN.LibVLC.Windows` y llevan el árbol sin GPL,
fijado por hash, y las diez suites pasan contra él.** Ningún proyecto ni fichero de bloqueo nombra ya
el paquete; el motor llega de una prerelease de este repositorio que la compilación descarga y
rechaza si un solo byte difiere; y lo que acaba dentro del MSIX se compara fichero a fichero con el
árbol que la puerta verificó. / The application and both packages stop carrying VideoLAN's package
and carry the GPL-free tree, pinned by hash, and all ten suites pass against it.

## Lo que se publicó / What was published

| Archivo / Asset | Bytes | SHA-512 (prefijo / prefix) |
| --- | ---: | --- |
| `libvlc-nogpl-x64.zip` | 40 966 169 | `d4996823a2b9720f…` |
| `libvlc-nogpl-arm64.zip` | 36 788 867 | `4735e3b105c46f8a…` |
| `libvlc-3.0.23-nogpl.1-source.tar` | 381 112 320 | `76c136190cb760d1…` |

Los tres se fijan enteros en `eng/libvlc/libvlc.lock.json`. **Es prerelease a propósito**, decidido por
el propietario: `gh api repos/apvisualsolutions/ap-reelume/releases/latest` contestó **404** después de
publicarla, que es exactamente lo que el actualizador recibe y lo que ya trata como «no hay versión».
/ Pinned in full in the lock file. `releases/latest` answered 404 after publication.

| Árbol / Tree | Complementos / Plugins | Referencia / Reference | Retirados por GPL / Removed as GPL |
| --- | ---: | ---: | ---: |
| x64 | 315 | 323 | 31 |
| ARM64 | 305 | 310 | 28 |

**Las cifras cuadran con los catorce GPL del paquete de VideoLAN, y se cuadraron a mano**, porque 31
retirados contra 10 ausentes no parecía cuadrar: el paquete de VideoLAN nunca llevó 23 de los 31
(interfaces y controles como `libhotkeys` o `libdummy`, que su paquete para aplicaciones omite). Los
catorce que sí llevaba se resuelven así: 8 retirados, 2 que ya no se compilan (`liblua`,
`libx26410b`) y 4 que ahora salen limpios —`libavcodec` y `libswscale` por FFmpeg sin GPL,
`libdeinterlace` por el parche de yadif y `libts` sin aribb24—. / The numbers reconcile with the
fourteen: 8 removed, 2 no longer built, 4 now clean.

## Las diez suites contra el motor fijado / The ten suites against the pinned engine

Tras borrar las copias del motor de todas las carpetas de salida —ver la primera trampa—:

| Suite | Resultado / Result |
| --- | --- |
| `Domain.Tests` | 851 de 851 |
| `Application.Tests` | 361 de 361 |
| `ArchitectureTests` | 55 de 55 |
| `DocumentationTests` | 106 de 106 |
| `UiTests` | 1 453 de 1 453 |
| `IntegrationTests` | 661 de 663, 2 omitidas por hardware de audio multicanal, como siempre |
| `MediaTests` | 200 de 201, la omitida es un diagnóstico manual (`APREELUME_DIAG_UYVY`) |
| `PerformanceTests` | 18 de 18 |
| `AccessibilityTests` | 150 de 150 |
| `PackagingTests` | ver abajo / see below |

`dotnet format`, la compilación `Release -warnaserror` y `eng/verify-docs.ps1`, limpios.

## Puertas nuevas, cada una vista fallar / New gates, each seen failing

- **`LibVlcPayloadTests`** (empaquetado): el motor dentro de cada paquete es el árbol verificado,
  fichero a fichero y byte a byte, en los dos sentidos; se copió del árbol fijado y no de uno instalado
  a mano; y el SBOM nombra el motor con su hash. Sin ella, un paquete construido sobre una carpeta de
  salida vieja llevaría los complementos GPL que dejó allí.
- **`LibVlcLockTests`** (arquitectura): ningún proyecto ni fichero de bloqueo referencia el paquete de
  VideoLAN —su primera ejecución lo encontró **de verdad** en diez ficheros de `.runner/_work`, la copia
  de un commit viejo que un runner local guarda dentro del repositorio; se excluyen las carpetas de
  primer nivel que empiezan por punto, y ese hallazgo quedó como control de que el lector ve—; el
  fichero de bloqueo fija los tres hashes y nombra el run; y la etiqueta es de la VLC que compila
  `build-nogpl.sh`. Estuvo en rojo mientras los hashes eran `null`.
- **`LicenceTextTests`** lee la versión del motor del fichero de bloqueo, y exige que el registro del
  código fuente correspondiente nombre ese motor y su paquete de fuentes con el SHA-512 fijado.
- **`fetch-libvlc.ps1`**, por tubería: árbol bueno aceptado; un byte alterado en `libvlc.dll`,
  rechazado; un complemento de más, rechazado; sin nada fijado y sin árbol, rechazado; y el zip
  publicado con un hash cambiado en una sola cifra, **rechazado sin dejar árbol instalado**. La segunda
  llamada con todo en su sitio tarda 0,4 s y no descarga.

## Las trampas, y tres las cazó un ensayo antes de publicar / The traps, three caught by a rehearsal

1. **`PreserveNewest` copia y nunca borra.** Al cambiar de motor, las veinte carpetas de salida del
   repositorio seguían llevando el árbol de VideoLAN —GPL y `win-x86` incluidos—, y las pruebas lo
   habrían cargado dando un verde falso. Se borraron antes de medir, y `LibVlcPayloadTests` hace que un
   paquete construido encima no pueda pasar.
2. **La LGPL-2.1 §2(b) pide un aviso con fecha en cada fichero modificado**, y dos de los cuatro que
   tocan los parches no lo tenían. Los parches se regeneraron sobre los ficheros reales de la etiqueta
   —y la primera regeneración salió con finales de línea convertidos y el modo ejecutable de
   `build.sh` perdido, las dos cosas vistas en el diff antes de empujar—. Obligó a recompilar el motor.
3. **Un tarball de contrib puede diferir entre arquitecturas**: fluidlite se archiva desde git al
   descargarlo. La primera publicación paró en su propia comprobación; ahora se conservan los dos.
4. **`sha256sum --ignore-case` no existe en el runner.** Segundo intento, parado antes de publicar.
5. **La peor, y la cazó el ensayo local del flujo**: el paquete de fuentes se montaba en `src/` dentro
   del checkout, que es donde vive el código de la aplicación, y el ensayo lo encontró **entero** dentro
   del archivo a publicar. Ahora todo se monta en la carpeta temporal del runner y el archivo se abre
   antes de subir: cuatro entradas permitidas, y un fichero intruso lo hace fallar (control negativo
   ensayado).

**Lo que hay que llevarse**: tras dos fallos seguidos de un flujo que publica, se paró de iterar contra
el servidor y se ensayó el paso entero en local con los artefactos reales. El tercer defecto no lo
habría detectado ningún fallo: el flujo habría terminado en verde publicando el código propietario.
/ After two failures in a row of a workflow that publishes, iteration against the server stopped and
the step was rehearsed locally; the third defect would never have failed — it would have published.
