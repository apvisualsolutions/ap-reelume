# «Secciones cortadas»: no era el ancho, era Inicio sin desplazamiento / It was not the width

Evidencia del hallazgo del propietario «secciones cortadas por el ancho», cerrado el 2026-08-28 sobre
el eje que quedaba. / Evidence for the owner's «sections cut off by the width», closed on 2026-08-28
on the axis that was left.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-28.

## El ancho estaba descartado, y las cuatro hipótesis que quedaban / The width was ruled out

`ViewOverflowTests` mide cada vista sola contra los 900 px del `MinWidth` de la ventana, y
`ViewOverflowInShellTests` contra los **836** que el shell deja de verdad. Ninguna de las 48 se pasa.
De ahí salieron cuatro hipótesis vivas: (a) con las listas llenas, (b) con el otro idioma, (c) que sea
**vertical**, (d) con la ventana ancha. / Neither width gate finds anything; four hypotheses survived.

**(b) queda cerrada por puerta.** Las dos suites fijaban `es-ES` como constante, así que el otro
idioma era una suposición y no una medición — y el cromo del mini ya plegó en tres filas una vez por
una palabra inglesa más larga. Ahora el idioma es un parámetro de las dos, y las 48 pasan en los dos.
/ **(b) is closed by a gate**: the language is a parameter of both suites now, and all 48 pass in both.

## (c) era la buena, y el número estaba a la vista / (c) was the one, and the number was in plain sight

Medida nueva: el alto que cada vista **quiere**, con las mismas dos limitaciones que las de ancho —
sin contexto de datos, así que todas las ramas a la vez y todas las listas vacías—. Once vistas piden
más de los 600 px del `MinHeight`: / Eleven views want more than the 600 px minimum:

```
AppearanceSettingsView 1587    LibraryView            753    RootOnboardingView 776
MetadataEditorView      800    PrivacySettingsView    753    MovieDetailsView   705
SubtitleStyleView       702    HomeView               683    LifecycleSettingsView 627
UpdateView              627    (ShellView 8775, que es quien contiene los scrollers)
```

Diez de ellas viven dentro de un `ScrollViewer` del shell. **`HomeView` no.** Era el único destino
montado en un `ContentControl` pelado mientras Biblioteca, Revisión, Duplicados y Ajustes tenían el
suyo, así que en la ventana más pequeña que la aplicación permite **se perdían 83 px de Inicio por
abajo, sin forma de llegar a ellos**. / Ten of them are inside one of the shell's scrollers.
**`HomeView` was not**, so on the smallest window this application allows, 83 px of Home were
unreachable.

Eso es «cortadas», con su número: no el ancho, sino el alto de un destino que no se desplazaba. / That
is "cut off", with its number.

## Cómo costó tres intentos medirlo / Three drafts to measure it

Vale escribirlo porque el error es reutilizable: / Worth writing down because the mistake repeats:

1. **`view.Bounds.Height` con la vista como contenido de la ventana** da siempre el alto de la
   ventana: una vista ahí se estira. Las 48 midieron exactamente 600.
2. **`Measure(...)` a mano sobre un árbol ya organizado** no recalcula `DesiredSize`. Volvieron a
   medir 600.
3. **Dentro de un `ScrollViewer`**, que es como el shell las monta y lo único que da altura sin
   límite al hijo. Ahí aparecieron los once números.

Las dos primeras veces **la puerta se declaró ciega ella misma**: lleva un suelo que exige al menos
una vista alta, con la razón escrita —«después de que Ajustes llegara a 1.797 px»—, y falló diciendo
que los árboles no se estaban organizando. Sin ese suelo, las dos primeras versiones habrían pasado en
verde midiendo nada. / Twice the gate declared itself blind, which is what its anti-blindness floor
exists for. Without it, both early drafts would have passed green measuring nothing.

## Lo que la puerta afirma, y lo que no / What the gate asserts, and what it does not

**No** afirma que una vista sea corta: una vista alta es normal. Afirma que **una vista alta está
dentro de algo que se desplaza**, leído del árbol del shell y no de una lista escrita a mano — una
vista que salga de un scroller mañana la caza el mismo día. / It does not assert that a view is short.
It asserts that a tall one is inside something that scrolls, read off the shell's own tree.

`ShellView` está excluida y eso no es una rendija: es quien **contiene** los scrollers, y sus 8.775 px
son el marcado de todos los destinos a la vez sin contexto de datos que oculte ninguno. / `ShellView`
is excluded because it holds the scrollers rather than being held by one.

## (d) medida y descartada / (d) measured and ruled out

Lo que fallaría en una ventana ancha no es que algo se salga, sino que algo **no crezca**, y eso no lo
miraba ninguna puerta. Medido: cada vista sola en una ventana de **1920**, buscando cuáles se quedan
por debajo. Sólo dos: / What would fail on a wide window is not something overflowing but something
that does not grow. Measured at 1920, only two views stay short:

```
ContinueCardView 332 px      PosterCardView 148 px
```

**Las dos son tarjetas, y una tarjeta tiene su ancho.** Son las piezas que las listas repiten, no
páginas: crecer con la ventana es exactamente lo que no deben hacer. **Ninguna página se queda corta**,
así que la hipótesis muere aquí — y sin puerta, porque lo que habría que afirmar es «una página crece»
y las dos únicas excepciones son legítimas y permanentes. / Both are cards, and a card has its own
width. No page stays short, so the hypothesis dies here — and without a gate, because the only two
exceptions are legitimate and permanent.

Con las mismas dos limitaciones de siempre: sin contexto de datos, y por tanto sin listas llenas. /
With the same two limitations: no data context, and therefore no full lists.

## Lo que sigue abierto / What is still open

**Sólo (a), con las listas llenas.** La cubre en parte el paseo, que recorre la aplicación con datos
sembrados y rechaza un clic que no aterriza — pero «rechaza un clic» no es lo mismo que «mide cada
control», y esa diferencia es lo que queda sin puerta. / **Only (a), with full lists.** The walk covers
part of it, but refusing a click is not the same as measuring every control.

## Verde / Green

```
UiTests            1.014 superadas, 0 con error
AccessibilityTests   146 superadas, 0 con error
check-walk-coverage  206 pulsados, 20 pendientes, trinquete quieto
dotnet format --verify-no-changes --severity warn   0
dotnet build -c Release -warnaserror                0 errores
```
