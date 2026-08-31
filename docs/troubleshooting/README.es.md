# Solución de problemas

Qué hacer cuando algo no va. La versión inglesa está en [README.en.md](README.en.md).

## Windows avisa al abrir la descarga

Es lo esperado: esta publicación **no está firmada**. El aviso de SmartScreen dice la verdad, que
Windows no sabe quién publicó el archivo.

Antes de continuar, compruebe el hash:

```powershell
Get-FileHash .\ApReelume-0.1.0-win-x64.zip -Algorithm SHA256
```

Debe coincidir con la línea correspondiente de `SHA256SUMS.txt`. Si no coincide, no lo abra.
[SMARTSCREEN.es.md](../release/SMARTSCREEN.es.md) explica qué más puede comprobar.

## El MSIX no se instala

Windows sólo instala un paquete firmado por un certificado en el que confía. Este no lo está, así que
el MSIX de esta publicación sirve para inspección y archivo. **Use el ZIP**: descomprímalo y ejecute
`ApSolutions.LocalMedia.Windows.exe`.

## Añadí una carpeta y la biblioteca sigue vacía

- ¿Confirmó el primer escaneo? La aplicación pregunta antes de escanear la primera vez.
- ¿Pulsó **Aplicar** después? El catálogo se refresca al aplicar.
- ¿La carpeta contiene alguno de los contenedores reconocidos? Son `.mp4`, `.mkv`, `.avi`, `.mov`,
  `.webm`, `.m4v`, `.ts`, `.m2ts` y `.flv`. Otros formatos no se catalogan.

## Dice que no puede añadir la carpeta

Tres motivos posibles, y la pantalla dice cuál:

| Mensaje | Qué significa |
|---|---|
| Ya está en la biblioteca | La carpeta ya se añadió. No se añade dos veces y no se toca nada. |
| Está dentro de otra, o la contiene | Las raíces no pueden solaparse. Elija una que no lo haga. |
| Esa ruta no se puede usar | La carpeta no existe o la ruta está incompleta. Escríbala entera. |

## Un vídeo aparece como «no disponible»

Su unidad no está conectada. El catálogo **no se pierde**: reconecte la unidad y el vídeo vuelve
solo, sin duplicarse. Si la biblioteca cambió de sitio para siempre, restaure una copia indicando la
ruta nueva y las rutas se reasignan.

## No identifica nada

La consulta a TMDB necesita un token de acceso en la variable de entorno
`AP_LOCALMEDIA_TMDB_TOKEN`. **La descarga no lleva ninguno**, a propósito: sin ese acto deliberado,
la aplicación no abre ninguna conexión de metadatos.

Sin token, la identificación funciona con lo que ya esté en la caché local y con lo que se deduzca
del nombre del archivo. Puede corregir a mano cualquier título en **Revisar**.

## Un vídeo no se reproduce

- El **estado del vídeo** en pantalla dice qué pasó. Si dice que el formato no está admitido, ese
  códec no está implementado en esta versión.
- Si el reproductor ofrece **Reintentar** o **Abrir externamente**, el motor falló al abrir el
  archivo. Abrirlo externamente lo reproduce en su reproductor predeterminado, pero entonces el
  progreso exacto no se promete.
- Un archivo corrupto se detecta al abrirlo y no bloquea la aplicación.

## Se ve pero no se oye, o al revés

Compruebe la **pista de audio** y la **salida de audio** en el panel de la sesión. El dispositivo se
elige por identificador estable, así que la preferencia sobrevive a desconectar y reconectar.

Si su equipo no ofrece una salida de más de dos canales, sólo verá estéreo: la lista muestra lo que
el punto final declara hoy, no lo que su hardware podría hacer con otra configuración.

## La aceleración por hardware no aparece

El indicador dice **«Decodificación acelerada por hardware»** cuando se pidió y no se ha caído a
software, y **«La aceleración por hardware no estaba disponible; se decodifica por software»** cuando
sí. Si ve lo segundo, la reproducción continúa igualmente: no es un fallo, es el camino alternativo.

## Cerré la aplicación y perdí dónde iba

No debería: el progreso se escribe cada cinco segundos y además al pausar, buscar y cerrar. Tras un
cierre inesperado se recupera con una precisión de ±5 s.

Si la ficha no ofrece continuar, compruebe que está mirando el mismo contenido: el progreso sigue al
contenido, no al archivo, así que un archivo reasignado a otro título lleva su progreso consigo.

## La aplicación no abre y muestra una pantalla de recuperación

La base de datos no se pudo abrir. La pantalla dice por qué. Los dos casos habituales:

- **Base dañada.** Se ofrece la carpeta de copias. Restaure la más reciente.
- **Una versión posterior migró esta base.** Está ejecutando una versión antigua sobre datos que ya
  actualizó una nueva. La aplicación **no escribe nada** en ese caso. Vuelva a instalar la versión
  nueva, o restaure una copia anterior a ella.

## Quiero que no toque mi carpeta de datos

Ponga `AP_LOCALMEDIA_DATA_ROOT` con la ruta que quiera antes de arrancar. La variable se lee una sola
vez al iniciar y un valor en blanco equivale a no ponerla. Es también la forma de probar la
aplicación sin tocar sus datos reales.

Redirigir `LOCALAPPDATA` **no** funciona: .NET resuelve esa carpeta con una llamada del sistema que
no lee esa variable.

## Quiero desinstalar sin perder mis datos

Borre la carpeta que descomprimió. Sus datos siguen en
`%LOCALAPPDATA%\APSolutions\LocalMedia`. Bórrelos aparte si eso es lo que quiere: la aplicación
nunca lo hace por usted.

## Enviar un diagnóstico

En **Ajustes → Privacidad** puede activar los diagnósticos. Están desactivados por defecto, se
sanitizan —nunca llevan rutas, nombres completos, biblioteca ni historial— y puede ver el informe
exacto antes de decidir. **Retirar el consentimiento borra el informe** ya escrito, y su carpeta con
él.
