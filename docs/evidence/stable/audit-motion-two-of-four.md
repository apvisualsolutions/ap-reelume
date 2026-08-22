# Dos animaciones de cuatro, y las otras dos contestadas / Two animations of four, and the other two answered

El paso 9 de la fase 6, y la primera vez que esta aplicación mueve algo. / Phase 6's ninth tranche,
and the first time this application moves anything.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El conducto, que era la mitad difícil / The conduit, which was the hard half

`ReducedMotionTests` llevaba la forma escrita desde que se borraron los tokens de duración:
**ninguna vista escribe una duración propia**, y «la primera transición que necesite una declara el
token entonces, **con el servicio leyendo de él**». Eso es lo que se ha hecho, y la parte que faltaba
por decir es la contraria: / The missing half was the other direction:

- `MotionDuration` es un **`TimeSpan`** declarado en `DesignTokens.axaml` (160 ms). Las animaciones lo
  leen por `DynamicResource`.
- `FluentThemeService` **lo lee** para su `MotionDuration` —así deja de tener su propia copia del
  160— **y lo escribe**: `Application.Resources["MotionDuration"] = TimeSpan.Zero` cuando Windows pide
  menos movimiento. / The service reads it and writes it.
- **Una animación no puede preguntarle nada a un servicio: lee un recurso.** Por eso el servicio
  escribe el recurso, y por eso afirmar sólo la propiedad `MotionDuration` habría dejado cada
  animación corriendo a 160 ms con una prueba verde al lado. `ThemeTests` afirma ahora las dos
  mitades. / Asserting only the property would have left every animation running with a green test
  beside it.

**Y una trampa medida al escribir la prueba**: recorrer `Application.Styles` buscando `Style` y
`Styles` encuentra **cero** animaciones, porque el tema llega como `StyleInclude`. Una comprobación
así habría estado de acuerdo con una aplicación que no anima nada. / A walk that did not know about
`StyleInclude` found zero and would have agreed with an application that had none.

## Las dos que se hacen / The two that are done

| Animación | Dónde | Por qué esa y no otra |
| --- | --- | --- |
| `apr-tip` | Tooltip de los destinos del carril | Los destinos **perdieron sus palabras** al pasar a pictogramas de 64 px, y el tooltip es donde están ahora. Es la única de las cuatro que ayuda en vez de decorar. |
| `apr-pulse` | El punto junto a «Escaneando» | Es lo único de esa fila que distingue «sigue trabajando» de «se paró»: el contador salta a tirones, y entre dos saltos un punto quieto y un escaneo terminado se ven igual. |

El punto toma **`state-glyph` además de `scan-pulse`**: es uno de los círculos de la aplicación y lee
al mismo tamaño óptico que los demás, que es lo que `StateGlyphTests` exige de cualquier bloque que
pinte `○ ◐ ●`. / The dot takes the circle class too.

## Las dos que NO se hacen, y no es aplazamiento / The two that are not done, and it is not a deferral

- **`apr-shim`** es el brillo sobre un esqueleto **mientras una lista carga**, y **nada en esta
  aplicación sabe que está cargando**. Lo midió ya la auditoría de `ReviewInboxView`: ningún modelo de
  vista lleva estado de carga. El esqueleto no tiene de qué ser esqueleto. Llega con el primer modelo
  de lectura que informe de ello. / Nothing here knows it is loading.
- **`apr-in`** es la subida de 6 px en cada cambio de pantalla, y **el shell no cambia de pantalla**:
  monta las once y alterna `IsVisible`, que Avalonia no anima —un control invisible no se dibuja, así
  que no hay fotograma del que partir—. Conseguirla exige rehacer el shell alrededor de **un solo
  `ContentControl` cuyo contenido se sustituye**, que es un cambio en cómo se hospeda cada superficie
  de la aplicación y no una línea de marcado. / The shell does not change screens.

## Lo que la puerta de cobertura destapó de paso / What the coverage gate turned up

`RouteStateConverter` bajaba de **100/85 a 100/81** al quitarle la rama del glifo. Mirado de cerca,
tenía **tres guardas que nada en este repositorio puede tomar**: una aplicación nula, un recurso
ausente y un valor nulo, en un converter que sólo corre dentro de una aplicación en marcha y sobre una
clave que una puerta exige en los dos diccionarios. Quitadas, el archivo llega a **100/100 y sale de
la lista de deuda** — el trinquete baja de 217 a 216. / Three guards nothing could take, removed, and
the file reaches the bar.

**La respuesta a una guarda inalcanzable es quitarla, no escribirle una prueba imposible** — y lo que
queda dice la verdad en voz alta: si la clave falta algún día, **la clave misma aparece en pantalla**,
que es un defecto que alguien ve en vez de uno que esto escondía. / What is left says the true thing
out loud.

Y `App.axaml.cs` bajaba por dilución: dos líneas nuevas que ninguna suite ejecuta, porque nada monta
un ciclo de vida de escritorio. En vez de aceptar el suelo más bajo, **la configuración de la barra de
título se extrae a `App.ApplyDesignedChrome`**, que sí se puede afirmar — y con ella se ata el número
que vivía dos veces: `App.TitleBarHeight` y la primera fila de `ShellView` son **el mismo 44**, y una
prueba lo dice. / The number that lived twice is now asserted against itself.

## Y lo que sólo dijo CI: una obligación de TMDB sin base / What only CI said

Quitar el nombre del producto del carril —para que no se escribiera dos veces sobre el título que
Windows dibuja— dejó a la aplicación **sin escribir su propio nombre en ninguna pantalla**. Y los
términos de TMDB piden que su logotipo se vea **«menos prominente que el tuyo propio»**, así que
`TmdbLogoTests` comparaba el alto del logo contra el tamaño del nombre en el carril. Sin carril, la
comparación se quedó sin ancla: *«The shell no longer draws the product name, so there is nothing to
compare against.»* / A term of somebody else's licence, left without its anchor.

**La marca y la firma van a `CreditsView`**, que es la pantalla que trata de la aplicación **y** la
que lleva el logo. La comparación pasa de hacerse entre dos pantallas a hacerse **en una**, que es lo
que la condición significa. / The comparison moves from across two screens to within one.

**Y la lección de proceso: el shell toca `IntegrationTests`.** Esta sesión corrió en local `UiTests`,
`AccessibilityTests` y `DocumentationTests` en cada tramo, y el defecto salió en la cuarta vuelta de
CI. Una vista del shell puede tener consecuencias de **licencia** en una suite que no parece suya. /
A shell view can have licence consequences in a suite that does not look like its own.

```
Suites / Suites: UiTests 728 · IntegrationTests 456 · AccessibilityTests 135 (paseo 135/135,
0 pendientes) · Documentation 87 · Architecture 30
```
