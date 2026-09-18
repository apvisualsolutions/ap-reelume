# Estado legal

Qué está resuelto en el plano jurídico, qué está corregido y qué sigue abierto. Lo escriben quienes
construyen el programa, no un abogado: este documento **no es un dictamen** y no sustituye a uno. Su
utilidad es que nadie tenga que adivinar dónde están los bordes.

Última revisión completa: 2026-08-10, sobre el repositorio público.

## Licencia del programa

AP Reelume by AP Solutions se publica bajo **una licencia propia**, identificada como
`LicenseRef-APSolutions`: gratuita para quien la use, sin derecho a modificar, redistribuir ni vender.
El texto íntegro está en [LICENSE](../../LICENSE) y la atribución del producto en
[NOTICE](../../NOTICE).

**Hasta el 2026-09-13 el programa fue `GPL-3.0-or-later`**, y el cambio lo decidió el propietario por
un motivo de negocio, no técnico: la licencia libre concedía a cualquiera el derecho a modificar,
redistribuir y vender el programa, que son las tres cosas que él quiere reservarse. Lo publicado
hasta esa fecha conserva sus derechos para siempre; esta licencia rige de ahí en adelante. Que sea
posible lo permite un hecho medido: **los 646 commits del repositorio tienen un solo autor**, así que
no hay copyright ajeno que relicenciar.

**El identificador no lleva versión, y es deliberado.** Antes nombraba una licencia concreta, así que
el día que cambió hubo que tocar **1.033 archivos**. Un `LicenseRef-` nombra «la licencia propia de
este proyecto» y apunta al documento; la versión y la fecha viven dentro de `LICENSE`, que es adonde
hay que ir de todos modos para conocer las condiciones. Reescribir las condiciones ya no toca ningún
archivo de código.

Desde la revisión de 2026-08-10, **cada archivo fuente lleva su cabecera SPDX**: 925 archivos `.cs`,
71 `.axaml` y 29 `.ps1` declaran `SPDX-License-Identifier: LicenseRef-APSolutions` junto al titular del
copyright. (Las cifras anteriores —556, 51 y 17— llevaban tiempo desfasadas: **ninguna puerta las
comprueba**, y eso sigue siendo cierto.) Una licencia que solo vive en `LICENSE` deja de estar unida al archivo en cuanto alguien
lo copia fuera del árbol; la cabecera viaja con él. La regla `IDE0073` la exige en `.editorconfig`, de
modo que `dotnet format --verify-no-changes` —una puerta que ya se ejecutaba— rechaza un archivo
nuevo sin cabecera.

## Descargo de garantía

El programa se entrega **sin garantía alguna**, en la medida en que lo permita la ley aplicable. Las
secciones 6 y 7 de [LICENSE](../../LICENSE) lo dicen con todas sus letras —incluido que, por tratarse
de un programa gratuito, el límite de responsabilidad es de cero euros—, y ni el README ni la
aplicación prometen nada distinto. En particular, y porque son las confusiones que de verdad ocurren:

- El artefacto **no está firmado con Authenticode**, y SmartScreen avisará. Está explicado en
  [SMARTSCREEN.es.md](../release/SMARTSCREEN.es.md); la firma de la publicación es otra capa —
  minisign sobre las huellas— que prueba otra cosa.
- La detección automática de segmentos, la identificación de títulos y las recomendaciones son
  **estimaciones locales**, no afirmaciones sobre las obras.
- Nada de lo que la aplicación deduce sobre una biblioteca implica derecho alguno sobre su contenido.
  Quien reproduce es responsable de tener los archivos que reproduce.

## Componentes de terceros

El inventario contrastado con la compilación real, con la licencia de cada componente, está en
[los avisos de terceros](../release/THIRD-PARTY-NOTICES.es.md), y `ThirdPartyNoticeTests` impide que
una dependencia entre en el artefacto sin aparecer allí.

