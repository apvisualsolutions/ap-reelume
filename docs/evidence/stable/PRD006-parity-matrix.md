# La matriz de paridad: la aplicación al lado de su prototipo / The parity matrix: the application beside its prototype

Evidencia de PRD-006. Veintiuna capturas de la aplicación **real**, con una biblioteca sembrada,
frente a las dieciséis del prototipo, en los cuatro diccionarios que el producto tiene. / Evidence
for PRD-006. Twenty-one captures of the **real** application over a seeded library, against the
prototype's sixteen, in the four dictionaries the product has.

Fecha / Date: 2026-08-24. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Cómo se toma, y por qué así / How it is taken, and why this way

Las capturas **no viven en el repositorio**: `artifacts/` está en `.gitignore` y las suites lo
barren. Lo que se archiva aquí es el método y lo medido. / The captures do not live in the
repository: `artifacts/` is gitignored and the suites sweep it. What is archived here is the method
and what was measured.

- **La aplicación es la de verdad**, compilada en Release y arrancada como un proceso: nada de
  montar vistas sueltas en un arnés. Su raíz de datos se pasa por `AP_LOCALMEDIA_DATA_ROOT`, que
  apunta **fuera del árbol** —la primera vez se puso bajo `artifacts/` y una suite se la comió—.
  / The application is the real one, built in Release and started as a process.
- **La biblioteca está sembrada**: 12 títulos —8 películas, una serie de dos temporadas y tres
  episodios, un grupo de duplicados con su montaje extendido— más los 9 archivos que el escaneo
  cataloga sin identificar, que es el estado que una biblioteca real tiene el primer día. Total en
  pantalla: **21 elementos**. / A seeded library of 12 titles plus the 9 files a scan catalogues
  unidentified, which is what a real library looks like on day one.
- **El tema se fija antes de arrancar**, escribiendo `theme.preference` en el `settings.json` de esa
  raíz, para que una captura no dependa de caminar hasta Apariencia. / The theme is set before
  startup by writing the stored preference, so a capture never depends on walking to Appearance.
- **La navegación es UIAutomation**, por nombre accesible y en secuencia (`Biblioteca` → `El Faro de
  Piedra` → `Editar metadatos`): lo mismo que pulsa una persona, y prueba de paso que cada destino
  es alcanzable. / Navigation is UIAutomation by accessible name, in sequence.
- **`PrintWindow` con conciencia de DPI por monitor**, o Windows virtualiza el rectángulo y pinta
  una ventana de 1770 px en un mapa de 1195, perdiendo el pie del riel. / DPI-aware `PrintWindow`.

## Las dieciséis parejas / The sixteen pairs

Ocho vistas, cada una en claro y en oscuro, junto a la captura del prototipo de la misma vista y el
mismo tema. / Eight views, each in light and dark, beside the prototype's capture of the same view
and theme.

| Vista / View | App | Prototipo / Prototype |
| --- | --- | --- |
| Inicio / Home | `app-home-{dark,light}-v3.png` | `proto-home-{dark,light}.png` |
| Biblioteca / Library | `app-library-{dark,light}-v3.png` | `proto-library-{dark,light}.png` |
| Ficha de película / Film card | `app-movie-{dark,light}-v3.png` | `proto-movie-{dark,light}.png` |
| Ficha de serie / Series card | `app-show-{dark,light}-v3.png` | `proto-show-{dark,light}.png` |
| Ajustes / Settings | `app-settings-{dark,light}-v3.png` | `proto-settings-{dark,light}.png` |
| Revisión / Review | `app-review-{dark,light}-v3.png` | `proto-review-{dark,light}.png` |
| Duplicados / Duplicates | `app-duplicates-{dark,light}-v3.png` | `proto-duplicates-{dark,light}.png` |
| Editor / Editor | `app-editor-{dark,light}-v3.png` | `proto-editor-{dark,light}.png` |

Las de la aplicación en `artifacts/ui-captures/T36-app/`, las del prototipo en
`artifacts/ui-captures/T36-proto/`. / Application captures under `T36-app/`, the prototype's under
`T36-proto/`.

## Los cinco altos contrastes y el reproductor / The five high contrasts and the player

La §4 pide alto contraste en el shell, en el reproductor y en un formulario. Los tres están, y dos
de ellos en el diccionario contrario para que ninguno de los dos quede sin mirar. / Three surfaces
in high contrast, two of them in opposite dictionaries so neither is left unlooked at.

