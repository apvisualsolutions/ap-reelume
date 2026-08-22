# «Añadir medios», al pie del carril / "Add media", at the foot of the rail

La primera pieza de la siguiente tanda: el sexto control del carril, que no es un destino. / The next
batch's first piece: the rail's sixth control, which is not a destination.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que el prototipo pone al pie del carril / What the prototype puts at the foot of the rail

`design/AP Reelume.dc.html`, línea 58: un botón de **46 × 42** con radio 12 y **borde hairline** —lo
único que el prototipo le da y le niega a los cinco destinos—, separado de ellos por un `flex:1`, con
el glifo `plus` y `aria-label` en los dos idiomas. Su cadena ya existía ahí en ambos:
`addMediaLabel: en ? 'Add media' : 'Añadir medios'`. / Its string already existed there in both
languages.

- **Cadena**: `NavigationAddMedia`, en `Strings.es.axaml` y `Strings.en.axaml`. **516 → 517 por
  idioma**, que es exactamente el objetivo del paquete de diseño.
- **Clase**: `Button.navigation-action` en `DesignTokens.axaml`, junto a `navigation-destination`. La
  diferencia entre las dos es el borde, y esa diferencia de dibujo es la diferencia de significado:
  los cinco dicen dónde estás, éste hace algo. No lleva relleno ni barra de 3 px porque **nunca es el
  destino abierto**. / The border is the whole difference, and it carries the whole meaning.
- **Contenedor**: el carril pasa de `StackPanel` a `DockPanel`, y **el botón se declara primero**. Un
  `DockPanel` reparte sus bandas en orden de declaración y el último hijo se queda con lo que sobra:
  escrito después de los destinos se habría quedado con el carril entero. / Declared last it would
  have taken the whole rail.

## Por qué no es un segundo «Biblioteca» / Why it is not a second "Library"

`AppRoute` está afirmado en **exactamente cinco nombres** por
`ShellLocalizationTests.Navigation_contract_exposes_exactly_the_five_approved_destinations`, y la
superficie donde se añade una carpeta vive **dentro** de la ruta Biblioteca. Así que esto es una
acción, no un destino — y navegar habría sido todo lo que hace, que es lo que ya hace el destino de
al lado.

Lo que lo distingue es la segunda mitad: `ShellViewModel.BeginAddMedia` navega **y** llama a
`RootOnboardingViewModel.BeginAdd`, que vacía el formulario. Eran **tres restos**, y cada uno una
manera de que la pantalla respondiese por algo que la persona no hizo: / Three leftovers, each a way
for the screen to answer for something nobody did:

| Resto | Lo que pasaba |
| --- | --- |
| `Path` | Nada la vaciaba al aceptar una carpeta, así que la anterior seguía escrita y un segundo intento contestaba «ya está en la biblioteca» |
| `FailureKey` | Una negativa sólo se borraba cuando el intento siguiente **acertaba**, así que seguía explicándose sobre un cuadro ya cambiado |
| `PendingRemoval` | Una retirada que alguien dejó a medio confirmar seguía esperando al volver |

**El consentimiento del escaneo se deja a propósito**: es una pregunta sobre una carpeta ya guardada,
y tirarlo cancelaría en silencio el primer escaneo de la biblioteca de alguien. / Dropping it would
silently cancel somebody's first scan.

## Medido en la aplicación abierta, y la trampa que costó cuatro capturas / Measured in the running application

`PrintWindow` sobre la aplicación real: el botón está al pie del carril, con su `+`, en una ventana de
1792 × 1151 físicos.

**Y la trampa, que no se deduce y que ya está en la memoria de captura**: un script de PowerShell que
no declara conciencia de DPI recibe de `GetWindowRect` un rectángulo **virtualizado** —1195 × 767 para
una ventana que mide 1770 × 1140—, y `PrintWindow` pinta el contenido real dentro de ese mapa de bits
más pequeño. Lo que se pierde es la esquina inferior derecha, que es justo donde estaba el control que
se quería mirar: **tres capturas dijeron «el botón no se dibuja» y el botón estaba ahí**. Lo que lo
zanjó fue UIAutomation, que sí es consciente del DPI y contestó
`'Añadir medios' rect=405,1445,69,63 enabled=True offscreen=False`. `SetProcessDpiAwarenessContext(-4)`
antes de capturar. / A PowerShell script that is not DPI aware gets a virtualised rectangle and
`PrintWindow` crops the bottom-right corner — exactly where the control was.

## El paseo / The walk

La escena del carril (`The_navigation_rail_and_the_card_actions_are_pressed_with_the_mouse`) lo pulsa
**primero**, porque lo que hace sólo se ve desde otra ruta. La sonda es **una sola cadena con las dos
mitades**, `"{ruta}|{path}"`: dos sondas dejarían pasar una pulsación que navega y no limpia nada, y
limpiar es la mitad que justifica el control. / One probe with both halves; two would let a press that
navigated and cleared nothing pass the first.

```
The walk: 137 declared command controls in 136 identities; 136 pressed, 0 pending.
```

El trinquete sigue en **0**. El bucle de destinos que sigue lee dónde está el shell en vez de que se
le diga, así que ahora arranca desde Biblioteca sin que haya que tocarlo.