Compatibilidad: una licencia propietaria admite incorporar `LGPL-2.1-or-later`, `MIT`, `Apache-2.0` y
`BSD-3-Clause` cumpliendo sus condiciones —que en el caso de la LGPL son las del §6, y se cumplen
porque las bibliotecas viajan como archivos separados que quien quiera puede sustituir—. **Lo que no
admite es código GPL.**

**Complementos GPL de VideoLAN — CERRADO por ingeniería el 2026-09-18 (`ENG-013`).** La aplicación ya
no lleva el paquete de VideoLAN: lleva LibVLC compilado por este repositorio desde la misma versión de
VLC, sin código GPL, verificado por una puerta que lee la licencia de cada complemento en sus fuentes
y fijado por hash (`eng/libvlc/libvlc.lock.json`). Está **modificado** —se le quitaron el algoritmo
yadif y libdvdread, las dos piezas GPL que no eran un complemento entero—, y los ficheros tocados lo
dicen con fecha, como pide el §2(b) de la LGPL-2.1. Su código fuente correspondiente viaja con cada
versión (ver más abajo).

**La `LGPL-3.0` de gmp, nettle y live555 — leída el 2026-09-18 (`ENG-028`), con una condición
pendiente del propietario.** Las tres van enlazadas de forma estática dentro de cinco complementos,
medido en los binarios de las dos arquitecturas: gmp y nettle en `libgnutls`, en los dos de SRT y en
`libdcp`; live555 en `liblive555`. `libvlc.dll` y `libvlccore.dll` no llevan ninguna. gmp y nettle
tienen doble licencia —LGPL-3.0-or-later o GPL-2.0-or-later, leído en sus propias fuentes— y se usa la
LGPL; live555 es LGPL-3.0-or-later. Contra el §4 de la LGPL-3.0:

- **La obra combinada es cada complemento**, y todo su código es abierto; su fuente completo, con los
  guiones que lo enlazan, viaja con cada versión. Eso es el §4(d)(0). Los complementos son archivos
  separados que el programa carga al arrancar y que cualquiera puede sustituir.
- **Aviso y textos (§4a y §4b)**: los avisos de terceros las nombran con sus complementos y sus
  copyrights, y `licenses/` lleva `LGPL-3.0.txt` y `GPL-3.0.txt`, porque la LGPL-3.0 está escrita sobre
  la GPL-3.0 y pide las dos. Faltaban hasta este día; `LicenceTextTests` impide que vuelvan a faltar.
- **Información de instalación (§4e)**: no se debe. Viene del §6 de la GPL-3.0 y sólo alcanza a un
  «User Product», un aparato de consumo tangible, y un programa que se descarga no lo es.
- **Lo que queda, y no es sólo de la LGPL-3.0**: el §4 exige que las condiciones del conjunto no
  impidan modificar las partes de la biblioteca ni «la ingeniería inversa para depurar esas
  modificaciones». El §6 de la LGPL-2.1 pide lo mismo para LibVLC, y con más claridad, porque el
  programa es la obra que usa esa biblioteca: sus condiciones deben permitir «modificar la obra para
  uso propio» e ingeniería inversa para depurarlo. **`LICENSE` 2.1 y 2.4 prohíben las dos cosas.** La
  cláusula 4 deja a salvo los derechos que las licencias de terceros concedan sobre sus componentes,
  pero no concede nada sobre el programa, que es lo que se pide. La salida es una excepción expresa en
  `LICENSE`, que es decisión del propietario y está pendiente. Hasta que entre, **publicar
  incumpliría la LGPL-2.1**, con o sin estas tres bibliotecas.

`--disable-gnuv3` no es la salida: quitaría las tres bibliotecas —y previsiblemente, con nettle, el
acceso a flujos cifrados, que no está medido— y dejaría intacto el problema de la LGPL-2.1.

