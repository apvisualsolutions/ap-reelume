# Manual de uso

Cómo hacer cada cosa en AP Reelume. La versión inglesa está en [README.en.md](README.en.md).

## La primera vez

1. Ejecute `ApSolutions.LocalMedia.Windows.exe`. No hay instalación ni registro.
2. Vaya a **Biblioteca**. Arriba está **Añade tus carpetas**.
3. Escriba la ruta de la carpeta, elija si es **Local**, **USB** o **UNC o NAS**, y pulse
   **Añadir carpeta**.
4. La aplicación pide permiso antes del primer escaneo. Pulse **Permitir primer escaneo**.
5. Cuando termine, pulse **Aplicar** para ver el catálogo.

Añadir una carpeta **no copia ni mueve** ningún vídeo. Si la carpeta ya está en la biblioteca, o
solapa con otra que sí lo está, la aplicación lo dice y no añade nada.

## La biblioteca

- **Buscar** por título, reparto o género. La búsqueda es local.
- **Filtrar** por estado de visionado y **ordenar**; pulse **Aplicar** para que surta efecto.
- Un vídeo cuya unidad no está conectada aparece como **no disponible**. No se borra del catálogo, y
  vuelve solo al reconectar la unidad, sin duplicarse.

### Escaneo

En **Ajustes** puede elegir cuándo escanea: al iniciar, manualmente o de forma incremental. La
vigilancia continua aplica los cambios locales en segundos; para USB y NAS hay un escaneo de respaldo
que recupera lo que la vigilancia no vio.

## Identificación

La aplicación deduce película, serie, temporada y episodio del nombre y de la carpeta. Patrones como
`S01E02`, `1x02` o `Cap.803` están cubiertos.

- Coincidencia **≥ 90 %**: se aplica sola.
- Entre **60 % y 89 %**: se sugiere y espera.
- **Menos del 60 %**: queda pendiente.

Todo lo que no se resuelve solo va a **Revisar**, donde usted elige. Una corrección suya no se
sobrescribe después.

### Metadatos en línea

La consulta a TMDB sólo ocurre si existe un token de acceso en la variable de entorno
`AP_LOCALMEDIA_TMDB_TOKEN`. **La descarga no lleva ninguno.** Sin token, la identificación trabaja
con lo que ya esté en la caché local y no abre ninguna conexión de metadatos. Las únicas conexiones
posibles de la aplicación —metadatos y comprobación de actualizaciones, ambas bajo su control—
están enumeradas en la declaración de privacidad.

## La ficha de un título

Desde la ficha puede reproducir, marcar como visto o no visto, poner favorito, guardar para más
tarde y valorar de 1 a 10. También:

- **Editar metadatos.** Lo que edite queda bloqueado: una actualización remota posterior no lo pisa.
- **Previsualizar renombrado.** Muestra qué haría antes de hacerlo. Si hay conflicto, no se ejecuta.
  Nunca mueve carpetas, y ofrece deshacer cuando es viable.
- **Revisar versiones.** Cuando un mismo contenido tiene varios archivos, se tratan como versiones.
  Ninguno se borra ni se oculta.

## Reproducir

Pulse **Reproducir desde el principio**, o **Continuar** si ya lo empezó.

| Acción | Dónde |
|---|---|
| Pausar, detener, retroceder, avanzar | Controles de transporte |
| Pista de audio y subtítulos | Panel de la sesión |
| Salida de audio | Panel de la sesión |
| Marcas de introducción y créditos | Panel de la sesión |
| Pantalla completa y mini reproductor | Botones del reproductor |

- **Velocidad, saltos y volumen** son configurables. Por encima del 100 % el volumen pasa por un
  limitador, para que un refuerzo no produzca picos.
- **Subtítulos** internos y externos: SRT, ASS y VTT. La selección se reaplica al siguiente episodio.
- **El estado del vídeo** en pantalla dice qué está haciendo el motor: rango dinámico, HDR10 directo,
  conversión a SDR, aceleración por hardware o caída a software.

Hay una sesión de reproducción, y sólo una. Cambiar entre ventana, pantalla completa y mini
reproductor conserva la posición y las preferencias.

## Continuidad

El progreso se guarda cada cinco segundos y además al pausar, buscar y cerrar. Al volver, la
aplicación ofrece continuar donde lo dejó, con una precisión de ±5 s incluso si el cierre fue
inesperado.

Un archivo movido o renombrado conserva su identidad y su progreso. Si cambia de versión, el progreso
viaja con el contenido: exacto cuando las duraciones coinciden, proporcional cuando la diferencia es
segura, y con confirmación cuando no lo es.

Al terminar un episodio hay una cuenta atrás para el siguiente. Es cancelable desde teclado, ratón o
tecla multimedia, y si el archivo siguiente no está, vuelve a la ficha en lugar de fallar.

## Teclado y accesibilidad

Todas las acciones esenciales funcionan sin ratón. Los atajos son configurables en **Ajustes**, y las
teclas multimedia del teclado se registran sólo mientras hay una sesión.

La aplicación respeta el tema del sistema, el alto contraste, el escalado y la preferencia de
reducción de movimiento de Windows. Los subtítulos tienen controles propios de tamaño, fuente, color,
fondo y contorno.

## Sus datos

- **Copias.** En **Copias** puede crear una copia y restaurarla. Las copias rotan y llevan un
  manifiesto con hashes.
- **Exportar e importar.** Un ZIP con su catálogo, sus marcas y su arte personal. **No lleva vídeos**,
  y la caché de imágenes descargadas se excluye porque se regenera sola.
- **Restaurar en otra ruta.** Si la biblioteca cambió de sitio, la restauración reasigna las rutas sin
  duplicar nada.

Todo vive en `%LOCALAPPDATA%\APSolutions\LocalMedia`, salvo que nombre otra carpeta con
`AP_LOCALMEDIA_DATA_ROOT`.

**Desinstalar no borra sus datos.** Windows retira la aplicación pero deja esa carpeta intacta: su
catálogo, su progreso y sus copias siguen ahí, y una reinstalación los encuentra donde estaban. Si
quiere borrarlo todo de verdad, elimine esa carpeta a mano después de desinstalar. Sus vídeos nunca
están dentro: la aplicación no los copia ni los mueve.

## Ventana, bandeja e inicio

Cerrar cierra. Si prefiere que se quede en la bandeja, o que arranque con Windows, actívelo en
**Ajustes**: ambas cosas están desactivadas por defecto y son reversibles. Al cerrar, la aplicación
escribe el progreso antes de irse.

**Si desinstala con «iniciar con Windows» activado**, la entrada que lo hacía posible queda huérfana
en el registro (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`). Es inocua: apunta a un
programa que ya no existe y Windows simplemente la ignora. Reinstalar la repara sola, porque la
aplicación reescribe su entrada al arrancar. Si quiere quitarla a mano, abra el Administrador de
tareas → pestaña **Aplicaciones de arranque** y deshabilítela, o bórrela de esa clave con el editor
del registro.

## Abrir un archivo suelto

Puede abrir un vídeo con AP Reelume desde el Explorador sin añadirlo a la biblioteca. Se reproduce y
**no crea nada** en el catálogo. Si después quiere catalogarlo, la aplicación le ofrece añadir su
carpeta, y sólo entonces la añade.
