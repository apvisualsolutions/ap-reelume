# El editor es una vista y no un panel / The editor is a surface, not a panel

Evidencia de la decisión 15 del propietario: el editor de metadatos y el renombrado seguro dejan de
ser un `TabControl` al final del desplazamiento de Biblioteca y pasan a ser la página que el prototipo
dibuja. / Evidence for the owner's decision 15: the metadata editor and safe renaming stop being a
`TabControl` at the bottom of the library's scroll and become the page the prototype draws.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-28.

## Lo que había, y lo que costaba / What was there, and what it cost

Las dos herramientas vivían bajo `HasEditorPanel`, dentro del mismo `ScrollViewer` que la rejilla de
la biblioteca. Abrir una ponía un panel **debajo de todas las fichas**, y el coste está escrito en el
propio arnés desde antes de esta sesión: la escena del editor abre la ventana a **2.000 px de alto**
con el comentario «tall enough for the editor to be on screen without scrolling». / Both tools lived
inside the library's own scroll, so opening one put a panel under every card. The harness had the
cost written down already: its scene opens a 2,000 px window.

## Por qué no es un sexto destino / Why it is not a sixth destination

Medido antes de escribir nada: / Measured before writing anything:

| Comprobación / Check | Resultado / Result |
| --- | --- |
| ¿Los destinos están fijados? / Are the destinations pinned? | Sí: `ShellAssemblyTests` afirma `Home, Library, Review, Duplicates, Settings` **y nada más** |
| ¿Quién recorre `Routes`? / Who iterates `Routes`? | El paseo ensamblado, esperando llegar a cada uno **por su botón del carril** |
| ¿Cómo se declara el carril? / How is the rail declared? | A mano, **cinco** botones en el marcado |
| ¿Hay un patrón para una superficie que no es ruta? / Is there a pattern? | Sí: `IsPlayerVisible`, que **cubre** la ruta de debajo |

Un sexto valor del enum habría roto la primera aserción y dejado al paseo navegando a un sitio sin
puerta. La página va sobre el hueco de Biblioteca: `IsLibraryListVisible` y `IsEditorVisible`, las dos
con la ruta dentro. / A sixth enum value would have broken the first assertion and left the walk
navigating to a place with no door.

## Las dos veces que medir corrigió el diseño / The two times measuring corrected the design

**1. La primera versión dibujó la biblioteca sobre Ajustes.** La lista se ató a `!HasEditorPanel` a
secas, y con eso ya no quedaba nada diciendo de qué ruta se trata: la rejilla entera apareció sobre
todos los destinos. Lo cazó `ThemeTests` contando **16 botones de apariencia donde hay 13**. Por eso
las dos propiedades llevan la ruta dentro, y hay una prueba que lo afirma donde puede leerse. / The
first draft bound the list to `!HasEditorPanel` alone, and the whole library appeared over every
destination. `ThemeTests` caught it by counting sixteen appearance buttons where thirteen exist.

**2. El paseo encontró un callejón que ninguna prueba de unidad habría visto.**

```
TitlePreviewRenameAction matched 0 controls on screen (); a click needs exactly one.
```

Con la página cubriendo la ficha, el segundo botón de la ficha dejó de estar en pantalla: **quien
abría el editor de metadatos ya no tenía forma de llegar al renombrado**. La respuesta es la del
prototipo, que dibuja sus dos píldoras siempre: cada una **abre** su herramienta además de
seleccionarla. / With the page covering the card, the card's second tool was no longer on screen. The
answer is the prototype's: both pills always drawn, and each one opens as well as selects.

## Los tres controles nuevos, con su escena / The three new controls, with their scene

`EditorBackAction`, `EditorMetadataTab` y `EditorRenameTab` llegan con su escena en el mismo commit,
que es la regla. Y las píldoras son `ToggleButton` con la clase `segment` —la misma que el selector de
temporada— porque **el arnés no alcanza nada dentro de un `Flyout`** y sí pulsa un `ToggleButton`
leyendo `IsChecked`. / The three arrive with their scene in the same commit, and the pills are
`ToggleButton`s because the harness reaches nothing inside a `Flyout`.

```
Antes / Before: 228 declared command controls in 223 identities; 203 pressed, 20 pending
Ahora / Now:    231 declared command controls in 226 identities; 206 pressed, 20 pending
```

**El trinquete no se movió.** Los tres se pulsan, y la escena del editor abre además el renombrado
para que las píldoras tengan un efecto que observar: con una sola herramienta abierta hay una píldora
ya elegida, pulsarla no cambia nada, y `PressAsync` rechaza una pulsación cuyo efecto no llega — que
es exactamente por lo que se puede confiar en él. / **The ratchet did not move.**

## Dos defectos propios, encontrados leyendo y midiendo / Two defects of my own

**Un rojo intermitente, y era mío.** Las primeras cuatro pruebas de la página eran `[Fact]` y llamaban
a `Dispatcher.UIThread.RunJobs()`, que fuera del hilo de UI cae sobre **la prueba que corra después**:
en la primera ejecución completa cayó sobre `MarkerUiTests`, y en la segunda no cayó sobre nadie.
Convertidas a `[AvaloniaFact]`, tres ejecuciones seguidas dan 1.010 sin un solo fallo. / An
intermittent red, and it was mine: `RunJobs` off the UI thread lands on whatever test runs next.

**Dos clases de estilo que no existen.** El botón de volver se escribió con `Classes="link"` y el
cabecero con `Classes="caption"`; las del árbol son `link-action` y, para el tamaño, el token
`FontSizeCaption`. Una clase que no existe no falla: deja el control con su estilo por defecto, en
silencio. **No hay puerta que lo cace**, y eso queda anotado. / Two style classes that do not exist. A
class that does not exist does not fail — it leaves the control with its default styling, silently.
**No gate catches this**, and that is written down.

## Verde / Green

```
UiTests            1.010 superadas, 0 con error (tres ejecuciones seguidas)
AccessibilityTests   146 superadas, 0 con error
IntegrationTests     485 superadas, 0 con error, 1 omitida
check-walk-coverage  206 pulsados, 20 pendientes, trinquete quieto
dotnet format --verify-no-changes --severity warn   0
dotnet build -c Release -warnaserror                0 advertencias, 0 errores
```

## Lo que no cambió / What did not change

Ninguna cadena nueva: la página se escribe con `LibraryBackShortAction`, `LibraryBackAction`,
`MetadataEditorTitle`, `MetadataEditorTabLabel` y `RenamePreviewTabLabel`, que ya estaban en los dos
idiomas. Y las dos herramientas siguen siendo las mismas vistas, montadas donde antes: lo que cambió
es dónde vive la página que las contiene. / No new strings, and the two tools are the same views.
