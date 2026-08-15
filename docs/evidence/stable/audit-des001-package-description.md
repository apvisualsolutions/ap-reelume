# La instalación habla dos idiomas / The installation speaks two languages

Primera mitad de `DES-001`: la que sí puede hacer un agente. Los cinco activos de marca los prepara
el propietario; lo que estaba **medido como defecto** era el texto. / The half an agent can do; the
brand assets are the owner's.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El defecto, que no era una declaración ausente / The defect, which was not a missing declaration

El manifiesto declaraba `es-ES` y `en-US` desde el principio. Su descripción era **una sola cadena
con una barra dentro**: / The manifest declared both languages all along. Its description was one
string with a slash inside it:

```xml
Description="Biblioteca y reproductor de vídeo local / Local video library and player"
```

Windows enseña eso **tal cual** a quien lee en español y a quien lee en inglés. **Un idioma declarado
no localiza nada por sí solo**: lo que localiza es una referencia `ms-resource:` y un recurso por
idioma, que es justo lo que winget ya hacía bien con sus dos `locale.*.yaml`. / A declared language
localises nothing on its own.

## Lo que hubo que medir antes de escribir una línea / Measured before writing a line

Dos preguntas decidían si esto era posible sin romper una puerta. / Two questions decided whether
this was possible without breaking a gate.

1. **¿Existe `makepri.exe`?** Sí, junto a `makeappx.exe` en el mismo SDK que el empaquetado ya usa
   (10.0.26100.0). / Yes, beside the tool the packaging already finds.
2. **¿Es determinista su salida?** La comparación de reproducibilidad de `verify.ps1` construye el
   mismo commit en **dos directorios distintos** y compara bytes, así que un `resources.pri` con una
   marca de tiempo dentro habría roto una puerta que nada más toca. Medido: / Measured:

```
run1  3647A2937DDD18E9BF3C0B60D2604F89  1272 bytes
run2  3647A2937DDD18E9BF3C0B60D2604F89  1272 bytes
```

Dos ejecuciones, dos directorios, **el mismo hash**. / Two runs, two directories, the same hash.

## Un tercer hallazgo, del que ningún mensaje de error habla / A third finding, which no error message names

Los `.resw` se generaban con el DOM de XML, y el DOM **se inventa un espacio de nombres** para
`xml:space`: escribe `d2p1:space` con su declaración al lado. `makepri` contesta a eso con / The XML
DOM invents a namespace for `xml:space`, and makepri answers with:

```
ERROR: PRI224: 0xdef00502 - root node not found.
```

que no nombra ni el atributo, ni el archivo, ni el nodo real. Se escriben como texto, con el valor
escapado, y está dicho en el guion para que nadie lo «mejore» de vuelta. / The message names neither
the attribute nor the file. They are written as text now, and the script says why.

## La forma / The shape

- **El texto no se escribe aquí.** Sale del primer párrafo de cada README, que es de donde winget ya
  sacaba el suyo, así que los **dos canales de instalación dicen lo mismo en cada idioma** y ninguno
  está tecleado dos veces. El lector salió a `eng/read-product-summary.ps1`, que antes era una
  función dentro del guion de winget. / One reader, two channels.
- **Los idiomas salen del manifiesto.** Declarar un tercero es la única edición que lo añade; si
  falta su README, el empaquetado se detiene y lo dice. / Declaring a third language is the only edit
  that adds it.
- **El nombre sigue siendo una cadena.** «AP Reelume» es el nombre del producto en los dos idiomas, y
  una marca que cambia con la configuración regional es otro producto. / The name stays a string.
- **`eng/find-sdk-tool.ps1`** reemplaza las **dos** copias de `Get-MakeAppx` que había, en vez de
  añadir una tercera para `makepri`. / One finder instead of a third copy.

## Aceptación por ejecución / Acceptance by running it

El paquete se selló **sin `/nv`**, así que MakeAppx corrió la validación que decide si Windows podría
instalarlo — incluida la referencia. / Sealed without `/nv`, so the reference passed the validation
that decides installability.

```
Described in en-US, es-ES under APSolutions.LocalMedia.
resources.pri  1696 bytes   (dentro del MSIX / inside the MSIX)
Description    ms-resource:AppDescription   (en el manifiesto sellado / in the sealed manifest)
```

Y leído de vuelta del propio `.pri`, no de lo que entró: / Read back out of the PRI itself:

