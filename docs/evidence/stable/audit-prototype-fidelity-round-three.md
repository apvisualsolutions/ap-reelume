# Auditoría de fidelidad con el prototipo, tercera vuelta / Prototype fidelity audit, round three

**2026-08-25.** Comparación vista a vista entre las capturas del prototipo verificadas
(`artifacts/ui-captures/T36-proto-full/`, dieciséis, cada una comprobada contra un segundo render) y
capturas de la aplicación compilada, tomadas con
`shoot.ps1 -Width 2250 -Height 1500 -Downscale 1.5` (la pantalla va al 150 %, así que eso da los
1600×1000 lógicos con los que se diseñó el prototipo). / A view-by-view comparison between the
verified prototype captures and captures of the built application, taken at the logical size the
prototype was designed at.

## Lo que se cerró en esta vuelta / What this round closed

| Vista | Lo que faltaba | Ahora |
| --- | --- | --- |
| Ficha de serie | año suelto bajo el título | «2020 · Drama · 3 temporadas · 16 episodios» |
| Ficha de serie | sin recuento ni barra | barra de la serie y «10/16 vistos» |
| Ficha de serie | sin panel de siguiente episodio | «SIGUIENTE EPISODIO», su código, su nombre, «Reanudar en 17:00» y «▶ Continuar» |
| Ficha de serie | desplegable de temporadas | píldoras, las tres a la vista |
| Fila de episodio | tira de 56 px con un número | tarjeta con miniatura, nombre, «48 min · Visto» y su barra |
| Las dos fichas | banner fijo y lista con barra propia | una sola página que se desplaza |
| Las dos fichas | «Volver a la biblioteca» como píldora | enlace «Volver · Biblioteca» |
| Las dos fichas | marcas personales dentro del banner | columna «Otras acciones» |
| Las dos fichas | herramientas del título bajo la cuadrícula | en la fila de acciones del banner |
| Bandeja de revisión | «movie:761053» y nada más | archivo, carpeta, carátula, tipo, confianza y señales |
| Bandeja de revisión | decisiones bajo la lista | tres decisiones dentro de cada tarjeta |
| Duplicados | títulos y un número | la tabla de ocho columnas con su radio |
| Reproductor | banda superior sin nada en medio | título y línea de lo que se reproduce |
| Reproductor | transporte en dos filas | una fila: atrás, reproducir, adelante |
| Reproductor | atajos sin escribir en ninguna parte | la línea bajo el transporte |
| Degradados | última parada al 19 % | al 30 %, que es lo que el prototipo pide |
| Paleta | sin tinta sobre el lavado del acento | `AccentInkBrush`, medida en los cuatro modos |

## Lo que se cerró después, mirando la tabla de duplicados con datos / Closed afterwards, with data

La primera comparación de esa tabla se hizo sobre una biblioteca sembrada con dos bytes por archivo:
todas las filas decían lo mismo, y cuatro diferencias se escondieron detrás de esa igualdad. Con
tamaños y códecs plausibles sembrados, salieron a la vez. / The first comparison of that table was
made over a library seeded with two bytes per file, which hid four differences behind rows that all
said the same thing.

| Vista | Lo que faltaba | Ahora |
| --- | --- | --- |
| Duplicados | la elección sólo en el radio | borde de acento y lavado del acento en toda la fila |
| Duplicados | el título del grupo en azul | en la tinta de la página, que es como lo escribe el prototipo |
| Enlaces | el acento (5,62:1 en claro) | su tinta (9,03:1 en claro, 11,36:1 en oscuro) |
| Duplicados | «0 MB» para lo que pesa menos | escala hasta los bytes; sólo el cero se queda en blanco |
| Ficha de serie | cada episodio con el color de su nombre | el tono de la serie, caminado 7° por episodio |
| Ficha de serie | «Siguiente episodio» encogido a su texto | 540 px, con «Continuar» en el borde |
| Inicio | ficha de tipo con palabra en el carrusel | sólo el glifo, como el prototipo; la palabra sigue en la cuadrícula |
| Biblioteca | «Añadir medios…» sin su signo más | con él |
| Reproductor | cabecera sin segunda línea para una película | «2019 · Drama · Misterio» |
| Reproductor | «Velocidad de reproducción 1×» | «VELOCIDAD 1×»; el rótulo largo sigue siendo el nombre accesible |