Lo que sigue es la historia de cómo se abrió, y se conserva porque es el ejemplo de que una
conclusión correcta puede dejar de serlo sin que nadie toque el código. Este punto
estuvo cerrado desde el 2026-08-10 **con el razonamiento contrario**, y conviene leer cómo se
invirtió, porque es el ejemplo de que una conclusión correcta puede dejar de serlo sin que nadie
toque el código. Entonces el argumento era: el `COPYING` del árbol de VLC lleva la GPL versión 2
**con** la cláusula «either version 2 of the License, or (at your option) any later version», de modo
que un complemento `GPL-2.0-or-later` sube a GPL-3.0 y **encaja dentro de un programa GPL-3.0**. El
razonamiento era válido y sigue siéndolo; lo que cambió es la premisa: **el programa ya no es GPL**,
así que no hay ninguna versión a la que subir.

**Son catorce complementos en x64 y once en ARM64, por tres causas distintas**, leídas el 2026-09-18
en las fuentes de VLC 3.0.23 con `eng/libvlc/scan-plugin-licenses.ps1` (evidencia
`audit-eng027-plugin-gpl-sources.md`). Hasta ese día este párrafo decía «dos», y luego «tres»: las dos
cifras salían de buscar `--enable-gpl` en los binarios, y esa cadena sólo la escribe FFmpeg.

- **FFmpeg compilado como GPL**: `libavcodec_plugin.dll` y `libswscale_plugin.dll`, que comparten el
  mismo build y llevan la cadena dentro.
- **Módulos del propio VLC con fuentes GPL**, que no llevan ninguna cadena que lo diga:
  `libx26410b` (x264), `liblua`, `libdeinterlace` (por su algoritmo yadif), `libhqdn3d`,
  `libheadphone_channel_mixer`, `libdolby_surround_decoder`, `libvisual` y `libremoteosd`; en x64
  además `libglspectrum`, `libi420_rgb_mmx` y `libi420_rgb_sse2`.
- **Una biblioteca GPL enlazada dentro de un módulo LGPL**: `libts_plugin.dll`, el demultiplexor de
  `.ts`, lleva aribb24, reconocible por sus propios mensajes dentro del binario.

**El núcleo, `libvlc.dll` y `libvlccore.dll`, está limpio.** No se ha distribuido ningún artefacto:
el repositorio no tiene releases.

**La consecuencia práctica fue dura mientras duró**: con esos complementos dentro del paquete, **el
artefacto no se podía distribuir** bajo la licencia propia, y el empaquetado quedó suspendido del
2026-09-13 al 2026-09-18.

**La salida se midió y no quita nada de lo que el programa reproduce.** La biblioteca que
descodifica es `LGPL` por defecto, y en FFmpeg lo contagioso son piezas **opcionales** que se activan
al compilar —un filtro de posprocesado heredado y algunas optimizaciones—; **ningún decodificador está
entre ellas**. Los módulos GPL de VLC son funciones que la aplicación no ofrece (listas de
reproducción web, visualizaciones, VNC, filtros de auriculares), un algoritmo de desentrelazado que no
es el que se usa por defecto y se quita con un parche, y la conversión de color por MMX/SSE2, que
`libswscale` también hace. `libts` sin aribb24 sigue leyendo `.ts`; pierde los subtítulos de la
televisión japonesa. La vía fue compilar LibVLC y sus dependencias sin GPL, en las dos arquitecturas,
quitar esos módulos, y mantener esa compilación (`ENG-013`): los catorce códecs prometidos decodifican
con las mismas cifras que el paquete de VideoLAN en Windows x64 y ARM64 reales, subtítulos incluidos.
Es trabajo de infraestructura permanente, no una renuncia de formatos.

