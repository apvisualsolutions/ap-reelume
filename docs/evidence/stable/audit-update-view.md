# La pantalla de actualización, y una prueba que aprobaba antes de existir el cambio / The update screen, and a test that passed before the change existed

`UpdateView` es la primera vista del rediseño que **sólo cuesta maqueta**: sus cinco controles ya
estaban en el paseo, así que no añade deuda de ninguna clase. Lo que le faltaba era la jerarquía —el
botón que es el sentido de la pantalla se pintaba igual que los otros tres— y dos radios escritos como
número al lado de un token que vale exactamente eso. / `UpdateView` is the first view of the redesign
that **costs layout only**: its five controls were already in the walk, so it adds no debt of any
kind. What it lacked was hierarchy, and two corner radii written as numbers beside a token worth
exactly that.

Fecha / Date: 2026-08-20. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El rojo, y lo que enseñó de paso / The red, and what it taught along the way

```
Exactly_one_button_leads_the_update_screen
  Expected: ["UpdateCheckButton"]
  Actual:   []

The_bordered_surfaces_take_their_corner_from_the_theme
  UpdateView.axaml still writes 2 corner radius as a number.
```

**El segundo rojo no existía en el primer intento.** La prueba comparaba el `CornerRadius` pintado
contra el token resuelto, y **pasaba antes de tocar la vista**: `CornerRadiusMedium` vale 8 y los
literales eran 8, así que los números coincidían. Habría dado verde sobre una vista que no gasta
ningún token y que se desincroniza el día que el tema se mueva. / **The second red did not exist in
the first attempt.** The test compared the painted radius against the resolved token and **passed
before the view was touched at all**, because both were 8.

**La forma general, y es nueva en este repositorio**: una prueba que compara el **valor** no distingue
un literal de un token cuando ambos coinciden hoy. Lo que hay que medir es la **fuente**. La casa ya
conocía la mitad de esto —«una prueba que compara números escritos en vistas tiene que resolver los
tokens»— y ésta es la otra mitad: **resolver el token no basta si el número escrito acierta**. /
**The general shape**: a test that compares the *value* cannot tell a literal from a token while the
two agree. What has to be measured is the *source*.

Así que la prueba afirma las dos cosas y por razones distintas: el marcado no escribe ningún radio
como número, y lo pintado es el token resuelto. La primera dice de dónde sale; la segunda, que llegó a
la pantalla. Ninguna sobra: nombrar un recurso en el marcado no prueba que alcance nada, y ya mordió en
la fase 2f. / So the test asserts both, for different reasons: the markup writes no radius as a
number, and what is painted is the resolved token.

## La corrección / The fix

- **`primary-action` en `UpdateCheckButton`**, y es el único candidato: descargar e instalar aparecen
  **según el estado** —no están en pantalla hasta que hay oferta y hasta que se ha descargado— y
  cancelar nunca es el sentido de una pantalla. La prueba lo afirma como **el único**, no como
  presente: dos acciones principales son una pantalla que no ha decidido para qué es, y eso pasaría
  una aserción que sólo buscara una. / **`primary-action` on `UpdateCheckButton`**, asserted as the
  only one rather than as present.
- **Los dos `CornerRadius="8"` pasan a `{DynamicResource CornerRadiusMedium}`.** El token ya se
  gastaba desde el mini reproductor; ahora lo gastan también las dos superficies con borde de esta
  pantalla. / **Both literals become the token.**

## Lo que NO lleva este cambio, y por qué / What this change does not carry, and why

**El espaciado.** `UpdateView` escribe `Spacing="12"`, `"8"` y `"6"`, y los tokens de espacio son
4/8/16/24/32: hace falta un **mapeo**, y ese mapeo vale para **183 sitios de todo el árbol**. Se decide
una vez en el archivo de tokens y no vista por vista, igual que se hizo con los trece literales de
tamaño de letra. Va en su propia fase. / **Spacing.** The mapping is one decision for 183 sites across
the tree, not a per-view one.

Y queda escrito lo que ya se contó en `NEXT-SESSION`: **los cinco `Space*` son `x:Double` y
`Padding`/`Margin`/`BorderThickness` son `Thickness`**, así que el setter no convierte. De los 89
literales de `Padding`/`Margin`, **37 son asimétricos** y ningún token escalar los expresa. /
The five `Space*` are `x:Double` while the properties that need them are `Thickness`.

## El verde / The green

```
UiTests   604/604
```

Con las otras tres suites que un cambio de vistas afecta —`AccessibilityTests`, `IntegrationTests` y
`DocumentationTests`— verdes también, y CI como verificación real. / With the other three suites a
view change affects green too.
