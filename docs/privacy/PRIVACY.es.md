# Privacidad de AP Reelume

AP Reelume es una aplicación local. Funciona sin cuenta, sin sincronización y sin conexión, y esta
página describe exactamente qué sale del equipo y en qué condiciones.

## Qué no sale nunca

Nunca salen del equipo, con consentimiento o sin él:

- Rutas de carpetas y de archivos.
- Nombres de archivo.
- Títulos de tu biblioteca, reales o inventados.
- Identificadores de contenido o de proveedor.
- Tu historial de reproducción, tu progreso y tus marcas personales.
- Tus términos de búsqueda.
- Cualquier token, contraseña o credencial.
- El nombre de tu usuario de Windows y el nombre de tu equipo.

## Qué conexiones puede hacer la aplicación

| Destino | Cuándo | Para qué |
|---|---|---|
| `api.themoviedb.org` | Sólo cuando pides identificar o actualizar los metadatos de un título | Obtener los datos de ese título |
| `image.tmdb.org` | Sólo para un título ya identificado | Descargar su imagen |
| `api.github.com` | Sólo cuando pulsas «Buscar actualizaciones», o al arrancar si activaste la comprobación automática en Ajustes | Preguntar si existe una versión nueva |
| `github.com` | Sólo al descargar una actualización que tú confirmaste; su almacenamiento puede redirigir a otro dominio de GitHub | Descargar el paquete de esa versión |
| `objects.githubusercontent.com` | Sólo como destino de la redirección de esa misma descarga; es el almacenamiento de GitHub | Recibir los bytes del paquete confirmado |
| `*.githubusercontent.com` | Sólo si el almacenamiento de GitHub redirige a otro de sus subdominios; ningún otro dominio se acepta | El mismo paquete confirmado, desde otro subdominio de GitHub |

No hay ninguna otra conexión. No hay telemetría ni envío de informes en segundo plano. La
comprobación de actualizaciones está **desactivada de fábrica**: ninguna instalación la trae
encendida, y hasta que la actives en Ajustes —o pulses tú el botón— la aplicación no pregunta nada a
nadie. Cada componente que puede abrir una conexión tiene su propósito declarado en el código, y una
prueba falla si aparece uno que no lo tenga o si esta tabla deja de coincidir con esa declaración.

La verificación de una actualización descargada tiene dos capas. Las huellas SHA-256 publicadas van
firmadas con una clave minisign cuya mitad pública viaja dentro del binario: el actualizador rechaza
cualquier versión cuyas huellas no lleven esa firma, así que la huella esperada ya no procede de la
misma respuesta sin firmar que el paquete al que avala. La huella y el tamaño demuestran después que
los bytes descargados son los publicados. Lo que esta firma **no** hace es autenticar el paquete ante
Windows: el artefacto sigue sin firma Authenticode, SmartScreen seguirá avisando, y quien controle a
la vez la cuenta de GitHub y la clave de firma podría publicar una versión que el actualizador
aceptara — las dos cosas viven separadas precisamente para que un solo compromiso no baste.

## Diagnósticos

Los diagnósticos están **desactivados** hasta que los actives en Ajustes. Cuando los activas:

1. Puedes ver el informe completo en pantalla antes de guardarlo. Lo que ves es exactamente lo que se
   guardaría, palabra por palabra.
2. El informe se guarda como un archivo en tu carpeta de datos. **No se envía a ningún sitio.**
3. Desactivarlos borra el consentimiento y la vista previa, y deja de generarse nada.

El informe se construye a partir de una **lista permitida cerrada**: si un dato no está en la lista, no
viaja, aunque alguien lo añada más adelante al resto de la aplicación. La lista es esta:

- Versión de la aplicación, de Windows y del entorno de ejecución.
- Idioma de la interfaz.
- Capacidades del equipo, en forma agregada.
- Códigos de error y el tipo de la excepción, nunca su mensaje.
- Recuentos por tramos: `0`, `1`, `2-5`, `6-20`, `21-100`, `100+`.

Los recuentos viajan por tramos porque el número exacto de elementos de tu biblioteca es, en sí mismo,
un dato sobre tu biblioteca. Los mensajes de excepción se descartan enteros: los escribe quien lanza la
excepción y no hay forma de saber de antemano qué decidió incluir.

## Copias y exportación

Una copia contiene tu base de datos local, tus preferencias, las imágenes que hayas elegido tú y un
manifiesto con las huellas SHA-256 de todo ello. Nunca contiene vídeos, ni las imágenes descargadas de
internet, ni diagnósticos, ni credenciales. La copia se queda donde tú la pongas: la aplicación no la
envía a ninguna parte.

## Credenciales de red

Las credenciales de un NAS pertenecen a Windows. AP Reelume no las pide, no las guarda y no tiene
almacén propio de credenciales; una prueba comprueba que el código no usa ninguno.

## Tu token de TMDB

Si usas metadatos remotos, el token se lee de una variable de entorno de tu sesión. No se guarda en la
base de datos, no entra en las copias y no aparece en los diagnósticos.

## Cómo se comprueba

- Se siembran canarios en rutas, nombres de archivo, títulos, identificadores, historial, términos de
  búsqueda, un token y una credencial señuelo, y se comprueba que ninguno aparece en el informe.
- Un servidor señuelo local cuenta las solicitudes que recibe durante las operaciones que no deberían
  hacer ninguna.
- Un observador dentro del proceso registra cada petición HTTP, cada conexión y cada resolución de
  nombres, antes del cifrado.
- Ese mismo observador se comprueba contra una petición real, para demostrar que sí ve lo que hay.

## Alcance de estas afirmaciones

Lo anterior describe lo que hace este proceso. No es una captura de la red del equipo: si otro programa
del sistema hace algo, esto no lo ve ni pretende verlo.
