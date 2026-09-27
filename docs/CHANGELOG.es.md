# Cambios

Todo cambio relevante de AP Reelume para quien lo usa. La versión inglesa está en
[CHANGELOG.en.md](CHANGELOG.en.md).

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el versionado es
[SemVer](https://semver.org/lang/es/).

## [Sin publicar] / [Unreleased]

### Añadido

- **Cursos.** Una carpeta de lecciones se puede marcar como curso y aparece en su propia sección,
  **Cursos**, con una tarjeta por curso. La ficha del curso dice dónde lo dejaste y qué fue lo último
  que viste, y cada lección se puede marcar como vista o quitarle la marca. Mientras se reproduce, el
  panel **Lecciones** del reproductor enseña el curso entero, y al terminar una lección se ofrece la
  siguiente. La imagen de cada curso se saca de su propio vídeo.
- **Tu propia portada.** En el editor de un título, un botón abre el explorador de Windows y la
  imagen que elijas queda como portada; una consulta posterior al proveedor no la pisa.
- **De dónde sale cada portada, a tu elección.** En Ajustes, **Orden de las portadas** ordena los
  tres sitios de los que puede venir —la tuya, la del proveedor y un fotograma del vídeo—, y en el
  editor de un título se puede elegir sólo para ese título, sin que un cambio posterior del orden
  general deshaga esa elección.
- **Una película o serie sin portada muestra un fotograma de su propio vídeo.** Se saca en segundo
  plano, cede ante una reproducción o un escaneo, y cada archivo se decodifica una sola vez mientras
  no cambie.
- **Brillo, contraste y gamma** en la reproducción, para aclarar un vídeo oscuro. Dejarlos como vienen
  no cambia la imagen.
- **Reducción de ruido.** Junto al brillo y la gamma, quita los cuadros que deja la compresión de un
  vídeo de poca calidad, que son los que se ven al aclararlo. Empieza apagada y se recuerda como el
  resto de la imagen. Si el equipo no llega a limpiar cada fotograma a tiempo, la aplicación lo
  limpia algo menos antes que dejar que el vídeo dé tirones.
- **Un vídeo más pequeño que la ventana se ve más nítido.** La aplicación lo amplía con un escalado
  propio que viene encendido y funciona en cualquier tarjeta gráfica; apagarlo devuelve la imagen
  exactamente como antes.
- **Los ajustes del reproductor, sobre el vídeo.** Lo que se decide mirando la película —velocidad,
  pistas, imagen, estilo de los subtítulos, la espera del siguiente episodio, la detección de
  introducciones, los atajos— se abre desde el engranaje del reproductor, sin salir de ella. Cada
  grupo de opciones, aquí y en Ajustes, lleva su botón **Restaurar valores por defecto**.
- **El siguiente episodio, a tu manera.** Se puede decidir si el siguiente episodio empieza solo y
  cuántos segundos espera.
- **Canales de audio.** Estéreo, 5.1 y 7.1 se pueden elegir cuando el dispositivo de salida los
  admite; si no los admite, la aplicación lo dice.
- **Apariencia.** Tema, seguir el tema de Windows, color de acento, fondo Mica, tinte de acento,
  densidad, tamaño de las portadas, redondeo de esquinas, títulos bajo las portadas, animaciones y
  superficie del reproductor. El idioma tiene su propia sección en Ajustes.
- **Actualizaciones.** La aplicación puede comprobar si hay una versión nueva, a mano o de forma
  automática; viene desactivado. Antes de ofrecer una versión verifica la firma de sus huellas, y si
  la rechaza dice por qué.
- **El escaneo se puede parar desde la pantalla**, avisa cuando termina, y la biblioteca avisa de una
  carpeta que no puede leer.
- **Ventana flotante.** El mini reproductor es una ventana sin marco que se puede arrastrar.
- **Al reproducir se oculta todo menos la imagen**, y vuelve al mover el ratón o pulsar una tecla.
  El doble clic sobre la imagen y la tecla `F` ponen y quitan la pantalla completa.
- Todos los botones dicen lo que hacen al posar el puntero.
- Los archivos `.flv` entran en la biblioteca.

### Cambiado

- **Licencia propia, gratuita para quien la usa.** AP Reelume deja de ser software libre: se sigue
  entregando gratis y su código se puede examinar, pero leerlo no da derecho a modificarlo ni a
  redistribuirlo. Lo publicado antes del 2026-09-13 conserva los derechos con los que se publicó. El
  texto está en [LICENSE](../LICENSE).
- **El motor de vídeo se compila sin código GPL**, a partir de la misma versión de VLC, y reproduce
  los mismos formatos que antes, subtítulos incluidos. Los avisos de terceros y los textos de todas
  las licencias viajan dentro del paquete.
- **La valoración es de cinco estrellas.**
- **La biblioteca estira las portadas hasta llenar cada fila**, y las pantallas siguen el diseño de la
  aplicación: iconos, botones, menús y marcas sobre las portadas.

### Eliminado

- **El teletexto deja de decodificarse.** Los dos decodificadores de teletexto de VLC llevan código
  GPL, así que el motor sin GPL no los incluye.

### Corregido

- Al cambiar a otra versión de una película y elegir **Empezar de nuevo**, la próxima vez vuelve a
  empezar desde el principio: a veces se quedaba guardado un minuto de la versión anterior.
- Cerrar la aplicación después de ver un vídeo ya no termina en un fallo.
- Los controles del reproductor se ocultan solos a los tres segundos de no mover el ratón mientras la
  película avanza, y el puntero con ellos.
- Un vídeo añadido a una carpeta de la biblioteca aparece solo, sin reiniciar la aplicación, y un
  disco externo o una carpeta de red se vuelven a revisar cada cierto tiempo.
- La velocidad de reproducción se recuerda, y ya no se cuela en películas que no la pidieron.
- Restaurar los datos del proveedor ya no borra la portada que elegiste.
- En pantalla completa el vídeo llena la pantalla, y ya no se deforma al redimensionar la ventana.
- El color de los vídeos HD se decodifica con la matriz que les corresponde.
- Los subtítulos que viven junto al archivo se cargan, y llegan a la pantalla.
- La biblioteca ya no se corta en el título cincuenta.
- La lista de carpetas ya no dice «Disponible» con el disco desenchufado.
- Inicio se carga al arrancar y ya no sale vacío con la biblioteca llena.
- Retirar una carpeta avisa de cuánto se pierde y hace lo que promete.
- La lista de atajos del reproductor habla el idioma de la aplicación.

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
- **Instalado como MSIX, los datos no iban donde la documentación promete**: Windows redirigía las
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
- **Una sola clase de adaptador de vídeo.** La matriz se ejecutó entera sobre un adaptador discreto;
  la ruta de decodificación de Intel Quick Sync no se ha ejercido nunca.
- **Sonido multicanal sin comprobar:** la selección de 5.1 y 7.1 no se ha ejercido porque ningún
  dispositivo de audio disponible declaraba más de dos canales.
- **Sin ARM64,** sin Store y sin actualizador: llegan con la primera publicación estable.
- **La agrupación automática de versiones no está cableada.** La comparación de versiones existe y
  está probada, pero hoy nada crea grupos, de modo que en el artefacto sólo aparece si un grupo
  llegara por otra vía.

[0.1.0]: https://github.com/apvisualsolutions/ap-reelume/releases/tag/v0.1.0
