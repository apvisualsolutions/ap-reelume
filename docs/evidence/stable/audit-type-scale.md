# La tipografía: trece tamaños, no doce / The type scale: thirteen sizes, not twelve

Treinta archivos decidían por su cuenta de qué tamaño era su texto. Eso no es una escala. / Thirty
files each decided on their own how big their text was. That is not a scale.

Fecha / Date: 2026-08-19. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo medido antes de tocar nada / Measured before touching anything

**52 usos literales de `FontSize` en 30 archivos**, con **trece** tamaños distintos: 12, 14, 16, 17,
18, 20, 22, 24, 26, 28, 30, 32 y 34. La decisión escrita decía doce, y **el que faltaba es el `17` de
`ShellView.axaml:140`**. / 52 literal uses across 30 files, with thirteen distinct sizes. The written
decision said twelve, and the missing one was the 17.

**Se coloca por lo que el texto es, no por la distancia numérica.** El `17` está a un punto tanto del
16 como del 18, así que el número no decide. Es la **descripción de bienvenida**: un párrafo que se
ajusta, con `MaxWidth` y `TextWrapping`, bajo un título de 34. Los `18` que van a `FontSizeSubtitle`
son todos **encabezados de riel o de sección**. Un párrafo es cuerpo, así que **17 →
`FontSizeBody`**. / The 17 sits one point from both 16 and 18, so the number decides nothing. It is
the welcome description — a wrapping paragraph under a title — while every 18 mapped to Subtitle is a
rail or section heading. A paragraph is body text.

## El mapeo, y el token que NO se declara / The map, and the token not declared

```
34, 32          -> FontSizeDisplay   32
30, 28, 26      -> FontSizeTitle     28
24, 22, 20, 18  -> FontSizeSubtitle  20
17, 16, 14      -> FontSizeBody      14
12              -> FontSizeCaption   12
```

**`FontSizeMono` no se declara.** Estaba en la decisión —13, para rutas, hashes y códecs— y **nada lo
gasta hoy**: la puerta de escalares, puesta hace tres commits, lo rechazaría. Llega con la primera
ruta o hash que lo necesite. / `FontSizeMono` is not declared: nothing spends it, and the scalars gate
from three commits ago would refuse it.

**Y la puerta lo demostró en vivo**: declarados los cinco tokens antes de gastarlos, la suite dio /
And the gate proved itself live — with the five declared and not yet spent:

```
FontSizeBody, FontSizeCaption, FontSizeDisplay, FontSizeSubtitle, FontSizeTitle — declared in
the theme and read by no .axaml under src/.
```

## Las tres redes de una migración mecánica / Three nets for a mechanical sweep

Un guion sobre treinta archivos necesita más que «compila», que para AXAML casi no dice nada: / A
script over thirty files needs more than "it builds", which for AXAML says almost nothing:

1. **La cuenta, antes y después**, dentro del propio guion, que aborta si no cuadra:
   `before=52 replaced=52 files=30` → `after=0 resource references=52`.
2. **La línea base de la maqueta**, que sí cazó algo (abajo).
3. **Una puerta nueva** que impide volver atrás, probada fallando en las dos direcciones.

## Lo que la línea base cazó, que es el valor de tenerla / What the baseline caught

`HomeLayoutTests` comparó 36 combinaciones de tamaño, escala, tema e idioma. **Cambió un solo
campo**: / It compared 36 combinations and exactly one field moved:

```
LibraryEntryBottom  283 -> 282   (24 registros: escalas 150 % y 200 % / 24 records: at 150 % and 200 %)
                    284 -> 282
```

Nada más: ni el orden de foco, ni el primer foco, ni la visibilidad, ni
`LibraryEntryWithinFirstViewport`, que sigue en `True` en las 36 — que es la garantía que esa prueba
existe para dar. / Nothing else moved, and `LibraryEntryWithinFirstViewport` stays true in all 36,
which is the guarantee that test exists to give.

**Y el cambio va hacia la consistencia**: antes el borde inferior de la entrada a la biblioteca
**dependía de la escala** —283 al 150 %, 284 al 200 %— y ahora es **282 en todas**. La línea base se
aprueba con eso dicho. / The change is toward consistency: the bottom edge used to depend on the
scale and is now the same at every scale.

## La puerta / The gate

`No_view_writes_a_font_size_of_its_own_instead_of_asking_the_scale`, hermana de la que
`ReducedMotionTests` hace con las duraciones y por la misma razón: **un literal es invisible para
cualquier cosa que quiera cambiar la aplicación entera de una vez**. Cuenta lo que queda, no lo que
cambió, así que sigue valiendo según se añaden vistas. Con suelo anticeguera de 52 referencias. /
It counts what remains rather than what changed, so it stays true as views are added.

Probada fallando en las dos direcciones, revertidas: / Proved failing both ways, reverted:

```
un literal que vuelve / a literal returns          -> rojo nombrando el archivo y la línea
una referencia que desaparece / one goes missing   -> "only 51 font sizes come from the scale…"
```

## El verde / The green

```
ScalarTokenTests                              3/3
ApSolutions.LocalMedia.UiTests              594/594
ApSolutions.LocalMedia.AccessibilityTests   133/133  (las 30 vistas se dibujan / all 30 views render)
dotnet build -c Release -warnaserror          0 advertencias / 0 warnings
dotnet format --verify-no-changes             limpio / clean
```