**Los textos de las licencias ya viajan — cerrado el 2026-08-10.** Era el incumplimiento abierto: el
artefacto llevaba la `LICENSE` de AP Reelume y los avisos de terceros, pero **no el texto de las
licencias ajenas**, y el paquete NuGet de VideoLAN tampoco incluye ningún `COPYING`, así que nadie lo
aportaba. Las obligaciones son explícitas y no las cumple una tabla que nombre el componente:
LGPL-2.1 §6, GPL-2.0 §1 y Apache-2.0 §4a exigen **acompañar** una copia de la licencia con la
distribución binaria, y MIT y BSD-3-Clause exigen reproducir su aviso de copyright. Ahora
`licenses/`, dentro de los dos artefactos, lleva el texto íntegro de la LGPL-2.1, la GPL-2.0, la
Apache-2.0, la MIT y la BSD-3-Clause, más los avisos de copyright de ANGLE, Skia, HarfBuzz,
BouncyCastle, SQLitePCLRaw, SQLite y VideoLAN. Los que un paquete publica se copian literalmente de
él —`LicenceTextTests` los compara byte a byte contra el paquete que la compilación consumió, de modo
que una subida de versión que cambie un aviso pone la prueba en rojo—; los canónicos se tomaron de
una fuente que ya los distribuía y se contrastaron con una segunda copia independiente. El inventario
y la procedencia de cada texto están en [licenses/README.es.md](../release/licenses/README.es.md), y
lo medido en
[audit-legal-licence-texts.md](../evidence/stable/audit-legal-licence-texts.md).

**Esta pregunta se cerró el 2026-08-14, y se cerró eligiendo la opción que no admite interpretación
en vez de encargando quién interpretaba.** Preguntaba bajo qué apartado del §6 de la LGPL-2.1 queda
amparada la manera en que LibVLC viaja aquí, y si la oferta escrita basta para el §3 de la GPL-2.0.
El §6(b) —el que uno esperaría para una biblioteca dinámica— pide un mecanismo que use «una copia ya
presente en el sistema del usuario», y aquí las DLL las trae el artefacto, así que su primera
condición no se cumple **literalmente**. Pero el §6(d) y el último párrafo del §3 dicen lo mismo y sin
condiciones: si el ejecutable se ofrece para descarga desde un lugar designado, ofrecer el fuente
**desde ese mismo lugar** es distribuirlo.

Eso es lo que hace ahora la versión: `eng/fetch-corresponding-source.ps1` trae `vlc-3.0.23.tar.xz`
—verificado contra la huella que VideoLAN publica—, el archivo de `LibVLCSharp 3.10.0` y, desde el
2026-09-18, el código fuente de la compilación propia del motor —parches, guiones y el tarball de cada
biblioteca de terceros que usó, verificado contra el SHA-512 que este repositorio fija—, y los adjunta
junto a los binarios. La oferta escrita se queda para los canales donde «el mismo sitio» no significa
nada, como una tienda, y ahora vale explícitamente para cualquier tercero. De paso se corrigieron dos
cosas: el aviso nombraba `libvlc 3.0.23.1` —una versión cuyo fuente **no existe**, porque ese cuarto
dígito es del paquete NuGet— y no mencionaba que el trabajo que usa la biblioteca es este programa.
Medido en [audit-corresponding-source.md](../evidence/stable/audit-corresponding-source.md).

**Y el §6(a) dejó de estar disponible el 2026-09-13.** Hasta entonces se cumplía por la vía más
cómoda: publicar el fuente de este programa, que era libre, bastaba para que cualquiera pudiera
relinkar LibVLC. Con una licencia propietaria esa puerta se cierra, y el cumplimiento pasa a
apoyarse en la que la LGPL ofrece justo al lado y que aquí ya se daba de hecho: **las bibliotecas
viajan como archivos separados** que quien quiera puede sustituir por su propia compilación sin tocar
el programa. Esa es la forma habitual en que un programa cerrado usa una biblioteca LGPL, y es
sólida; lo que ya no vale es el argumento anterior, y por eso queda escrito aquí en vez de borrado.

Nada de esto es un dictamen ni lo sustituye: es cumplimiento comprobable contra el texto de las
licencias, y lo que consigue es que no haga falta interpretar.

## API de TMDB

La aplicación consulta `api.themoviedb.org` únicamente si usted pone un token en
`AP_LOCALMEDIA_TMDB_TOKEN`; **el artefacto no lleva ninguno**. Sobre sus términos de uso:

- **Atribución.** Los términos fijan la frase, no su idea. Hasta la revisión de 2026-08-10 el
  programa mostraba un resumen —«usa la API de TMDB… no está avalado ni certificado»—; ahora dice la
  frase exigida, en los dos idiomas, en Créditos, en el `NOTICE` y en los dos README, y una prueba la
  fija carácter a carácter.
- **Retención.** Los términos prohíben conservar más de seis meses lo obtenido de TMDB. La caducidad
  blanda de la caché (un día) no bastaba: cuando la red fallaba o el token desaparecía, el programa
  servía la copia guardada **sin límite de antigüedad**. Ahora hay un suelo duro de 180 días
  (`TmdbOptions.RetentionLimit`): pasado ese plazo la entrada no se sirve y **se borra**.
- **Uso comercial.** Los términos lo reservan a un acuerdo escrito aparte. AP Reelume se entrega
  gratuitamente y no obtiene ingresos de TMDB ni de su contenido, así que hoy no aplica. **El cambio
  de licencia del 2026-09-13 no lo activa** —lo que decide es si se cobra, no cómo se licencia—,
  pero sí acerca el día: la licencia nueva existe precisamente para dejar abierta la puerta de
  cobrar. Si algún día se cobra por el programa, este punto cambia y hay que releerlo antes.
- **Logotipo — cerrado el 2026-08-10.** Los términos piden identificar el uso de TMDB **con su
  logotipo**, menos prominente que el del propio producto. Créditos lo muestra desde esta sesión,
  encima de la frase de atribución, con texto alternativo y sin enlace: identifica el origen de los
  datos, no invita a navegar. El archivo es el que TMDB publica —su huella SHA-256 coincide con la
  que ellos mismos incrustan en la dirección del recurso, y una prueba lo comprueba— y el dibujo de
  la vista es su vector, no una imitación.

## Términos de GitHub

El repositorio se aloja en GitHub y el actualizador consulta `api.github.com` y descarga desde
`github.com` y su almacenamiento. Publicar código en un repositorio público es el uso previsto por
sus Términos de Servicio, **pero desde el 2026-09-13 la relación se invirtió y conviene tenerlo
claro**: esos términos conceden por sí mismos a cualquier usuario el derecho a ver el repositorio y a
**bifurcarlo** dentro de la propia plataforma, y ahora la licencia del programa concede **menos** que
eso. GitHub no lo concede en nombre de AP Solutions y AP Solutions no lo puede retirar mientras el
repositorio sea público; la sección 5 de [LICENSE](../../LICENSE) lo reconoce por escrito y reserva
todo lo demás, en vez de prohibir algo que la plataforma ya permite. **El repositorio sigue público a
propósito**: de ello dependen las máquinas de compilación gratuitas del proyecto, incluidas las
ARM64. No se usa ninguna API de GitHub que exija autenticación ni acuerdo adicional: las peticiones del
actualizador son lecturas anónimas de publicaciones públicas.

## Criptografía y exportación

El artefacto incorpora criptografía en dos lugares: **BouncyCastle** (Ed25519 y Blake2b) para
verificar la firma minisign de las huellas publicadas, y el propio motor de .NET para TLS. No hay
cifrado de datos del usuario en reposo.

Esto sitúa al programa dentro de la categoría de software con criptografía publicado como código
fuente disponible públicamente. Bajo el reglamento estadounidense de exportación (EAR), esa categoría
se acoge normalmente a la excepción TSU de §740.13(e), que **exige una notificación por correo
electrónico** a la BIS y a la ENC Encryption Request Coordinator indicando la dirección desde la que
el código está disponible. El repositorio está alojado en Estados Unidos, así que la regla es
aplicable.

**Estado: no consta que la notificación se haya enviado.** Es una acción del propietario, de coste
prácticamente nulo (un correo con la URL del repositorio), y forma parte de lo que el dictamen
profesional debe confirmar. Se nombra aquí para que no se pierda.

## Marca, dominio y nombre público