Y dos diferencias más que se quedan, con su razón: / And two more that stay, with their reason:

7. **La insignia de «no disponible» es ámbar en los siete sitios.** El prototipo tiene dos formas
   —una píldora casi negra sobre la carátula y un chip rojo en la tabla—; aquí hay una sola, y la
   puerta que impide la segunda (`UnavailableBadgeTests`) es anterior a esta comparación. / The
   prototype has two shapes; here there is one, and the gate that forbids a second predates this
   comparison.

8. **El transporte lleva un botón de detener que el prototipo no tiene.** Detener no es pausar: cierra
   la sesión y suelta el archivo, que es lo que hace falta para expulsar un disco. / Stopping is not
   pausing: it ends the session and releases the file.

## Lo que sigue siendo distinto, y por qué / What is still different, and why

Seis diferencias, y las seis son decisiones con su medición detrás. / Six differences, each a
decision with a measurement behind it.

1. **Las iniciales en la portada.** El prototipo dibuja el aro y nada más; aquí van dos letras. Una
   cuadrícula de portadas idénticas no se lee sin abrirlas, y esta aplicación no descarga carátulas.
   / The prototype draws the ring alone; this draws two letters, because a wall of identical covers
   cannot be read without opening them and this application downloads no artwork.

2. **Los filtros llevan su círculo.** En los dos diccionarios de contraste alto el relleno elegido y
   el no elegido son el mismo color, así que el glifo no es adorno: es toda la señal. / In both high
   contrast dictionaries the chosen and unchosen fills are the same colour, so the glyph is the whole
   signal rather than decoration.

3. **La columna de paneles del reproductor está siempre.** El prototipo la abre desde cuatro
   píldoras de la banda superior. Cerrarla por defecto dejaría fuera de alcance los controles que el
   paseo autónomo pulsa dentro de ella, y esa cobertura está en cero pendientes. / The prototype
   toggles it from four header pills; closing it by default would put the controls the autonomous
   walk presses out of reach, and that coverage is at zero pending.

4. **«Mantener pendiente» no existe en la bandeja.** El prototipo tiene cuatro botones por tarjeta;
   aquí hay tres, porque un candidato pendiente que se deja en paz ya se queda pendiente y un botón
   que no cambia nada es un botón que miente. / The prototype has four buttons per card; there are
   three here, because a pending candidate left alone stays pending and a button that changes
   nothing is a button that lies.

5. **La fila del episodio no ofrece «Editar metadatos».** El editor de esta aplicación se abre por
   **título**, no por episodio: `CatalogMetadata` está indexado por `TitleId`. Un botón por fila que
   editase la serie sería un botón que dice una cosa y hace otra. / The editor here opens per
   **title**, not per episode, so a per-row button would say one thing and do another.

6. **La ficha de película lleva dos acciones donde el prototipo lleva una.** El prototipo dibuja un
   solo botón cuya etiqueta cambia —«Reanudar» o «Reproducir»—; aquí son dos, porque reanudar y
   empezar de nuevo son dos cosas distintas y la oferta de reanudación existe además. El acento va
   sobre «Reanudar», así que una película sin empezar deja la ficha sin acción acentuada. / The
   prototype draws one button whose label changes; here there are two, because resuming and starting
   again are different things — so a film nobody has started leaves the card with no accented action.

## Lo que el propietario miró el 2026-08-25 por la tarde / What the owner looked at

Nueve cosas señaladas mirando la aplicación, no el prototipo. Seis cerradas en esa misma tanda —dos
herramientas del título que aparecían sin tener nada que hacer, el botón del tráiler externo, el
contorno punteado con el radio equivocado, los rótulos de la tarjeta de revisión en dos estilos, la
columna de «Otras acciones» con dos gramáticas y el convertidor de forma que se retiró—. Las tres que
quedan están descritas en [la nota de la próxima sesión](../../NEXT-SESSION.es.md): el reproductor
como copia exacta, Apariencia con las opciones del prototipo y el selector de color de subtítulos.
/ Nine things the owner named while looking at the application; six closed the same day, three
described in the next-session note.

