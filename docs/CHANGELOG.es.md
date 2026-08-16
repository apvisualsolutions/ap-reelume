# Cambios

Todo cambio relevante de AP Reelume. La versión inglesa está en [CHANGELOG.en.md](CHANGELOG.en.md).

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el versionado es
[SemVer](https://semver.org/lang/es/). El registro canónico del alcance, con su estado y su
evidencia, es [FEATURES.md](FEATURES.md).

## [Sin publicar] / [Unreleased]

### Añadido

- **Actualizador independiente.** Comprueba si hay una versión nueva sólo cuando lo pides o lo has
  permitido, te dice qué cambia en español y en inglés, descarga a una carpeta aparte comprobando el
  hash y el tamaño publicados, y entrega el paquete a Windows únicamente tras una confirmación que
  nombra esa versión. Una descarga que se corta se reanuda desde donde iba; una que no coincide se
  borra. Tu biblioteca no participa en la descarga: se midió que la base de datos y el binario en uso
  quedan intactos tras una actualización correcta, cancelada, manipulada e interrumpida. La Store
  sigue usando su propio canal.
- **Entrada para el gestor de paquetes de Windows.** Cada compilación deja el manifiesto de winget
  generado desde el propio archivo: el hash es el publicado, el ejecutable que declara está
  comprobado dentro del ZIP y las descripciones salen de los dos README. winget no cuesta nada y no
  exige certificado, así que es la primera vía de instalación que estará disponible.
- **Paquete ARM64 nativo.** MSIX y ZIP `win-arm64` construidos y verificados: cada binario del
  payload lleva ARM64 en su cabecera, LibVLC viaja donde el cargador lo busca, y la aplicación es
  idéntica a la de x64 archivo por archivo. Se construye en cada verificación y en cada publicación.
- **Detección automática de introducciones, resúmenes y créditos.** Compara localmente los
  episodios de cada serie entre sí y encuentra el audio que se repite; nada sale de la máquina y no
  se abre una sola conexión. Las detecciones se guardan por episodio con su confianza, una marca
  manual del mismo tipo las suprime siempre, y aceptar o corregir una la protege de todas las
  ejecuciones posteriores. El trabajo cede el paso a la reproducción y la opción está desactivada
  hasta que la enciendas. Evaluada contra un corpus retenido de series sintéticas: cada umbral
  aprobado se cumplió en la primera medición, con cero detecciones espurias en los episodios sin
  segmento.

- **Actualizar solo las fichas más antiguas, si tú lo enciendes.** Al abrir la aplicación se pueden
  volver a pedir al proveedor las fichas cuyos datos tienen más de 90 días: como mucho 20 por vez,
  las más antiguas primero y sólo las de títulos ya identificados. Viene apagado, el interruptor está
  en Ajustes → Privacidad y **no se ofrece siquiera si no has consentido la conexión**, porque sin
  ella no habría nada que pedir. Nunca ocurre mientras se escanea ni con un vídeo abierto, y se
  comprueba antes de cada ficha, no sólo al empezar. Con el interruptor apagado no se abre ninguna
  conexión: medido con el vigilante de red, que en la misma ejecución cuenta cero apagado y dos
  encendido.
- **Una guardia permanente contra el defecto de la casa.** La auditoría encontró componentes
  registrados que la aplicación nunca invoca, y cada caso se cazó a mano; ahora una prueba de
  arquitectura exige que cada servicio registrado tenga al menos una resolución fuera de su propio
  registro. Su primera ejecución enumeró 32 huérfanos —no los ~12 estimados— y destapó caras nuevas:
  la selección de dispositivo de audio nunca llega al motor, las preferencias de reproducción
  guardadas no se aplican, el conmutador de «visto» no está conectado a nada, no hay manera de
  retirar una carpeta de la biblioteca, y elegir una versión duplicada no hace nada. Cada deuda vive
  en la propia prueba con su identificador, y una segunda aserción expulsa la entrada en cuanto su
  cableado aterriza: la lista sólo puede encoger.

- **Abrir un vídeo ya no puede dejar la ventana quieta esperando al teclado.** Al empezar cada
  reproducción, la aplicación reclama las teclas multimedia del teclado, y se quedaba parada —el hilo
  que dibuja la ventana incluido— hasta que ese registro contestaba, sin ningún plazo. Si no
  contestaba, no había salida: el mismo hilo atrapado sujetaba el cerrojo que hacía falta para
  cancelarlo. Ahora la espera tiene plazo y ocurre fuera del cerrojo, y si se agota la reproducción
  empieza igual: las teclas del teclado son un extra, y una sesión sin ellas es mejor que una sesión
  que no arranca.

- **Ningún botón puede ya cerrar la aplicación al fallar.** Cada superficie con botones traía su
  propia clase de comando escrita a mano —veinticuatro— y ninguna recogía un fallo, así que un error
  en el trabajo de detrás terminaba el programa. Ahora hay una sola, y recoge siempre: quedan dos
  sitios en todo el código donde una espera puede terminar sin dueño, y los dos capturan. Al
  unificarlas aparecieron dos comportamientos que sólo una superficie tenía: la valoración de una
  ficha comprobaba el valor antes de guardarlo —y esa comprobación se habría perdido en silencio si
  una prueba no llega a estar ahí—, y los saltos del reproductor rechazan el siguiente mientras uno
  está en curso. Los dos siguen.

- **Un fallo deja de poder llevarse la aplicación por delante.** Hasta ahora, si algo salía mal en el
  trabajo que hay detrás de un botón, la excepción no volvía a nadie: se relanzaba sobre el hilo de la
  interfaz, y ahí lo único que esperaba era el final del programa. Ahora hay una red: lo que llega a lo
  alto del proceso queda anotado como un código en vez de tumbarlo, y una tarea que falla sin dueño se
  recoge antes de que se convierta en un cierre. Y el informe de diagnóstico —que existe desde hace
  tiempo y sólo sabía hablar de renombrados— por fin cuenta lo demás: hasta ahora, en una sesión donde
  nadie renombraba nada, una aplicación que fallaba parecía sana. Lo anotado vive en memoria y sólo
  durante esa sesión: nada se escribe en tu disco, y de una excepción viaja su tipo, nunca su mensaje.

- **La aplicación revisa sus propios cables al construirse.** Un componente que pide algo que nadie
  registró era hasta ahora un fallo que esperaba a la primera pantalla que lo necesitara —quizá la
  tuya, en un rincón que ninguna prueba abrió—. Ahora esa revisión ocurre al montar la aplicación, de
  modo que el fallo aparece al arrancar cualquier prueba en vez de delante de alguien. Cubre 109 de
  los 156 registros: los 45 que se construyen con una función propia siguen siendo opacos a la
  revisión, y decirlo es mejor que dejar creer que están cubiertos. Cuesta 0,22 milisegundos por
  arranque. En su primera ejecución no encontró ni un cable roto.

- **Preferencia de idioma.** En Ajustes → Apariencia puedes elegir español o inglés. La interfaz,
  los resúmenes de las actualizaciones y los metadatos hablan el mismo idioma — antes la interfaz
  iba fija en español mientras el resumen del actualizador y los metadatos seguían el idioma de la
  máquina, y podían llegar en otro. Los metadatos usan el idioma nuevo al reiniciar.
- **La ventana vuelve a donde estaba.** Posición, tamaño y estado (maximizada o no) sobreviven al
  cierre por cualquier camino. Una posición guardada en un monitor que ya no está conectado se
  descarta en vez de abrir la ventana fuera de toda pantalla, y una ventana cerrada maximizada
  reabre maximizada sobre sus límites de restauración.

### Cambiado

- **La herramienta que firma las publicaciones no compilaba, y nadie podía enterarse hasta publicar.**
  Le faltaba el encabezado de licencia que este proyecto exige en cada archivo, y esa regla es un
  error de compilación — pero su proyecto **no estaba en la solución**, así que ninguna de las
  comprobaciones que se ejecutan en cada cambio llegaba a construirlo. El primer sitio donde habría
  saltado era una publicación real, en el paso que comprueba que la firma sirve. Ahora el proyecto
  está dentro, con lo que todas las comprobaciones que ya existían pasan a cubrirlo, y una prueba
  nueva falla en cuanto aparezca otro proyecto fuera.
- **Cada versión publicada lleva al lado el código fuente de las bibliotecas que reproduce el vídeo.**
  Las licencias de LibVLC y de sus complementos obligan a que ese código esté a tu alcance, y la forma
  más clara de cumplirlo es la que ellas mismas describen: ofrecerlo desde el mismo sitio del que te
  descargas el programa. Así que la versión adjunta `vlc-3.0.23.tar.xz` —comprobado contra la huella
  que publica VideoLAN— y el de LibVLCSharp, además de la oferta por escrito que ya existía, que se
  mantiene para quien reciba el programa por otra vía. De paso se corrigió que el aviso nombraba una
  versión de VLC que no existe: el `3.0.23.1` del paquete es su propia numeración, y el código fuente
  se publica como `3.0.23`.
- **Y si el tráiler no lo tienes, la ficha te lo abre en el navegador.** Cuando TMDB conoce uno,
  aparece un botón en la ficha de la película o de la serie que lo abre en tu navegador de siempre.
  **La aplicación no se conecta a YouTube**: le pasa la dirección a Windows y quien entra es tu
  navegador, con tus ajustes y tus extensiones — por eso la lista de conexiones que esta aplicación
  declara no crece ni un host. Lo que se guarda es la clave del vídeo y nunca una dirección, y sólo
  una clave con la forma exacta que usa YouTube llega a componer un enlace: cualquier otra cosa no
  ofrece botón. El dato viaja en la misma consulta de metadatos que ya se hacía, así que no hay una
  petición más. Dentro de la aplicación **no se reproduce**, y no es una limitación técnica: hacerlo
  incumpliría los términos de YouTube.
- **Si tienes el tráiler junto a la película, la ficha lo reproduce.** Sirve la convención que ya
  usan Plex, Jellyfin y Kodi: un archivo `<película>-trailer.<extensión>` al lado, o una carpeta
  `Trailers` dentro de la de la película. El botón sólo aparece cuando ese archivo existe de verdad,
  se abre igual que un vídeo que arrastras a la aplicación —sin añadirlo a tu biblioteca— y sólo
  acepta los contenedores que esta versión reproduce. **No se descarga nada**: si tu tráiler está en
  YouTube, eso no es un archivo tuyo y no se reproduce aquí dentro.
- **La ficha de una película o una serie muestra su sinopsis.** El resumen ya se descargaba, se
  guardaba, se fusionaba respetando lo que hubieras bloqueado y se podía editar a mano — pero no se
  leía en ninguna parte: podías escribirlo en el editor y no encontrarlo después. Ahora aparece en
  las dos fichas, envuelto y acotado para que no empuje a las versiones o a las temporadas fuera de
  la pantalla, y anunciado con su nombre para quien use un lector de pantalla. Un resumen en blanco
  no ocupa sitio. No se abre ninguna conexión nueva: el texto ya estaba en tu disco.

- **Renombrar archivos no proponía ningún nombre, y ahora se sabe por qué.** La vista previa de
  renombrado se abre, enseña su casilla de confirmación y sus dos botones, y **no ofrece nunca una
  sola operación**: la aplicación pedía renombrar cada archivo al nombre que ya tenía, así que la
  comprobación de seguridad lo descartaba, correctamente, como «sin cambios». Falta la pieza que
  decide cómo debe llamarse un archivo a partir de su ficha, y eso es una decisión de producto, no un
  cable suelto: queda anotada en vez de inventada. Nada de tu disco se tocó ni se toca.

- **Los mandos del reproductor ya se pueden usar con el ratón.** Tres cosas distintas los tenían
  inservibles, y las tres se veían igual: un botón en pantalla que no hacía nada. El aviso de estado
  del vídeo —el que dice si va por hardware o si el rango es estándar— **ocupaba toda la superficie
  del reproductor**, opaco, encima del vídeo y encima de la barra de mandos, así que se tragaba cada
  clic; ahora es un distintivo en una esquina. Los botones no volvían a comprobar si podían usarse,
  de modo que **pausabas con el ratón y ya no podías reanudar**. Y el deslizador de volumen se movía
  sin llegar a la reproducción: cambiaba el número en pantalla y nada que pudieras oír. Todo esto
  seguía funcionando con el teclado, que es exactamente por qué nadie lo había visto.

- **Windows ya describe la aplicación en tu idioma antes de que la abras.** El paquete declaraba
  español e inglés desde el principio, pero su descripción era **una sola frase con una barra en
  medio** —«Biblioteca y reproductor de vídeo local / Local video library and player»—, que Windows
  enseñaba igual a todo el mundo: declarar un idioma no traduce nada por sí solo. Ahora el paquete
  lleva un texto por idioma y Windows elige el tuyo. El texto no está escrito a mano en ninguna
  parte: sale del primer párrafo del README correspondiente, que es de donde ya salía el de winget,
  así que las dos vías de instalación dicen exactamente lo mismo. El nombre no se traduce, a
  propósito: «AP Reelume» es el nombre del producto en los dos idiomas.

- **Una puerta cuenta cuántos botones de la aplicación se pulsan de verdad con el ratón.** Hasta
  ahora el recorrido automático conducía la aplicación con el teclado y **dos** de sus 129 controles
  de mando llegaban a recibir un clic; el resto podía estar visible, activo e incapaz de hacer nada
  sin que nada avisara — que es exactamente lo que le pasó a un par de botones que sobrevivieron a
  una auditoría entera. Ahora hay un inventario de los 129, una lista de lo que aún no se pulsa
  **con el motivo escrito al lado**, y un trinquete: esa lista sólo puede encoger. La primera tanda
  cubre la biblioteca y la ficha —filtrar, ordenar, aplicar, abrir una entrada, volver, marcar
  visto, favorito, ver más tarde, puntuar y reproducir—, y un control sólo cuenta cuando se ha
  pulsado de verdad, se ha comprobado **lo que cambió** y un clic al lado no ha hecho nada. Lo que
  se cuenta se anota mientras se ejecuta, no leyendo el código de las pruebas. Al estrenarla
  aparecieron **tres** defectos del propio arnés: no sabía distinguir dos botones Atrás idénticos y
  sólo uno visible; el mismo botón se pulsaba con un nombre y se contaba pendiente con otro; y el
  clic de comprobación que debía no hacer nada **apagaba el botón de favorito** de la fila de
  encima, sin que nada avisara.

- **La puerta de cobertura ya vigila código que no es nuevo.** Sólo miraba los archivos que
  aparecían por primera vez, así que uno antiguo que empeorase no lo miraba nadie — y no es una
  hipótesis: al re-medir los tres archivos que arrastraban deuda, dos estaban igual que hace un día
  y el tercero había **retrocedido** quince puntos, porque una limpieza anterior le quitó código y
  se llevó por delante justo las partes que sí estaban probadas. Nada avisó. Ahora hay una lista
  explícita de archivos vigilados que se miden siempre, cada uno con el listón que cumple hoy: si
  baja, la verificación falla; si sube, **también** falla hasta que se anota el nuevo listón, de
  modo que una deuda saldada no puede volver en silencio. Dos de los tres quedaron cubiertos por
  completo por el camino; el tercero queda vigilado con su nombre y su número a la vista en cada
  ejecución.
- **La última deuda de cobertura está saldada.** Al que reconcilia lo que un escaneo encuentra —el
  que hace que un vídeo cambiado de carpeta siga siendo la misma ficha— le estaba probado el camino
  feliz y no sus decisiones: qué se niega a tocar, qué cuenta como fallo sin costarle el escaneo al
  resto, y qué guarda. Ahora están probadas una a una, y el archivo pasa de 86,73 % de líneas y
  76,00 % de ramas al 100 % de las dos, con el listón subido detrás. Medir la lista de huecos antes
  de escribir nada la recortó en un tercio —cinco de los puntos anotados leyendo el código ya
  estaban cubiertos— y destapó uno que ninguna lectura habría encontrado: el contador de archivos
  intentados no lo leía ninguna prueba.
- **La ventana ya no espera a que la base de datos esté lista para existir.** Al arrancar, la
  aplicación pone al día la base —y comprueba su integridad si eso reescribió el archivo— y hasta
  ahora hacía ese trabajo en el mismo hilo que dibuja: no había nada en pantalla hasta que
  terminaba, y en una biblioteca grande la comprobación crece con el archivo. Ahora la ventana
  aparece de inmediato con una pantalla de inicio, el trabajo ocurre aparte, y al acabar su sitio lo
  ocupa la biblioteca o, si la base no se puede abrir, la misma pantalla de recuperación de siempre:
  cambia cuándo se decide, no qué se decide. No hay barra de progreso, y es a propósito — nada en
  ese momento sabe cuánto falta, y una barra que se mueve sin significar nada es una imagen de
  progreso en lugar de progreso. La medición que lo decidió: escribir «espera» en el código no basta
  para que el hilo quede libre, así que se comprobó, y no quedaba libre ni un milisegundo.
- **La verificación del paquete ya dice cuánto tardó la ventana en aparecer, no sólo que apareció.**
  Anotaba un sí o un no, y ese sí cubría por igual un arranque instantáneo y uno que llegó justo
  antes de agotar el plazo de noventa segundos, así que una degradación no se veía hasta ser un
  fallo. Ahora informa el tiempo —desde antes de arrancar el proceso, porque el arranque también se
  espera— en los tres ciclos que abren la aplicación, y una prueba lo exige y lo acota contra ese
  plazo. Tres cifras y no una, porque un número solo no dice si el que empeoró fue el arranque o la
  máquina. La primera medición dejó dos cosas claras: los cinco ciclos migran una base nueva, no
  sólo el primero como estaba escrito, y el primer arranque tampoco es el más lento de los tres.
- **Catalogar y reproducir comparten el motor nativo, en lugar de arrancar uno cada uno.** Leer los
  datos técnicos de un archivo levantaba su propia instancia de LibVLC con las mismas opciones que la
  de reproducción, así que un proceso que catalogaba y reproducía mantenía dos motores nativos
  abiertos — y el contador que dice «uno por juego de opciones» no podía ver el segundo. Ahora hay un
  dueño y una prueba que falla si aparece otro. De paso desaparece la segunda cola de liberación de
  medios: la que tenía el sondeo no protegía su propio cierre, de modo que un único fallo al liberar
  habría dejado su trabajador muerto para siempre y todo lo catalogado después se habría ido
  filtrando sin que nada avisara. La que queda ya vive protegida contra eso.
- **El compilador vigila que un número guardado no dependa del idioma del sistema.** Un tamaño, una
  fecha o una comparación escritos con las reglas del idioma de quien usa el programa se leen mal en
  otra máquina, y ese error no avisa: aparece cuando ya está guardado. Tres comprobaciones que venían
  apagadas quedan encendidas como error. No hubo nada que corregir —se midió antes: cero casos en
  todo el proyecto—, y ese cero se comprobó compilando una violación deliberada de cada regla para
  saber que las comprobaciones se estaban ejecutando de verdad.
- **El actualizador se presenta con la versión que de verdad tienes.** Al preguntar si hay una
  versión nueva se identificaba como «1.0», un número escrito a mano que nunca existió: la versión
  declarada es 0.1.0. Ahora sale del propio programa, y una prueba la compara contra el único sitio
  donde este proyecto declara su versión, leyendo la cabecera que sale de verdad y no la constante
  del código.
- **Las pruebas encuentran la raíz del proyecto en un solo sitio.** El mismo recorrido hacia arriba
  estaba pegado en cincuenta y nueve archivos, y ni siquiera era el mismo: dos de ellos buscaban un
  documento y el resto el archivo de solución, así que el repositorio tenía dos definiciones de su
  propia raíz. Ahora hay una, compartida, y una prueba que falla si alguien vuelve a escribir la
  suya. Ochocientas líneas menos.
- **La prueba que vigila que ninguna pantalla quede inalcanzable ya no se cree un comentario.**
  Buscaba el nombre de la vista en el texto de los archivos, así que una referencia **comentada**
  contaba como si la pantalla se pudiera abrir: justo la pantalla huérfana que esa prueba existe para
  encontrar podía esconderse detrás de un comentario y la puerta seguía en verde. Ahora se quitan los
  comentarios antes de buscar. Se comprobó primero si algo se estaba escondiendo ya de esa forma —no
  lo había—, y el recorte se hizo hacia el lado seguro: quitar de más pierde una referencia y produce
  un aviso ruidoso, nunca un permiso silencioso.
- **Y el reproductor tampoco guarda ya la suya.** Quedaba una tercera cola, la del propio motor de
  vídeo, con el mismo desecho sin proteger: un solo fallo al liberar habría acabado con su trabajador
  y todo lo abierto a partir de ahí se habría filtrado en silencio. Ahora hay **una** cola para todo
  el proceso, la que ya vive protegida. Cerrar el reproductor espera a que sus vídeos estén sueltos
  antes de devolverlo —ese orden es lo que impide que la destrucción nativa se lleve el proceso por
  delante— y esa espera tiene techo, así que una biblioteca ocupada catalogando no puede retener una
  salida. El segundo de reposo antes de soltar un vídeo, que es el número que dejó de hacer crashear,
  no se ha tocado.
- **Un arranque que no llega a pintar ya deja diagnóstico en vez de un código de salida mudo.** La
  verificación mata el proceso cuando agota el plazo de la ventana, y lo único que quedaba escrito
  era ese matarile —`exit code -1`—, que no habla del arranque. Ahora, **antes** de matarlo, la
  verificación anota si el proceso seguía vivo, cuánto procesador había gastado y en cuántos hilos
  —que es lo que separa girar de esperar—, si la base de datos existe y por cuántas migraciones ha
  pasado, y qué hay en la carpeta de datos. Todo eso va en la misma línea que CI imprime al fallar.
  Ninguna de esas lecturas puede romper nada: un diagnóstico que falla sustituiría al fallo que venía
  a explicar, así que lo que salga mal se cuenta dentro de la propia frase. No se ha subido el plazo
  de noventa segundos, que sería convertir la única señal que hay en silencio.
- **Al salir, la aplicación suelta lo que había tomado.** El reproductor nativo, la base de datos, el
  icono de bandeja, los registros de teclas multimedia y los clientes de red vivían en un campo
  estático que nada liberaba nunca: el proceso terminaba y le dejaba a Windows recoger lo suyo, que no
  es cerrar sino confiar. Ahora la aplicación es un objeto con dueño y se libera al salir, venga la
  salida de la ventana o de la bandeja. Tiene un segundo efecto que se nota en las pruebas más que en
  la pantalla: dos aplicaciones pueden existir a la vez en un proceso sin verse la una a la otra, y la
  cláusula que obligaba a las dos suites de recorrido completo a ejecutarse en fila se retiró — que es
  la manera de comprobar que la propiedad es real y no sólo más ordenada. Lo que sigue sin liberarse,
  a propósito y documentado, es la instancia nativa de LibVLC: crearla y destruirla repetidamente es
  un modo de fallo conocido, así que vive lo que vive el proceso.
- **El registro de la aplicación deja de ser una lista de trescientas líneas.** Todo lo que la
  aplicación monta se declaraba en una sola cadena, y averiguar de qué dependía una pieza obligaba a
  leerla entera. Ahora hay ocho módulos por área —datos, reproducción, personalización, biblioteca,
  ajustes y copias, actualizaciones, apariencia e identificación— cada uno lo bastante corto como para
  que una pieza que falta se vea. El comportamiento es el mismo: lo garantizan las pruebas que
  recorren la aplicación ensamblada de verdad.
- **La lógica que elige qué copia ofrecerte cuando la biblioteca no abre ya se puede medir.** Vivía
  dentro del archivo de composición, donde la única forma de alcanzarla era hacer fallar una base de
  datos real; decide qué se te ofrece el peor día que tiene tu biblioteca. Ahora es una pieza aparte
  con cinco pruebas, entre ellas las dos que antes nadie comprobaba: que una copia que el registro
  nombra pero que no está en el disco no se ofrezca, y que la copia de otra base de datos no se
  confunda con la tuya.

### Legal

- **Créditos muestra el logotipo de TMDB, no sólo su nombre.** Sus términos piden identificar el uso
  de TMDB con su marca, menos prominente que la del propio producto; era el último punto de esos
  términos que quedaba abierto. El archivo es el que TMDB publica y se puede comprobar que lo es: la
  huella SHA-256 que ellos incrustan en la dirección del recurso coincide con la del archivo
  versionado, y una prueba las compara. Lo que se dibuja es su vector, no una imitación —otra prueba
  contrasta la geometría de la vista contra la del archivo carácter a carácter—, y se dibuja a 16 px
  frente a los 24 px del nombre del producto. La especificación decía 48 px para ese nombre; se midió
  y no existía, así que se corrigió la cifra en vez de heredarla. Lleva texto alternativo en los dos
  idiomas y no es un enlace.
- **El texto de cada licencia ajena viaja dentro del paquete.** Nombrar un componente y su licencia no
  es entregar la licencia, y varias lo exigen: la LGPL-2.1, la GPL-2.0 y la Apache-2.0 piden acompañar
  una copia con el binario, y la MIT y la BSD-3-Clause, reproducir su aviso de copyright. El paquete de
  VideoLAN no trae ninguno, así que nadie los aportaba. La carpeta `licenses/` de los dos artefactos
  lleva ahora los cinco textos íntegros y los avisos de ANGLE, Skia, HarfBuzz, BouncyCastle,
  SQLitePCLRaw, SQLite y VideoLAN —incluido el archivo de Microsoft que cubre las veintitantas
  bibliotecas que Skia y HarfBuzz llevan dentro, de freetype a zlib, que hasta ahora no aparecían en
  ningún sitio—. Los avisos que un paquete publica se copian de él y una prueba los compara byte a byte
  contra el paquete que la compilación consumió, de modo que una subida de versión que cambie un aviso
  se pone en rojo en vez de distribuir en silencio el aviso anterior. Los textos canónicos se tomaron
  de una fuente que ya los distribuía y se contrastaron con una segunda copia independiente; el de la
  GPL-2.0 salió del propio árbol de VLC, que es la licencia que obliga a sus complementos.
- **Cada archivo fuente dice bajo qué licencia está.** La licencia vivía solo en `LICENSE`, y una
  licencia que solo vive ahí deja de estar unida al archivo en cuanto alguien lo copia fuera del
  árbol. Los 556 archivos de código, los 51 de interfaz y los 17 de compilación llevan ahora su
  cabecera `SPDX-License-Identifier: GPL-3.0-or-later` junto al titular del copyright, y la puerta de
  formato que ya se ejecutaba rechaza un archivo nuevo que llegue sin ella.
- **Los avisos de terceros nombran lo que el paquete lleva de verdad.** Listaban ocho componentes —
  los que alguien recordaba haber pedido— mientras el artefacto transportaba treinta, entre ellos
  ANGLE bajo BSD-3-Clause, Skia, HarfBuzz, BouncyCastle y el propio motor de .NET, todos con avisos
  que deben viajar con el binario. Ahora la lista sale del inventario real de la compilación, explica
  aparte el motor autocontenido y los complementos de VideoLAN, y una prueba impide que una
  dependencia entre en el artefacto sin aparecer en los avisos de los dos idiomas.
- **La atribución de TMDB dice la frase que TMDB exige.** Mostraba un resumen de la frase obligatoria;
  ahora dice la exigida —«usa TMDB y las API de TMDB, pero no está avalado, certificado ni aprobado de
  ningún otro modo»— en Créditos, en el aviso y en los dos README, con una prueba que la fija palabra
  por palabra en ambos idiomas.
- **Nada de TMDB se conserva más de seis meses.** Sus términos lo prohíben y la caducidad de la caché
  no lo garantizaba: cuando la red fallaba o usted quitaba el token, el programa seguía sirviendo la
  copia guardada por vieja que fuera. Ahora hay un suelo duro de 180 días; pasado ese plazo la entrada
  no se sirve y se borra del disco.
- **Los complementos de VideoLAN quedan confirmados como compatibles.** Estaba anotado como duda para
  un dictamen: si alguno fuera `GPL-2.0-only` chocaría con la licencia de este programa. Se comprobó
  en la fuente y llevan la cláusula «o cualquier versión posterior», así que encajan. En el mismo
  repaso apareció lo contrario de una buena noticia: el paquete de VideoLAN no trae ningún archivo de
  licencia, y el artefacto tampoco incluía el texto de las licencias ajenas, que varias de ellas
  exigen acompañar. Ese hueco es el que cierra la primera entrada de esta sección.
- **Un estado legal que dice también lo que falta.** Una página nueva en los dos idiomas reúne la
  licencia, el descargo de garantía, los terceros, los términos de TMDB y de GitHub y la nota de
  exportación por la criptografía que el paquete lleva, y nombra sin adornos los cinco puntos que
  siguen siendo del propietario, entre ellos el dictamen jurídico profesional.

### Seguridad

- **Reproducir fuera de la aplicación sólo abre vídeo.** El botón que entrega un archivo al
  reproductor que tengas registrado en Windows confiaba en que quien lo llamara ya hubiese filtrado
  por la lista de contenedores. Se midió qué pasaba si no: de cinco tipos de archivo que la biblioteca
  no cataloga, **tres abrieron su manejador**, entre ellos un `.ps1` y un archivo sin extensión. Ahora
  la comprobación está donde se hace la llamada, contra la misma lista que decide qué entra en tu
  biblioteca.
- **Restaurar una copia aplica sus propios límites de tamaño.** Los topes —512 MB por archivo, 2 GB en
  total— los ponía el paso de inspección, que es un paso que hay que acordarse de dar; desempaquetar
  ahora los aplica también. De paso se comprobó, forjando un archivo que declara un byte donde guarda
  una base de datos entera, que ninguna entrada puede entregar más de lo que declara: el riesgo estaba
  en la declaración, no en la copia, y ahí es donde se corta.
- **La integración continua ya no puede salir a una máquina personal.** Cuando el repositorio era
  privado, la verificación podía enrutarse a un runner propio a través de una variable del
  repositorio; ahora que es público, esa puerta era una invitación a que el CI de un pull request
  ajeno se ejecutara en la máquina del propietario en cuanto la variable reapareciera. El flujo de
  trabajo ya no la consulta y corre siempre en los runners hospedados, que son gratuitos en los
  repositorios públicos.
- **Las tres acciones de la tubería suben a su versión mayor vigente.** `checkout` 7.0.1,
  `setup-dotnet` 6.0.0 y `upload-artifact` 7.0.1, con cada SHA comprobado contra la etiqueta que dice
  representar. El cambio de ruptura de `checkout` endurece precisamente los disparadores que este
  proyecto no usa, y el de `upload-artifact` sólo altera el nombrado cuando se desactiva el
  empaquetado, que aquí no se desactiva.
- **La única herramienta externa que sella la publicación queda fijada por versión.** Todas las
  acciones ya iban ancladas por SHA y NuGet en modo bloqueado, pero ffmpeg se instalaba desde el
  canal de la comunidad tomando siempre la última versión: era el único ejecutable de terceros sin
  fijar en la máquina que empaqueta y firma. Ahora se instala una versión concreta que sólo se mueve
  por una edición deliberada, igual que los SHA de las acciones.
- **Las huellas publicadas van firmadas y el actualizador exige la firma.** El hash con el que se
  comprueba una actualización viajaba en la misma respuesta sin firmar que el paquete al que avala:
  quien alterase la respuesta podía alterar ambos a la vez. Ahora cada publicación firma sus
  huellas con una clave minisign cuya mitad pública viaja dentro del binario; el actualizador
  verifica esa firma antes de creer ningún hash, lee el hash únicamente del bloque firmado, y una
  versión sin firma —o con las líneas alteradas— se rechaza diciendo por qué. La clave privada vive
  fuera del repositorio, la tubería de publicación firma y se niega a publicar sin firma, y la
  declaración de privacidad explica qué prueba esta capa y qué sigue sin probar (la firma de código
  de Windows es otra capa y sigue pendiente de su decisión económica).
- **El actualizador sólo acepta bytes de los dominios declarados, en cada salto.** Las
  redirecciones ya exigían HTTPS pero aceptaban cualquier destino; ahora cada salto tiene que
  quedar dentro de la lista que la declaración de privacidad publica (GitHub y su almacenamiento),
  un salto fuera se rechaza con su motivo en pantalla, y la declaración nombra por fin el dominio
  de almacenamiento al que GitHub redirige — con una prueba que impide que código y promesa vuelvan
  a divergir. El arte de los títulos queda igualmente limitado a su dominio declarado.
- **Toda respuesta de red tiene techo.** Los metadatos de una versión se cortan en un megabyte, el
  paquete se corta en cuanto llegan más bytes de los que la versión declaró (y el parcial
  envenenado se borra), y un póster de más de diez megabytes se rehúsa a medio camino conservando
  el arte anterior.

### Corregido

- **La verificación automática ya pulsa los ajustes con el ratón.** La página de ajustes es más alta
  que la ventana, y el recorrido que conduce la aplicación construida sólo sabía bajar por ella: una
  vez pulsado algo, todo lo que quedaba más arriba dejaba de alcanzarse. Ahora vuelve al principio de
  la página y sólo se desplaza cuando hace falta, así que cada pulsación vale por sí sola sin
  depender de cuál se hizo antes. Con eso quedan comprobados con el ratón el tema claro, el oscuro,
  el del sistema, los dos idiomas, la vigilancia de carpetas locales y la detección de segmentos: se
  pulsan y se comprueba que la preferencia cambia de verdad — y, antes, que pulsar al lado no cambia
  nada.
- **El renombrado ya renombra.** Proponía el nombre que el archivo ya tenía, así que la
  previsualización salía siempre vacía y los botones de Renombrar y Deshacer no podían hacer nada por
  mucho que se pulsaran. Ahora propone el nombre que la ficha merece, con el convenio que leen Plex,
  Jellyfin y Kodi: `Título (Año).ext` para una película y `Serie (Año) - SxxEyy - Título.ext` para un
  episodio. Sobre las doce formas de nombre que el catálogo reconoce, ocho reciben un nombre distinto
  del actual y la que ya seguía el convenio se deja en paz. Cuando nadie ha identificado la ficha y el
  nombre no se deja leer con seguridad, no se propone nada: un renombrado no adivina. Lo que decide
  qué caracteres son seguros y qué hacer con dos archivos que quieren llamarse igual no ha cambiado.
- **Una carpeta vigilada ya no deja de vigilarse justo cuando le llegan muchos archivos.** Cuando
  Windows avisaba de que había perdido cambios —lo que pasa al copiar una temporada entera de golpe—,
  la vigilancia en vivo de esa carpeta terminaba en silencio y no volvía hasta el siguiente arranque
  de la aplicación. No se perdía ningún archivo, porque se lanzaba un repaso completo, pero a partir
  de ahí lo que se añadiera tardaba en aparecer hasta el repaso siguiente. Ahora un aviso de esa
  clase significa «repasa la carpeta entera y sigue vigilando», el espacio que Windows usa para
  avisar se pide al máximo que permite, y una vigilancia que se cae de verdad —un disco desconectado,
  una carpeta de red que deja de contestar— se reintenta sola en el siguiente repaso.
- **La verificación automática ya pulsa botones con el ratón.** El recorrido que conduce la
  aplicación construida sólo usaba el teclado, y por ese hueco pasaron unos botones que estaban a la
  vista y no hacían nada. Ahora abre la ficha de una película identificada, pulsa «Actualizar desde
  el proveedor» con el ratón y comprueba que la ficha cambia — y, antes, que pulsar al lado no cambia
  nada, para que el resultado no pueda deberse a otra cosa.
- **Los dos botones del proveedor en el editor ya hacen algo.** «Actualizar desde el proveedor» y
  «Restaurar campos del proveedor» estaban visibles y activos, y no podían funcionar: esperaban unos
  datos que sólo una prueba les daba. Ahora el refresco averigua por sí mismo a qué ficha del
  proveedor corresponde el título, con lo que quedó guardado al identificarlo. Y cuando no puede
  hacer nada, lo dice: una ficha sin identificar y un proveedor sin respuesta son cosas distintas, y
  ninguna es un error.
- **Guardar en una ficha recién añadida ya guarda.** Editar por primera vez un título que nunca se
  había tocado no creaba su ficha, así que el botón Guardar se pulsaba y no ocurría nada, sin aviso.
- **Dos ventanas editando la misma ficha ya no pueden ganar las dos.** La comprobación que impide
  pisar el trabajo ajeno comparaba contra un número que nunca cambiaba, de modo que la segunda
  ventana sobrescribía a la primera en silencio. Ahora la segunda recibe el aviso de que su copia
  está anticuada, que es lo que siempre debió pasar.
- **Identificar una película ahora cambia lo que la biblioteca enseña.** Hasta aquí no lo cambiaba:
  aceptar una coincidencia en la bandeja marcaba la revisión y nada más, de modo que la ficha seguía
  mostrando lo que el analizador sacó del nombre del archivo. La sinopsis y la clave de tráiler
  existían de punta a punta y sólo se rellenaban escribiéndolas a mano. Ahora una identificación
  —aceptada por ti, o automática cuando la confianza pasa del 90 %— pide los datos al proveedor y los
  guarda junto con de quién son y cuándo se pidieron. **Lo que hayas bloqueado sigue ganando**: la
  identificación se funde con lo que ya hubiera, igual que un refresco manual. Sin conexión
  consentida el proveedor sólo sirve lo que tenga guardado, así que una biblioteca sin permiso de red
  se queda exactamente como estaba, que no es un error.
- **Una verificación que se colgaba ya no se lleva una hora en silencio.** Seis de las diez
  ejecuciones de integración continua del 10 de agosto murieron al llegar al techo de sesenta
  minutos, y el registro no decía nada entre «compilación correcta» y la cancelación cincuenta y seis
  minutos después. El paso siguiente genera la matriz de contenedores con FFmpeg y lo arrancaba sin
  ningún tope: un encode atascado se comía el trabajo entero y se reportaba como un hipo de
  infraestructura. Ahora cada llamada al codificador tiene techo, cada muestra se anuncia antes de
  producirse y un proceso que no vuelve se mata nombrando la receta en la que estaba. Producir las
  dieciséis muestras cuesta 1,6 segundos, así que ese techo no es un presupuesto de rendimiento: es
  la diferencia entre un fallo con nombre y un trabajo que se muere sin decir por qué.
- **Y con el nombre delante, la receta que se atascaba.** La primera ejecución con el techo puesto
  falló en cuatro minutos en vez de morir a los sesenta, y señaló a `mkv-dual-audio-english-first`.
  Las once recetas que terminaban con `-shortest` pasan a fijar la duración de salida de forma
  explícita: `-shortest` tiene un bloqueo documentado cuando la mezcla de flujos y el vaciado se
  cruzan, que es justo lo que hacía que unas ejecuciones tardaran veintidós minutos y otras se
  colgaran para siempre. Todas las entradas ya duraban tres segundos, así que el resultado es el
  mismo archivo: las 114 pruebas de la matriz de contenedores lo confirman propiedad por propiedad.
- **Las preferencias de reproducción guardadas se aplican de verdad.** La pista de audio y los
  subtítulos que elegiste — por archivo, por serie o globales, con repliegue por idioma cuando una
  pista no está — se resolvían y nunca se aplicaban: cada sesión abría con lo que el motor
  eligiera. Ahora se aplican en cuanto el vídeo abre, y el selector de pistas muestra lo
  efectivamente aplicado. De paso, seis registros muertos del contenedor (duplicados de lo que la
  aplicación construye por otra vía) quedan retirados en vez de en silencio.
- **Elegir el dispositivo de salida de audio ahora cambia dónde suena.** El selector existía y el
  motor no se enteraba: tu elección se guardaba y el audio seguía saliendo por donde quisiera VLC.
  Ahora elegir un dispositivo pausa, cambia la ruta y reanuda — sin reiniciar el vídeo —, la
  elección global guardada se aplica al abrir cada sesión, y un dispositivo que desaparece a mitad
  jamás corta la reproducción: se repliega al predeterminado sin olvidar tu preferencia.
- **Cambiar de versión sin salir de la reproducción.** Si el título que estás viendo tiene más de
  una versión (otra resolución, otro códec, HDR), el reproductor las lista y puedes saltar a otra
  en plena sesión. Tu posición se guarda antes de nada; si las duraciones no permiten trasladar el
  punto con seguridad, la aplicación te lo pregunta con el segundo propuesto a la vista — continuar
  ahí, empezar de nuevo, o cancelar. Una versión que no está disponible no se ofrece como abierta.
- **Mover un vídeo de carpeta ya no le hace perder su historia.** Cada escaneo captura una
  identidad ligera de lo que cataloga (el identificador estable del disco y una huella acotada del
  contenido) y reconcilia: un archivo que apareció en una ruta nueva y desapareció de la vieja
  vuelve a ser la misma entrada, con su progreso y tus decisiones intactos. Una copia que convive
  con la original sigue tratándose como copia (versiones, como siempre). Y cuando hay duda — dos
  copias conocidas y una nueva idéntica — la bandeja de revisión te pregunta: «es el mismo,
  reasignar» conserva la historia bajo la ruta nueva; «es un archivo nuevo» lo deja como entrada
  propia. La oferta reaparece en cada escaneo hasta que decidas; nada se decide en silencio.
- **Una carpeta puede retirarse de la biblioteca.** La biblioteca lista por fin sus carpetas, y
  cada una tiene una acción de retirada con confirmación que dice la verdad: la carpeta se retira
  del catálogo, ningún vídeo del disco se toca, y si vuelves a añadirla se cataloga de nuevo. El
  catálogo en pantalla se recarga al momento.
- **Marcar algo como visto ahora se guarda de verdad.** El conmutador de «visto» de la ficha se
  construía sin manejador: cada marca iba a ninguna parte y la tarjeta la olvidaba al recargar.
  Ahora una decisión tuya se guarda como manual — nada que el reproductor calcule después la
  cambia — y quitarla devuelve el estado a las reglas automáticas. Además, el umbral de «visto»
  (qué porcentaje hay que alcanzar) se configura por fin en los ajustes de recomendaciones, entre
  el 50 y el 100 %; moverlo recalcula sólo los estados automáticos y te dice cuántos movió.
- **Un vídeo que no abre ya no arrastra consigo a las preferencias.** Aplicar las preferencias
  daba por hecha una sesión viva: con un archivo que el motor no pudo abrir, la selección de pista
  estallaba por dentro después de que la pantalla ya mostrara el diagnóstico. Ahora una sesión que
  no abrió, o que se cerró debajo, simplemente no tiene nada que aplicar; cualquier otro fallo
  sigue avisando.
- **Renombrar con el archivo abierto en otro programa ya dice qué hacer.** El fallo se guardaba en
  la auditoría como «IOException» y la pantalla no decía nada: el renombrado simplemente no ocurría.
  Ahora la superficie dice si otro programa tiene el archivo abierto, si Windows denegó el permiso o
  si la unidad falló — cada caso con su acción — y la auditoría guarda el motivo con nombre útil.
- **El diagnóstico dice lo que tu máquina hizo, no lo que una constante prometía.** La aceleración
  de vídeo informada es la que el motor usó de verdad en el último vídeo, el tamaño de la
  biblioteca es el real (en tramos, como siempre), y los errores son los que la aplicación registró
  — sin rutas ni nombres de archivo, como exige la lista de lo permitido.
- **El manual explica la entrada de arranque huérfana.** Si desinstalas con «iniciar con Windows»
  activado, queda una entrada inocua en el registro; el manual dice por qué no hace nada, cómo
  quitarla a mano y que reinstalar la repara sola.
- **La pantalla sigue al motor durante toda la sesión.** El estado en pantalla sólo cambiaba al
  abrir: la pausa pausaba el motor pero la interfaz seguía diciendo «reproduciendo» para siempre,
  con los controles de reanudar inalcanzables. El método que aplicaba las transiciones existía y
  estaba probado; en la aplicación ensamblada nadie lo llamaba. Lo encontró el paseo físico del
  artefacto empaquetado —tres escenas re-ejecutables con disco, SQLite y decodificación reales:
  vigilancia catalogando un archivo soltado y agrupando dos copias, las teclas operando un vídeo en
  reproducción, y dos episodios encadenándose solos— que queda como guardia permanente.
- **Rutas largas y escalado por monitor, declarados en vez de heredados.** La aplicación no traía
  manifiesto propio: el límite de 260 caracteres seguía aplicándose aunque Windows ya lo hubiera
  levantado —una biblioteca en una carpeta profunda perdía archivos en silencio— y la conciencia de
  escalado era la que el runtime adivinara. Ahora ambas cosas están escritas en el manifiesto del
  proceso.
- **Una migración reescrita ya no pasa desapercibida, y la integridad se pregunta una vez.** El
  arranque sólo comparaba números de versión: si el texto de una migración aplicada cambiaba, el
  esquema del disco y el que el código asume divergían en silencio. Ahora cada checksum guardado se
  compara con el del build y una discrepancia se rehúsa con nombre y apellidos. De paso, la
  comprobación de integridad —la parte más lenta de abrir una biblioteca grande— corre una vez por
  arranque, no dos.
- **Una detección ya no puede salirse de su episodio.** Las marcas manuales siempre validaron
  contra la duración; las detectadas se juzgaban a ciegas. Ahora el detector recorta lo que emite
  al episodio en que lo midió y la política aplica a las detecciones la misma regla que a las
  marcas manuales.
- **El manual dice qué pasa con sus datos al desinstalar.** Nada se borra: catálogo, progreso y
  copias siguen en su carpeta y una reinstalación los reencuentra; el manual explica también cómo
  borrarlo todo de verdad.
- **El coordinador de ventanas del reproductor tenía dos dueños.** Estaba registrado en el
  contenedor de servicios y a la vez construido a mano por la vista principal: dos instancias, una
  de ellas guardando geometría que nadie leería. La vista —que es quien posee la ventana del mini
  reproductor— queda como único dueño y el registro muerto se retira.
- **Una marca creada durante la reproducción no funcionaba hasta reabrir el episodio.** Las marcas
  de la sesión eran una foto tomada al abrir: guardar, borrar, aceptar o corregir una marca
  cambiaba lo almacenado y el botón de saltar seguía leyendo la foto vieja. Ahora cada cambio
  recompone las marcas de la sesión al momento, así que el botón aparece (o desaparece) sin cerrar
  nada.
- **Dos copias de la misma película nunca se agrupaban solas.** La agrupación de versiones existía
  con su repositorio, su política conservadora y sus pruebas, y nada la invocaba: los grupos sólo
  se creaban en los tests. Ahora cada escaneo agrupa las copias que su nombre declara iguales, una
  diferencia notable de duración espera confirmación en vez de agruparse en silencio, la
  preferencia que fijes sobrevive a los reescaneos, el grupo se encuentra desde cualquiera de las
  copias, y ningún archivo se borra ni se oculta jamás.
- **Ni los atajos de teclado ni las teclas multimedia hacían nada.** Cada pieza existía —el mapa de
  atajos con sus valores de fábrica, el editor que impide conflictos, el enrutador que evita
  acciones duplicadas, el servicio de teclas multimedia— y ninguna tocaba a otra: el reproductor no
  leía el teclado y el servicio nunca se arrancaba. Ahora el reproductor responde al mapa
  compartido (espacio pausa, flechas saltan, M silencia, F pantalla completa…), las teclas
  multimedia del hardware operan la sesión mientras existe y se sueltan al cerrarla, una tecla que
  llega por dos caminos actúa una sola vez, y el editor de Ajustes edita el mismo mapa que las
  teclas leen.
- **Al terminar un episodio nunca se ofrecía el siguiente.** El motor ni siquiera se enteraba de
  que el vídeo había terminado —el estado se quedaba en «reproduciendo» para siempre—, la cuenta
  atrás probada de punta a punta no estaba registrada, y los botones del cartel no hacían nada.
  Ahora el fin del medio es un estado de verdad, terminar un episodio ofrece el siguiente con su
  cuenta atrás cancelable, «Reproducir ya» abre sin esperar, y si no hay siguiente episodio o su
  archivo ya no está, se vuelve a la ficha.
- **La vigilancia de carpetas nunca arrancaba.** El coordinador de vigilancia, el vigilante con
  amortiguación y el planificador de respaldo existían, estaban probados y nada los arrancaba: la
  aplicación sólo escaneaba al pulsar un botón. Ahora la vigilancia arranca con la ventana y se
  detiene al salir, una carpeta recién añadida se sigue desde su primer escaneo sin reiniciar, una
  raíz configurada como manual no se vigila a espaldas de su dueño, y el escaneo de respaldo para
  USB y NAS recupera eventos perdidos cada quince minutos de verdad. Además, todo escaneo —del
  vigilante o manual— entrega lo que encontró a la identificación, no sólo el manual.
- **La identificación nunca se ejecutaba, así que la bandeja de revisión estaba siempre vacía.** El
  caso de uso existía completo — analiza el nombre, puntúa candidatos, consulta el proveedor sólo si
  hace falta y guarda el resultado — y nada lo invocaba jamás. Ahora cada escaneo entrega lo que
  encontró a la identificación: lo seguro se resuelve solo, la duda aparece en la bandeja, un
  archivo ya decidido se deja en paz en todos los escaneos siguientes, y los archivos que ningún
  escaneo anterior identificó sanan en el próximo. Sin el token del proveedor todo queda local y no
  se abre ninguna conexión.
- **«Continuar donde lo dejaste» dejaba el vídeo en cero.** La decisión de reanudar se calculaba
  después de abrir el medio, nadie pasaba la posición inicial al motor (que la acepta desde siempre)
  y los botones del cartel no estaban conectados a nada. Ahora la decisión existe antes de abrir, el
  medio se abre ya en la posición guardada, y «Empezar de nuevo» busca a cero de verdad. Tres pruebas
  de ensamblado, una unitaria y una con decodificación real cubren la cadena completa.
- **La detección en segundo plano no podía pararse y sobrevivía a la salida.** El caso de uso acepta
  cancelación desde siempre y el planificador lo llamaba sin token; cerrar la aplicación podía dejar
  un proceso decodificando en segundo plano. Ahora cada detección corre bajo un token de apagado y
  salir de la aplicación la detiene, junto con el bucle de guardado de la sesión.
- **Una respuesta ilegible del origen de actualizaciones podía tumbar la aplicación al arrancar.**
  Un portal cautivo (el wifi de un hotel) responde `200` con una página de acceso; eso lanzaba una
  excepción sin traducir que, en la comprobación automática del arranque, salía por el hilo de
  interfaz. Ahora un cuerpo que no es una versión se lee como «origen inalcanzable», la pantalla de
  actualizaciones siempre aterriza en un estado, y los tres trabajos de arranque (comprobación,
  salida por bandeja, archivo suelto) observan sus excepciones en vez de entregárselas al hilo de
  interfaz.
- **El guardado periódico de la posición nunca arrancaba.** El bucle de los cinco segundos sólo lo
  invocaban las pruebas: en la aplicación escribían únicamente el cierre ordenado y el cambio de
  versión, así que un corte de luz perdía la sesión entera. Ahora la sesión arranca el bucle al
  abrir y lo cancela al cerrar, pausar escribe la posición, y cada búsqueda —del transporte, de los
  saltos o del botón de saltar— escribe el destino elegido. De paso, los manejadores de posición se
  desenganchan del motor al terminar cada sesión, en vez de acumularse uno por episodio.
- **La detección de segmentos liberaba LibVLC en el orden que estrella el proceso.** El extractor de
  huellas paraba un player aún reproduciendo, lo liberaba antes que su media y disponía el media sin
  ventana de quiescencia — las tres reglas que el propio código tiene escritas como modo de fallo
  nativo, sobre la misma instancia que usa la reproducción. Ahora sigue el mismo orden que el motor,
  con una cola de liberación diferida en la fábrica cuyo drenaje sobrevive a un fallo de liberación.
  Un simulacro de veinte ciclos con diez episodios queda como prueba permanente.
- **El empaquetado fallaba en máquinas con un solo SDK de Windows.** La búsqueda de `makeappx.exe`
  devolvía un texto en lugar de una lista cuando había exactamente una versión instalada, e indexarlo
  producía su primer carácter: el sellado intentaba ejecutar un programa llamado `C`. La búsqueda
  estaba copiada en tres scripts (empaquetar x64, empaquetar ARM64 y verificar el paquete) y las tres
  copias llevaban el mismo defecto. Es lo que rompía la verificación continua en el runner
  actualizado; en local nunca se vio porque hay dos SDK. La misma actualización de imagen retiró
  ffmpeg del runner, así que el flujo lo instala ahora explícitamente: la matriz de códecs, el corpus
  de segmentos y la fase de asociación de archivos vuelven a medirse en cada push.
- **La primera pasada completa de la suite en CI destapó siete supuestos de máquina.** Dos pruebas de
  progreso de copia eran inestables por construcción (`Progress<T>` encola sus avisos y una máquina
  cargada llega al assert antes que las últimas etapas; ahora reportan síncronamente). Las otras
  cinco dependían del equipo: sin ningún endpoint de audio el catálogo se declara bloqueado en vez de
  fallar, una muestra HDR generada sin metadatos de color declara su precondición rota, y la promesa
  de ±5 s y el presupuesto de frame se declaran fuera del alcance de un runner compartido — sus
  puertas siguen midiéndose en el arnés físico local, como siempre.
- **La declaración de privacidad no mencionaba al actualizador.** La tabla de conexiones seguía
  describiendo la aplicación anterior a T44: enumeraba los dos destinos de metadatos y negaba que
  existiera comprobación de actualizaciones, cuando el actualizador —opcional y desactivado de
  fábrica— habla con `api.github.com` y `github.com`. La tabla enumera ahora los cuatro destinos, el
  resto de documentos acota «ninguna conexión» a los metadatos, y una prueba nueva falla si el
  registro de propósitos de red y la tabla vuelven a divergir en cualquiera de los dos idiomas.
- **Diez estados de la matriz decían más de lo demostrado.** La auditoría del 2026-08-08 encontró una
  familia de un solo defecto: componentes construidos, registrados y probados que ningún camino de la
  aplicación ensamblada invoca — la identificación, la vigilancia de carpetas, la agrupación de
  duplicados, el bucle de guardado periódico, la reanudación, la cuenta atrás del siguiente episodio
  y las teclas de atajo y multimedia — más un ciclo MSIX verificado sobre una copia resellada que el
  artefacto sin firma no puede repetir. Esas filas (PRD-002, LIB-002/003/006/007/008, PLY-008/011/014
  y REL-003) vuelven a `IMPLEMENTED`, cada una con su bloqueo, responsable y condición de desbloqueo
  en el manifiesto de verificación. La evidencia por componente sigue siendo válida; lo que faltaba
  era el ensamblaje, y ahora el registro lo dice.
  pero ningún camino de la aplicación pedía nunca una comprobación automática: sólo se comprobaba al
  pulsar el botón. Lo encontró la verificación física de T44, no las pruebas.
- **El actualizador consultaba un repositorio que no existe.** Pedía `ap-solutions/ap-reelume` en vez
  de `apvisualsolutions/ap-reelume`; GitHub habría respondido 404 y la aplicación habría dicho «ya
  tienes la versión más reciente» indefinidamente. Ahora una prueba compara esa dirección con la que
  publican los changelogs.
- **Un resumen que empezara por un subtítulo llegaba vacío,** porque el lector de secciones cortaba
  en `###` además de en `##`. Una versión así no se habría ofrecido a nadie.
- **El aviso de que Windows no abrió el paquete decía «puedes intentarlo otra vez».** En una máquina
  sin instalador de aplicaciones, reintentar no funciona nunca. Ahora dice que el archivo está
  descargado y comprobado, y cómo instalarlo a mano.
- **La puerta de recursos de reproducción fallaba una de cada tres ejecuciones sin regresión alguna.**
  Comparaba el conjunto de trabajo del proceso entero —host de pruebas y recolector de cobertura
  incluidos— con un margen de 32 MiB, y siete ejecuciones sin tocar el código dieron entre −7,9 y
  +37,6 MiB. Ajustar una pendiente tampoco sirve: se midió y va de −170 a +1107 KiB por ciclo. El
  límite pasa a 128 MiB, que es lo que corresponde a una regresión gruesa; lo que detecta una fuga
  son los recuentos exactos que ya se comprueban en los cincuenta ciclos.
- **La matriz de medios de prueba no se podía generar desde cero.** Varias muestras mezclan una pista
  de subtítulos desde un archivo acompañante, y su receta lo nombraba con un marcador que el
  generador nunca sustituía ni escribía. Estaba tapado por dos caminos a la vez: en una máquina que
  ya tenía el árbol de salida, las muestras se reutilizaban en vez de producirse; y sin ffmpeg, el
  guion termina antes de intentarlo. Lo descubrió mudar el proyecto de carpeta.
- **Una redacción anterior de la biblioteca personal estaba incompleta.** Se había sustituido el
  título de una serie en español y quedaba el mismo título en inglés, porque el patrón que se buscaba
  estaba escrito en español. Ahora la comprobación no depende de que nadie se acuerde:
  `RepositoryPrivacyTests` recorre los archivos versionados en cada ejecución y deriva lo que busca
  de la máquina, sin escribir ningún dato personal en el código.
- **Una prueba de recuperación fallaba una de cada dos ejecuciones,** porque esperaba a que el
  archivo de señal *existiera* y lo leía acto seguido: existir y estar terminado son momentos
  distintos, y leer entre ambos choca con el proceso que aún lo tiene abierto. Ahora la espera
  consiste en leerlo.
- **El botón de saltar marcas nunca recibía datos.** Estaba construido desde el MVP y ninguna
  parte del ensamblado le entregaba las marcas ni la posición, así que el salto de introducciones
  sólo existía en las pruebas. Ahora sigue al playhead con las marcas compuestas —manuales y
  detectadas—, el editor carga las marcas reales de la serie, y un episodio resuelve su serie de
  verdad en lugar de tratar cada archivo como serie propia.
- **La rama por defecto ya no puede quedarse atrás al publicar.** La redacción de la biblioteca
  personal vivía sólo en la rama de trabajo y el árbol de `main` siguió mostrando lo redactado
  hasta que la auditoría lo encontró; `main` se avanzó hasta la rama y `prepare-release.ps1`
  bloquea desde entonces cualquier publicación con `main` por detrás.
- **La compilación ARM64 nunca había funcionado.** El proyecto de Windows fijaba `PlatformTarget` a
  `x64` sin condición, así que `-r win-arm64` fallaba con `NETSDK1032`. La comprobación temprana que
  existía para detectarlo era un `dotnet restore`, que resuelve paquetes sin compilar nada: daba
  verde mientras el build era imposible. Ahora la CI construye el paquete ARM64, que contiene esa
  comprobación y además la responde.

### Nota

- **Cuatro guardias del ensamblado dejaron de leer el código como texto.** Afirmaban su promesa
  buscando caracteres en el archivo de composición — lo que un comentario o un registro muerto
  puede satisfacer, y ya había pasado tres veces. Ahora afirman los descriptores registrados y,
  en el caso del actualizador, la dirección del objeto que la aplicación construye de verdad,
  comparada con la que publican los dos changelogs. Las dos mitades que ningún descriptor puede
  expresar quedan declaradas como texto a la espera de la reforma del arranque.
- **El código nuevo ya no puede llegar sin sus pruebas.** Cada verificación exige ahora que todo
  archivo fuente nuevo respecto a `main` llegue con al menos el 96 % de sus líneas y ramas
  cubiertas por las suites, con el veredicto por archivo escrito junto a los resultados. La
  puerta se calibró contra la sesión anterior y encontró tres archivos reales por debajo del
  listón —los caminos felices estaban paseados de punta a punta; sus ramas de error, no—, que
  quedan nombrados como deuda visible en la evidencia en lugar de bajar el listón para taparlos.
- **La mejora de calidad para vídeos de baja resolución queda aplazada con su medición.** Se
  investigó con un spike medible sobre medios reales si los filtros de vídeo de VLC 3 (nitidez,
  reducción de ruido, deblocking, escalado) pueden mejorar las series de menos de 720p: ninguno
  procesa un solo fotograma en la ruta de vídeo de esta aplicación — el propio VLC monta la
  cadena y la retira entera al no poder casar los formatos, con cualquier decodificador y
  formato de salida. La función no se promete: su fila queda aplazada con la evidencia medida y
  las alternativas reales (VLC 4, realce propio sobre los fotogramas ya decodificados u otro
  motor) documentadas con su coste, para decidirse con conocimiento y no por intuición.
- **Los presupuestos de rendimiento ya no bloquean en runners compartidos.** Dos presupuestos
  fallaron en CI midiendo el ruido del vecino, nunca en local. La CI los sigue ejecutando y archiva
  su veredicto con los resultados, pero no puede fallar por ellos; siguen bloqueando en el arnés
  físico local, que es donde significan algo. La prueba de durabilidad WAL gana además un reintento
  acotado (3 intentos / 1 s) solo en la reapertura posterior al kill: el «disk I/O error»
  transitorio del disco del runner no es el fenómeno bajo prueba.
- **Ni el actualizador ni winget funcionan mientras el repositorio sea privado.** Los dos leen la
  dirección de publicaciones de GitHub, que para un repositorio privado no responde a nadie; como la
  ausencia de publicación es una respuesta resuelta, la aplicación diría «ya tienes la versión más
  reciente». Publicar el repositorio y cortar una versión son requisitos de que funcionen, no
  adornos. `eng/build-winget-manifest.ps1 -Verify` lo comprueba preguntando a la dirección.
- `PRD-003` queda **bloqueado**, no verificado: no hay máquina Windows 11 ARM64 donde certificar la
  reproducción, y emularla mediría la emulación. Las seis fases físicas están declaradas en
  `arm64-matrix.json` con su razón. Detalle en
  [T42-arm64.md](evidence/stable/T42-arm64.md).
- En ARM64 no existe la decodificación por Intel Quick Sync ni las salidas de vídeo OpenGL: VideoLAN
  no las compila para esa arquitectura. Las quince diferencias de código nativo entre las dos
  versiones quedan listadas en el informe del paquete.

## [0.1.0] — 2026-08-04

Primer artefacto instalable. Cataloga, identifica, reproduce y recuerda dónde se quedó, en español y
en inglés, sin cuenta y sin enviar nada.

### Añadido

- **Biblioteca local.** Carpetas locales, USB y UNC/NAS en su ubicación original, sin copiar ni mover
  ningún vídeo. Escaneo inicial, al iniciar, manual e incremental, cancelable y reanudable, con
  vigilancia continua y escaneo de respaldo para unidades que la vigilancia no cubre.
- **Identificación híbrida.** Detección de película, serie, temporada y episodio por nombre y
  carpeta, con metadatos de TMDB en español e idioma alternativo. Umbrales de confianza: automático
  desde el 90 %, sugerido entre el 60 % y el 89 %, pendiente por debajo. Lo dudoso va a una bandeja
  de revisión.
- **Duplicados como versiones.** Ningún archivo se borra ni se oculta; se elige versión por calidad y
  disponibilidad.
- **Edición protegida de metadatos y arte,** y renombrado opcional con previsualización, registro y
  deshacer.
- **Reproductor LibVLC integrado,** con apertura externa como alternativa. Contenedores y códecs
  habituales, HDR10 con conversión de tono a SDR, pistas y subtítulos internos y externos, velocidad,
  saltos y volumen amplificado con limitador, pantalla completa y mini reproductor.
- **Continuidad.** Progreso exacto guardado cada cinco segundos y en pausa, búsqueda y cierre;
  reanudación dentro de ±5 s; estados de visionado con umbral configurable; progreso trasladado entre
  versiones compatibles; cuenta atrás cancelable para el siguiente episodio; marcas manuales de
  introducción y créditos.
- **Experiencia personal.** Inicio híbrido con reanudar y biblioteca, favoritos, ver más tarde,
  valoración y recomendaciones locales que se explican y se pueden desactivar.
- **Accesibilidad.** Teclado completo, foco visible, lectores de pantalla, escalado, alto contraste,
  reducción de movimiento y subtítulos personalizables.
- **Datos y privacidad.** SQLite local con WAL y migraciones versionadas, copias rotatorias con
  manifiesto y exportación/importación ZIP sin vídeos. Cero telemetría sin consentimiento;
  diagnósticos opt-in y sanitizados.
- **Integración con Windows.** Bandeja e inicio automático configurables y desactivados por defecto,
  teclas multimedia y «Abrir con…» que reproduce sin importar al catálogo.
- **Distribución.** MSIX x64 y ZIP independiente, con SHA-256 publicado, SBOM en CycloneDX y SPDX,
  licencia y avisos de terceros dentro del artefacto, y compilación reproducible.
- **La aplicación puede decir dónde vive.** `AP_LOCALMEDIA_DATA_ROOT` nombra la carpeta de datos; se
  lee una vez al arrancar y en blanco equivale a no ponerla.

### Corregido

Durante el ensamblado y el empaquetado, recorrer la aplicación real encontró defectos que ninguna
prueba sin cabeza veía:

- Consentir el primer escaneo no escaneaba nada, así que una instalación nueva se quedaba vacía para
  siempre.
- Añadir una carpeta repetida cerraba el proceso en lugar de rechazarla con una frase.
- Un archivo escaneado y sin identificar abría la ficha de serie, que no ofrece reproducir.
- Elegir una pista de audio ni la aplicaba ni la guardaba.
- La sesión no alimentaba el registro de progreso, así que la oferta de reanudar no volvía.
- Retirar el consentimiento de diagnósticos dejaba el informe exportado en el disco.
- El indicador de estado del vídeo no se alimentaba nunca: quedaba en blanco mientras el motor
  decodificaba por hardware.
- Una versión antigua abría y escribía sobre una base que una versión posterior ya había migrado.
- **Instalado como MSIX, los datos no iban donde esta documentación promete**: Windows redirigía las
  escrituras al contenedor del paquete, y **desinstalarlo borraba la biblioteca entera**, copias
  incluidas. El paquete desactiva ahora esa redirección, de modo que el MSIX y el ZIP comparten una
  sola carpeta de datos y desinstalar retira sólo la aplicación.

### Seguridad

- El artefacto **no lleva ningún token de acceso**. La identificación remota exige poner uno a mano
  en `AP_LOCALMEDIA_TMDB_TOKEN`, y sin él no se abre ninguna conexión.
- El paquete declara una sola capacidad, `runFullTrust`, y ninguna de red, ubicación o biblioteca del
  sistema.
- El payload se examina antes de publicarse en busca de claves, tokens y rutas locales.

### Limitaciones conocidas

- **Sin firma de código.** Windows mostrará un aviso de SmartScreen, y la documentación no afirma lo
  contrario. Compruebe el hash publicado; la compilación es reproducible.
- **El MSIX sin firma no se instala.** Windows exige una firma en la que confíe, así que el MSIX de
  esta publicación sirve para inspección y archivo; use el ZIP, que no necesita instalador.
- **Una sola clase de adaptador de vídeo.** La matriz se ejecutó entera sobre el adaptador discreto
  disponible; este equipo no tiene gráficos integrados, de modo que la ruta de decodificación de
  Intel Quick Sync no se ha ejercido nunca.
- **`PLY-004` bloqueado:** la selección de 5.1 y 7.1 no se ha ejercido porque ningún punto final de
  audio declara más de dos canales.
- **Sin ARM64,** sin Store y sin actualizador: llegan con la primera publicación estable.
- **La agrupación automática de versiones no está cableada.** La comparación de versiones existe y
  está probada, pero hoy nada crea grupos, de modo que en el artefacto sólo aparece si un grupo
  llegara por otra vía.

[0.1.0]: https://github.com/apvisualsolutions/ap-reelume/releases/tag/v0.1.0
