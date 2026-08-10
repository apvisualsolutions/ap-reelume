# Estado legal

Qué está resuelto en el plano jurídico, qué está corregido y qué sigue abierto. Lo escriben quienes
construyen el programa, no un abogado: este documento **no es un dictamen** y no sustituye a uno. Su
utilidad es que nadie tenga que adivinar dónde están los bordes.

Última revisión completa: 2026-08-10, sobre el repositorio público.

## Licencia del programa

AP Reelume by AP Solutions se publica bajo `GPL-3.0-or-later`. El texto íntegro está en
[LICENSE](../../LICENSE) y la atribución del producto en [NOTICE](../../NOTICE).

Desde la revisión de 2026-08-10, **cada archivo fuente lleva su cabecera SPDX**: 556 archivos `.cs`,
51 `.axaml` y 17 `.ps1` declaran `SPDX-License-Identifier: GPL-3.0-or-later` junto al titular del
copyright. Una licencia que solo vive en `LICENSE` deja de estar unida al archivo en cuanto alguien
lo copia fuera del árbol; la cabecera viaja con él. La regla `IDE0073` la exige en `.editorconfig`, de
modo que `dotnet format --verify-no-changes` —una puerta que ya se ejecutaba— rechaza un archivo
nuevo sin cabecera.

## Descargo de garantía

El programa se entrega **sin garantía alguna**, en la medida en que lo permita la ley aplicable. Las
secciones 15, 16 y 17 de la GPL-3.0 lo dicen con todas sus letras, y ni el README ni la aplicación
prometen nada distinto. En particular, y porque son las confusiones que de verdad ocurren:

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

Compatibilidad: `GPL-3.0-or-later` admite incorporar `LGPL-2.1-or-later`, `MIT`, `Apache-2.0` y
`BSD-3-Clause`, que es todo lo que el paquete transporta.

**Complementos de VideoLAN — cerrado el 2026-08-10.** Estaba anotado como pregunta para el dictamen:
si algún complemento fuera `GPL-2.0-only` sería incompatible con GPL-3.0. Se comprobó en la fuente:
el `COPYING` del árbol de VLC es la GPL versión 2 **con** la cláusula «either version 2 of the
License, or (at your option) any later version», así que el conjunto es `GPL-2.0-or-later` y encaja
bajo GPL-3.0. El punto sale de la lista de pendientes.

**Los textos de las licencias no viajan todavía — pendiente y prioritario.** El artefacto lleva la
`LICENSE` de AP Reelume y los avisos de terceros, pero **no el texto de las licencias ajenas**. Se
comprobó además que el paquete NuGet de VideoLAN no incluye ningún `COPYING`, de modo que nadie lo
está aportando. Las obligaciones son explícitas y no las cumple una tabla que nombre el componente:
LGPL-2.1 §6, GPL-2.0 §1 y Apache-2.0 §4a exigen **acompañar** una copia de la licencia con la
distribución binaria, y MIT y BSD-3-Clause exigen reproducir su aviso de copyright. La corrección es
mecánica —los textos son canónicos y `licenses/` ya viaja en el paquete— y es lo primero que debe
hacerse en la próxima sesión.

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
- **Uso comercial.** Los términos lo reservan a un acuerdo escrito aparte. AP Reelume es software
  libre y no obtiene ingresos de TMDB ni de su contenido, así que hoy no aplica. Si algún día se
  cobrara por el programa, este punto cambia y hay que releerlo antes.
- **Logotipo — pendiente.** Los términos piden identificar el uso de TMDB **con su logotipo**, menos
  prominente que el del propio producto. Hoy Créditos muestra el texto «TMDB» y la frase de
  atribución, pero no el logotipo. Incorporar la marca de un tercero es una decisión del propietario,
  no de quien programa, y queda nombrada aquí como acción pendiente.

## Términos de GitHub

El repositorio se aloja en GitHub y el actualizador consulta `api.github.com` y descarga desde
`github.com` y su almacenamiento. Publicar código bajo una licencia libre en un repositorio público
es exactamente el uso previsto por sus Términos de Servicio, y la sección F de esos términos ya
concede a otros usuarios el derecho de ver y bifurcar el repositorio; la GPL-3.0-or-later concede
más. No se usa ninguna API de GitHub que exija autenticación ni acuerdo adicional: las peticiones del
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
sino un requisito de sus términos: se incorpora, y la especificación está abajo para que la ejecución
sea mecánica.

### El logotipo de TMDB, decidido

Sus términos piden identificar el uso de TMDB con su logotipo, «menos prominente» que el del propio
producto. No es una elección de marca que corresponda posponer: es parte de la condición bajo la que
se usa la API, igual que la frase de atribución. Se incorpora, con esta forma:

- El archivo oficial se toma de la página de marca de TMDB y viaja versionado en
  `src/ApSolutions.LocalMedia.Presentation/Assets/tmdb-logo.svg`, no se descarga en ejecución.
- Va en Créditos, encima de la frase de atribución que ya está, con un alto de 24 px — la mitad del
  espacio que ocupa el nombre del producto en esa misma vista, que es como se cumple «menos
  prominente» de forma verificable.
- Lleva texto alternativo para el lector de pantalla y no es un enlace: identifica el origen de los
  datos, no invita a navegar.
- Una prueba fija su presencia en `CreditsView.axaml`, igual que la que fija la frase.

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
