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

## Lo que sigue siendo distinto, y por qué / What is still different, and why

Cinco diferencias, y las cinco son decisiones con su medición detrás. / Five differences, each a
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
