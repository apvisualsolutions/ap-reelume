# Cinco superficies dibujaban un radio que el diseño no dibuja, y el número por sí solo no basta para emparejarlas / Five surfaces drew a radius the design does not draw, and the number alone is not enough to pair them

Tercer trabajo del barrido de fidelidad de
[ADR-0007](../../adr/0007-every-element-matches-the-prototype.md), después de los botones y de la
versalita. Los catorce botones estaban emparejados; el resto de lo que declara una esquina propia,
no. / The third job of ADR-0007's fidelity sweep, after the buttons and the overlines.

Fecha / Date: 2026-09-03. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Las cinco discrepancias, medidas por la puerta antes de tocar nada / The five divergences, measured by the gate before anything was touched

```
Border.setting-row              dibujaba  8   y el diseño dibuja  10
Border.candidate-card           dibujaba  8   y el diseño dibuja  10
Border.state-chip               dibujaba  8   y el diseño dibuja 999
Border.poster-chip              dibujaba  4   y el diseño dibuja 999
ListBox.side-list ListBoxItem   dibujaba  4   y el diseño dibuja   7
```

**El relevo las nombraba y esta vez acertaba**, cosa que no se dio por hecha: las cinco se
comprobaron contra el diseño antes de escribir la tabla, porque dos premisas del mismo relevo ya
habían resultado cortas ese día. / The handover named them and this time was right, which was not
assumed: all five were checked against the design first.

## Lo que obliga a emparejar por ELEMENTO y no por número / What forces pairing by ELEMENT rather than by number

**Los radios pequeños del diseño tienen dos significados según dónde estén**, y una tabla ordenada
por el número los confundiría siendo perfectamente coherente consigo misma: / **The design's small
radii carry two meanings depending on where they sit**:

- **7** es la fila de la lista lateral **y** la mitad del botón de 14 px que corre dentro de un
  interruptor. / the side list's row **and** half of the 14 px knob inside a switch.
- **10** es la fila de ajustes **y** la mitad del carril de 40×20 de ese mismo interruptor. / the
  settings row **and** half of that switch's 40×20 track.

**Y varios de los «doce radios distintos» son una sola decisión —la píldora— escrita como la mitad de
la altura que toque**: 26 en un círculo de 52, 16 en un botón de 32, 15 en uno de 30. Contarlos como
radios distintos infla el trabajo e invita a inventar escalones que el diseño no tiene. / **And
several of the "twelve distinct radii" are one decision — the pill — written as whatever half the
height happens to be.**

## Dos literales, y por qué no son un descuido / Two literals, and why they are not an oversight

La escala tiene tres tokens —4, 8 y la píldora— y el diseño dibuja 10 y 7. **Redondear al token más
cercano dibujaría una forma que el diseño no tiene**, así que se escribe el número: es la primera
consecuencia de ADR-0007, escrita el día que ese documento se aceptó. `ScalarTokenTests` sigue
prohibiendo escribir un número **que tenga token**, que es otra cosa. / The scale has three tokens and
the design draws 10 and 7. Rounding to the nearest one would draw a shape the design does not have.

## Que la diferencia se ve, contada en píxeles / That the difference is visible, counted in pixels

Dos píxeles de radio es justo la magnitud que un rasterizador puede tragarse, y este repositorio
tiene dos puertas que estuvieron verdes sobre dos píxeles de desalineación visible. Así que se
pregunta lo que una persona vería: **cuánta tinta falta en la esquina**. Una caja redondeada pinta su
relleno en todas partes menos en la esquina que recorta, así que un radio mayor deja ver más fondo en
el mismo cuadrado — y ésa es la lectura que un radio recortado o ignorado no puede fingir. Medido: 10
recorta más que 8, y 7 más que 4. / Measured as ink missing from the corner, which is what a person
sees and the one reading a clamped or ignored radius cannot fake.

## Lo que la puerta NO ve, escrito como número / What the gate does NOT see, written as a number

Todo lo anterior es sobre **clases**. Una vista que escribe el radio directamente en su marcado es
invisible para la puerta entera, y el 2026-09-03 había **86 sitios así en treinta vistas** —56 con el
medio, 30 con el pequeño y dos con la píldora—. / All of the above is about classes. A view writing
its corner straight into its markup is invisible to it: **86 such sites across thirty views**.

**Emparejarlos no es trabajo de clase**: cada uno pertenece a un elemento de una pantalla concreta,
así que se hace vista por vista junto con todo lo demás que esa vista le debe al prototipo. Lo que
sujeta el hueco mientras tanto es un **trinquete**: la cifra puede bajar y no puede subir, y falla en
las dos direcciones diciendo cuál. Una vista nueva gastando un token por reflejo es exactamente cómo
llegaron los 86. / Pairing them is not a class-shaped job. A ratchet holds the direction meanwhile:
the number may fall and must not rise.

## Lo verificado / What was verified

```
UiTests                1175 / 1175
Mutaciones / mutations    3 / 3   (radio del árbol, número de la tabla, clase sin emparejar)
dotnet format             limpio / clean
```