| Superficie / Surface | Captura / Capture |
| --- | --- |
| Shell (biblioteca) en alto contraste oscuro | `app-library-hc-dark-v3.png` |
| Formulario (Ajustes) en alto contraste claro | `app-settings-hc-light-v3.png` |
| Ficha de película en alto contraste oscuro | `app-movie-hc-dark-v3.png` |
| Reproductor en alto contraste oscuro | `app-player-hc-dark-v3.png` |
| Reproductor en oscuro | `app-player-dark-v3.png` |

El reproductor se captura por la herramienta de escenas y no por la aplicación entera: abrirlo de
verdad exige LibVLC decodificando un archivo real, y los archivos de la siembra son de dos bytes.
La escena monta `PlayerView` **sola y llenando la ventana**, que es como la hospeda la aplicación.
/ The player is captured through the scene tool: opening it for real needs LibVLC decoding a real
file, and the seeded files are two bytes long.

**Una alarma falsa, medida y descartada**: apilada con un segundo control, la vista quedaba con una
fracción del alto y su banda de transporte —anclada al pie del mismo `Panel`— cruzaba el panel de
fallo centrado, cortando su botón por la mitad. Montada sola a 900 px de alto, el panel se pinta
entero y la banda queda en el pie. **Era el arnés, no la aplicación**, y se comprobó cambiando el
arnés en vez de deducirlo. / A false alarm, measured and dismissed: it was the harness, not the
application, and it was checked by changing the harness rather than reasoning about it.

## Lo que las capturas cerraron / What the captures closed

Tres cosas que sólo se ven mirando, y que ninguna puerta había mirado: / Three things only visible
by looking, which no gate had looked at:

1. **Inicio arrancaba vacía.** La ruta con la que nace el navegador no dispara su propio evento, y
   toda alimentación de superficies colgaba de ese evento. Corregido y con prueba en
   [la ruta inicial que nunca navegaba](audit-initial-route-never-navigates.md). / Home started
   empty: the route the navigator is born on never fires its own event.
2. **El título de una ficha se iba a dos líneas** y empujaba su año por debajo del año de la ficha
   vecina; una fila se leía como un borde dentado. Ahora una línea con puntos suspensivos. / A
   card's title took two lines and pushed its year below its neighbour's.
3. **Un conmutador no era una píldora.** La forma estaba declarada sólo para `Button`, y
   `ToggleButton` no coincide con ese selector: «Favorito» y «Ver más tarde» conservaban la caja
   más baja del tema base junto a las píldoras de su misma fila. / A toggle was not a pill: the
   shape was declared for `Button` alone.

## Recuentos finales / Final counts

| Medida / Measure | Valor / Value |
| --- | --- |
| Vistas `.axaml` (sin diccionarios) / Views | 53 |
| Claves de cadena en español / Spanish string keys | 576 |
| Claves de cadena en inglés / English string keys | 576 |
| Vistas con fila en `LeadingActionTests` / Views with a leading-action row | 48 |
| Escenas del paseo pendientes / Walk scenes outstanding | 0 |
| Capturas de la aplicación / Application captures | 21 |
| Capturas del prototipo / Prototype captures | 16 |

La desviación del Inventario de controles sigue siendo una y está documentada: **«Aplicar» no
existe**, porque el prototipo aplica al elegir y un botón cuyo único trabajo era repetir lo que el
control de al lado ya había dicho es la definición de un control de más. / The one deviation from
the control inventory remains documented: there is no "Apply" button.

## Una corrección posterior, del arnés y no del producto / A later correction, of the harness

Las veintiuna capturas se tomaron a **1600 × 2186** aunque se pidiera 1600 × 1000, y la causa era el
propio guion: en PowerShell `$H` y `$h` son la misma variable, así que el manejador de la ventana
pisaba la altura. Está medido y corregido en
[el arnés mentía sobre su propia geometría](audit-harness-geometry-lied.md). **No invalida nada de
lo anterior** —una ventana más alta enseña más de cada vista, y esta matriz nunca declaró una
resolución—, pero se deja escrito aquí porque la afirmación «la aplicación al lado de su prototipo»
sólo vale si se sabe con qué geometría se tomó cada mitad. / The twenty-one captures were taken at
1600 × 2186 although 1600 × 1000 was asked for, and the cause was the script itself. It invalidates
nothing above — a taller window shows more of each view, and this matrix never declared a resolution
— but it is written here because "the application beside its prototype" only holds if the geometry
of each half is known.