`REL-004` en [la matriz de alcance](../FEATURES.md) registra la comprobación formal de marca, dominio
y Store para «AP Reelume by AP Solutions». La decisión de nombre está en
[ADR-0001](../adr/0001-public-product-name.md), con una comprobación preliminar que **no sustituye**
al informe final.

## Lo que queda del propietario

Ninguno de estos puntos lo puede cerrar quien escribe código, y ninguno frena el desarrollo:

| Punto | Qué falta | Dónde vive |
|---|---|---|
| Dictamen jurídico profesional | Encargo a un profesional que cubra licencia, terceros, TMDB, exportación y marca | `REL-004` |
| Notificación de exportación | Correo a BIS y a ENC con la URL del repositorio. Va desde su identidad, por eso es suyo; el texto está abajo, listo para copiar | esta página |
| Marca y dominio | Informe formal de `REL-004` | `REL-004`, ADR-0001 |
| Firma Authenticode | Decisión económica pospuesta, ya documentada | SMARTSCREEN |

Salieron de esta lista el 2026-08-10, resueltos en vez de delegados: los **complementos de VideoLAN**
(comprobado que son `GPL-2.0-or-later`, compatible) y el **logotipo de TMDB**, que no era una decisión
sino un requisito de sus términos. Está incorporado desde esa misma fecha; abajo queda cómo, y qué se
midió para corregir la cifra que la especificación traía mal.

### El logotipo de TMDB, incorporado

Sus términos piden identificar el uso de TMDB con su logotipo, «menos prominente» que el del propio
producto. No era una elección de marca que correspondiera posponer: es parte de la condición bajo la
que se usa la API, igual que la frase de atribución. Quedó así:

- El archivo oficial se tomó de la página de marca de TMDB y viaja versionado en
  `src/ApSolutions.LocalMedia.Presentation/Assets/tmdb-logo.svg`; nunca se descarga en ejecución. Su
  autenticidad es comprobable sin creerle a nadie: TMDB incrusta la huella SHA-256 del recurso en su
  propia dirección, y `TmdbLogoTests` compara el archivo contra ella.
- Va en Créditos, encima de la frase de atribución. **Se dibuja a 16 px frente a los 24 px a los que
  el raíl de navegación dibuja el nombre del producto.** La especificación decía «24 px frente a
  48 px»: ese 48 no existía en ninguna vista —el nombre del producto se dibuja a 24— y con el
  logotipo a 24 «menos prominente» habría dejado de ser comprobable. Se midió y se corrigió; las dos
  cifras las leen las pruebas de los propios AXAML, y otra las compara ya renderizadas.
- Avalonia no dibuja SVG, y traer un renderizador para una marca de 16 px habría metido media docena
  de paquetes —y sus licencias— dentro del artefacto. La vista lleva la geometría del archivo, y una
  prueba compara las dos carácter a carácter: una aproximación de una marca ajena sobreviviría a una
  revisión por captura y muere aquí.
- Lleva texto alternativo en los dos idiomas para el lector de pantalla y no es un enlace: identifica
  el origen de los datos, no invita a navegar.

Lo medido está en [audit-legal-tmdb-logo.md](../evidence/stable/audit-legal-tmdb-logo.md).

### La notificación de exportación, redactada

Enviar el correo es suyo porque sale de su identidad. El contenido no tiene nada que decidir:
destinatarios `crypt@bis.doc.gov` y `enc@nsa.gov`, asunto «TSU notification — publicly available
encryption source code», cuerpo con el nombre del proyecto, la URL
`https://github.com/apvisualsolutions/ap-reelume` y la frase de que el código fuente que incorpora
criptografía (Ed25519 y Blake2b vía BouncyCastle, para verificar firmas de publicación) está
disponible públicamente en esa dirección, conforme a §740.13(e) del EAR.

## Cómo informar de un problema legal

Si cree que este proyecto infringe una licencia, una marca o un derecho suyo, escriba por el mismo
canal privado que [SECURITY.md](../../SECURITY.md) describe para las vulnerabilidades. Se responde a
todo, y una atribución incorrecta se corrige sin discutir la corrección.