```
ms-resource://APSolutions.LocalMedia/Resources/AppDescription
  Language-es-ES  Una biblioteca de vídeo local y su reproductor, para Windows 11 x64. …
  Language-en-US  A local video library and its player, for Windows 11 x64. …
```

El mapa se llama como la identidad del paquete, que es contra lo que `ms-resource:AppDescription`
resuelve y contra nada más. / The map is named after the package identity, which is what the
reference resolves against.

## El rojo archivado / The archived red

Con la descripción antigua puesta de vuelta, la suite nueva falla: / With the old description put
back, the new suite fails:

```
Assert.Equal() Failure: Strings differ
Con error: 1, Superado: 3, Total: 4
```

## Lo que este cambio caducó, y cómo se rehízo / What this expired, and how it was redone

Tocar el manifiesto **caduca dos mediciones manuales archivadas**, porque las dos fijan su SHA-256:
`windows-lifecycle.json` —que degrada a «bloqueado» y la suite acepta— y `updater-handover.json`, que
pone `UpdateHandoverTests` en rojo. Y aquí la caducidad es **correcta, no conservadora**: una
referencia `ms-resource:` con su `resources.pri` cambia lo que Windows resuelve al instalar. / The
expiry is correct here rather than merely conservative.

Se rehízo, con permiso del propietario, y **el ciclo quedó versionado**, que no lo estaba: el
documento describía los pasos y el guion vivía fuera del repositorio, así que rehacer la medición
justo cuando caduca dependía de un archivo que nada versionaba. / The cycle is versioned now.

**Tres defectos del arnés, los tres medidos y ninguno con un mensaje que los nombrara:**

1. **El `.wsb` no era XML válido.** El comando de inicio es **texto XML**, y el operador de llamada
   `&` de PowerShell lo rompe. Windows Sandbox contesta «el archivo de configuración no es válido»,
   sin nombrar carácter, línea ni elemento — el sandbox estuvo siete minutos en ese diálogo sin
   escribir nada. Ahora el comando de inicio es un `.cmd` preparado, sin nada que escapar, y el `.wsb`
   **se valida como XML antes de entregarse**. / The `&` alone makes the configuration malformed.
2. **Cerrar el sandbox matando `WindowsSandboxServer`** —un servicio del anfitrión— dejó la ejecución
   siguiente cerrando su propia sesión con «el entorno remoto está cerrando la sesión», un mensaje
   que no nombra ni la causa ni la ejecución anterior. Se cierra la **ventana**, y nada más. / Killing
   the host service broke the next run.
3. **Y la primera corrección de eso no habría cerrado nada**: apuntaba a `WindowsSandboxClient`, un
   proceso que en esta compilación **no existe**. La ventana la tiene `WindowsSandboxRemoteSession` —
   comprobado, no supuesto. / Measured, not assumed.

Y una cuarta, del propio informe: escribirlo en ASCII convirtió «Elegir una aplicación» en «Elegir
una aplicaci?n». **Una evidencia que estropea lo que registró es evidencia sobre el arnés.** El guion
sigue siendo ASCII —lo exige PowerShell 5.1—; su informe, no. / Evidence that mangles what it
recorded is evidence about the harness.

## Un hallazgo que la medición añadió / A finding the measurement added

En una máquina sin nada registrado para `.msix`, `Process.Start` devuelve **nulo** —la aplicación
dice «Windows no lo aceptó», y es cierto: no se instaló nada, la base quedó intacta en 372 736 bytes
antes y después— **pero Windows deja en pantalla el diálogo «Elegir una aplicación»**. Reproducido en
las dos ejecuciones de esta sesión; el informe del 2026-08-14 registró allí sólo la consola de
PowerShell. / Process.Start returns null and Windows still leaves the "choose an app" dialog on
screen. Reproduced twice; the 2026-08-14 report recorded only a PowerShell console there.

Es la imagen espejo de lo que la mitad `withHandler` existe para descartar, y **no se toca aquí**: se
mide, se nombra y lo decide el propietario. / Not touched here: measured, named, and the owner's call.

## Lo que sigue abierto de DES-001 / What stays open in DES-001

Los **cinco activos** de `src/ApSolutions.LocalMedia.Windows.Package/Assets/` siguen siendo
marcadores de posición del 3 de agosto —de 576 B a 7 KiB— y son lo primero que alguien ve del
producto. Los prepara el propietario con el rediseño. / The five placeholder assets are the owner's,
with the redesign.