**Dos decisiones tomadas por el propietario**, y por eso dejan de ser diferencias por resolver:

1. **El botón de detener del transporte se queda** aunque el prototipo no lo tenga: detener no es
   pausar —cierra la sesión y suelta el archivo, que es lo que hace falta para expulsar un disco—.
   / Stopping is not pausing: it ends the session and releases the file.
2. **El panel «Otras versiones» del reproductor se queda.** El prototipo tiene cuatro paneles y esta
   aplicación cinco; el quinto es LIB-008, VERIFIED, y quitarlo sería una regresión registrada.
   / The fifth panel is a VERIFIED feature; removing it would be a recorded regression.

**Y una decisión de diseño nueva, que el prototipo no tiene:** al reproducir se oculta todo menos el
vídeo, y vuelve **al mover el ratón o al pulsar una tecla**. Se comprobó el código del prototipo: no
hay auto-ocultación en él, así que es un requisito propio y no una diferencia. / The prototype has no
auto-hide; this is a requirement of our own.

### Cómo se explora cualquier estado del prototipo / How to reach any prototype state

La copia de trabajo `scratchpad/proto/proto.html` acepta `?press=A|B|C`: pulsa esos nombres en orden,
por `aria-label` o por el texto del botón, con medio segundo entre cada uno. Con eso se llega a
pantallas que no tienen ruta —el reproductor, cada uno de sus cuatro paneles, el minirreproductor y
la pantalla completa— y se fotografían con Chrome sin cabeza. `shoot-player.ps1` lo hace para las
siete del reproductor. / The working copy takes `?press=A|B|C`, which is how a screen with no route
of its own gets photographed.

## La captura no estaba oscura: la lectura sí / The capture was not dark, the reading was

**2026-08-25.** Tres veces se dijo que una captura «salía oscura» —las del prototipo, la del
reproductor, y la de la biblioteca en tema claro— y las tres veces el archivo estaba bien. Medido
sobre la biblioteca en claro: `GetPixel` da `#FBFCFE` en el lienzo y `#E9EEF4` en el raíl, que son
exactamente los dos valores del diccionario claro; el PNG es 100 % opaco; y la **misma imagen
reducida a 750×500 se ve clara**. Lo que oscurece es mirar un PNG de 1500×1000, no tomarlo. / The
same image at half size reads correctly; the file was never dark.

Por eso el color se decide midiendo —`GetPixel`, o el contraste calculado— y nunca mirando una
captura a tamaño completo. La comparación de arriba se rehízo entera a 750×500 después de saberlo, y
una alarma que había levantado —«las portadas de la aplicación están más claras abajo»— resultó ser
un punto de medición mal elegido: a la misma altura relativa, el prototipo da S=39 % L=16 % y la
aplicación S=41 % L=17 %. / Colour is decided by measurement, never by looking at a full-size shot.

`shoot.ps1` guarda desde entonces sin canal alfa. No era la causa —el 99 % de sus píxeles ya era
opaco— pero un PNG con alfa lo compone quien lo muestra, y las capturas del repositorio acaban en
una página cuyo fondo no elegimos nosotros. / Not the cause, but a page we do not own composites it.

## Cómo se rehace esta comparación / How to repeat this comparison

```powershell
$tools = "$env:USERPROFILE\.claude\projects\D--Proyectos-ap-reelume\tools"
& "$tools\seed\bin\Debug\net10.0\seed.exe" "$tools\matrix-root"
$env:AP_LOCALMEDIA_DATA_ROOT = "$tools\matrix-root"
pwsh -NoProfile -File "$tools\shoot.ps1" -Out captura.png -Width 2250 -Height 1500 -Downscale 1.5 `
    -Theme Dark -Language es -Invoke 'Biblioteca;Historias del Muelle' -Wait 12
```

La referencia del prototipo se toma con Chrome sin cabeza y
`--force-prefers-reduced-motion`: sin eso la foto cae a mitad del `apr-in` y sale entre 1,3 y 1,9
veces más oscura, que es como se tomaron las dieciséis primeras. / The prototype reference is taken
with headless Chrome and `--force-prefers-reduced-motion`; without it the photograph lands mid
fade-in and comes out between 1.3 and 1.9 times darker, which is how the first sixteen were taken.
