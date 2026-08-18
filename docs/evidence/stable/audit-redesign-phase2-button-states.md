# Los cinco estados del botón, y de dónde salían antes / A button's five states, and where they used to come from

La fase 2 del paso 6 empieza por el botón, porque hay **95 en las vistas** frente a 18 casillas y 15
cuadros de texto. Y empieza midiendo qué pintaba cada uno de sus estados, que resultó no ser ningún
token de este proyecto. / Phase two of step 6 starts with the button, because the views hold **95 of
them** against 18 checkboxes and 15 text boxes. And it starts by measuring what painted each of its
states, which turned out to be no token of this project's.

Fecha / Date: 2026-08-18. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que pintaba antes / What painted before

Sonda sin ventana que fuerza cada pseudoclase y lee el `ContentPresenter`, que es **quien pinta** —el
`Button` no—: / A headless probe forcing each pseudo-class and reading the `ContentPresenter`, which
is **what paints** — the `Button` is not:

```
:rest         presenter.Background=#33000000  border=Transparent  foreground=Black
:pointerover  presenter.Background=Black (opacity 0,1)  border=Transparent  foreground=Black
:pressed      presenter.Background=#66000000  border=Transparent  foreground=Black
:disabled     presenter.Background=#33000000  border=Transparent  foreground=#66000000
```

Tres cosas, y la tercera casi se cuenta mal: / Three things, and the third was nearly reported wrong:

1. **El borde es `Transparent` en los cuatro estados**, donde el diseño pide 1 px de
   `ShellBorderBrush`. Un botón no tenía forma propia. / **The border is `Transparent` in all four
   states**, where the design asks for one pixel of the control boundary.
2. **Deshabilitado y reposo tienen el mismo relleno.** Lo único que los separaba era el gris del
   texto. / **Disabled and rest have the same fill.** The only thing between them was the text's grey.
3. **El relleno de «sobre» dice `Black` y no es negro**: el pincel lleva `Opacity 0,1`. Leer
   `.Color` e ignorar la opacidad daba «texto negro sobre fondo negro», que se llegó a escribir antes
   de mirar la segunda propiedad. Un color no se lee a medias. / **The pointer-over fill says `Black`
   and is not black**: the brush carries `Opacity 0.1`. Reading `.Color` and ignoring the opacity gave
   "black text on a black fill", which was written down before the second property was looked at. A
   colour is not read by halves.

## Por dónde entra un token, medido a la tercera / Where a token gets in, measured on the third try

Dos formas no funcionaron, y las dos parecían correctas: / Two ways did not work, and both looked
right:

- `Style Selector="Button /template/ ContentPresenter"` — no gana.
- El mismo selector **con el nombre de la parte**, `#PART_ContentPresenter` — tampoco.

Un estilo de aplicación no alcanza los elementos de plantilla que un `ControlTheme` define. Lo que sí
alcanza es el **recurso** que esa plantilla consume, y los doce existen y estaban a la vista: /
An application style does not reach the template elements a `ControlTheme` defines. What does reach
them is the **resource** its template consumes, and all twelve exist:

```
ButtonBackground / PointerOver / Pressed / Disabled
ButtonForeground / PointerOver / Pressed / Disabled
ButtonBorderBrush / PointerOver / Pressed / Disabled
```

Cada uno se redirige al token que le toca con `<StaticResource x:Key="…" ResourceKey="…" />`, que en
Avalonia **vale como entrada de diccionario**: doce alias por tema, cuarenta y ocho en total, y
**ningún valor duplicado** — un alias apunta, no copia. / Each is pointed at its token with
`<StaticResource>` as a dictionary entry, which Avalonia accepts: twelve aliases per theme,
forty-eight in all, and **no duplicated value** — an alias points, it does not copy.

## La inversión, y el token que hizo falta / The inversion, and the token it needed

En los dos temas de alto contraste, pasar el ratón y pulsar **invierten**: el relleno pasa a ser el
color del borde y el texto el de la superficie. El relleno ya tenía token; el texto no, y sin él la
inversión no se puede expresar sin una regla por tema. `ControlTextActiveBrush` es ese token: en claro
y oscuro es el texto de siempre, y en los dos de alto contraste es el color contrario. / In both high
contrast themes, hovering and pressing **invert**. The fill had a token; the text did not, and
without one the inversion cannot be expressed without a per-theme rule.

## Lo que esta pieza NO cierra, y por qué se dice aquí / What this piece does not close

**En alto contraste, deshabilitado sigue siendo indistinguible de reposo.** Está medido y es de
diseño: el relleno deshabilitado *es* la superficie porque esas paletas no tienen un tercer color que
gastar, y la respuesta del diseño es el **borde punteado**, que `Border` no sabe dibujar y necesita
una plantilla propia. La prueba afirma la diferencia donde existe —claro y oscuro— y **afirma el
estado actual donde no**, en lugar de aflojar la aserción para los cuatro temas: una prueba que se
afloja para pasar deja de mirar. / **In high contrast, disabled is still indistinguishable from
rest.** Measured, and by design. The test asserts the difference where it exists and asserts today's
value where it does not, rather than loosening for all four themes: a test loosened to pass stops
looking.

Lo mismo con el borde de 2 px al pulsar en alto contraste: la plantilla base tiene un solo grosor para
todos los estados, así que llega con la misma plantilla propia. Afirmado a 1 px, que es lo que hoy es
cierto, para que el día que cambie sea un rojo aquí. / Same for the 2 px pressed border in high
contrast: asserted at 1 px, which is what is true today, so the day it changes is a red here.

## La puerta, probada fallando / The gate, proved failing

```
the hover fill goes back to the base theme       -> RED
high contrast dark stops inverting the label     -> RED
the control boundary goes transparent again      -> RED
```

## Y un rojo que se produjo solo / And a red that made itself

Al retirar los estilos de plantilla que no funcionaban, el borrado se llevó por delante **el estilo
del anillo de foco**, que había quedado entre ellos. Dos pruebas de la fase 1 cayeron en el acto —el
anillo leía blanco donde su tema pinta amarillo, y un `TextBox` se quedaba sin anillo— y el tema base
tiene su propio adorno de foco, así que la primera prueba de las tres **siguió pasando**: la que sólo
pedía dos bordes concéntricos los encontraba igual. Las otras dos, que preguntan por el color y por
los diez tipos, fue lo que dijo que faltaba algo. / Removing the template styles that did not work
also removed **the focus ring style**, which sat between them. Two phase-1 tests fell at once, and the
base theme has a focus adorner of its own, so the first of the three **still passed**: the one that
only asked for two concentric borders found two. The two that ask about colour and about all ten types
are what said something was missing.

## Verde / Green

```
Correctas! - Con error: 0, Superado: 464, Omitido: 0, Total: 464  (UiTests, dos pasadas)
```
