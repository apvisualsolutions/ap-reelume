# El reproductor gana su superficie, y el silencio de la puerta de contraste se escribe / The player gets its own surface, and the contrast gate's silence is written down

Segundo trabajo del tramo 4 de la §4. / §4's fourth tranche, second piece.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que pedía la fila, y lo que había / What the row asked for

La §4 da al reproductor una **superficie propia, `#0B0D10`**. Había `Panel Background="Black"` en
`PlayerView` y otro en `MiniPlayerWindow`: **negro puro, escrito dos veces, sin token**. / Pure black,
written twice, with no token.

**Es el primer token de este árbol que vale lo mismo en los cuatro temas, y eso es la decisión:** todo
lo demás sigue el tema; esto no, porque **lo que se apoya encima es la imagen**. Y `#0B0D10` en vez de
negro puro para que la banda negra no se lea como un agujero al lado de un fotograma que casi nunca es
negro del todo. / The one surface that does not follow the theme, because what sits on it is the
picture.

Se afirma como **cuatro declaraciones idénticas** además de por lo pintado: un pincel que sólo está
bien en la variante bajo la que corre la prueba es un pincel que no vigila nadie. / Four identical
declarations, because a brush that is only right in one variant is a brush nobody is watching.

## La puerta que saltó, y tenía razón / The gate that fired, and it was right

```
ContrastTokenTests.Every_visual_mode_carries_every_brush
  Assert.Equal() Failure: Collections differ at index 14
```

`RequiredKeys` es una **lista cerrada**: cada diccionario tiene que llevar **exactamente** esas claves,
para que un tema no pueda caer en silencio a lo que pinte el tema base. Una clave de tema nueva **no
entra sin declararse ahí**, y eso es lo correcto. / A new theme key does not get in without being
declared.

## Y la decisión que la puerta NO tomó, escrita en voz alta / And the decision the gate did not make

`ContrastTokenTests` mide el texto primario **sobre cada superficie donde puede caer**, y
`PlayerSurfaceBrush` **no está en esa lista**. Dejarlo fuera sin razón escrita es indistinguible de
haberlo olvidado, así que la razón va con su aserción: **todo lo que el reproductor dibuja sobre la
imagen lleva superficie propia**. Medido — los hijos del panel raíz son el vídeo y **tres `Border` con
fondo**—, y afirmado, para que el día que alguien ponga un `TextBlock` suelto sobre el vídeo la prueba
lo diga en vez de callar. Sin eso, un texto oscuro sobre `#0B0D10` en tema claro sería ilegible y
**ninguna puerta lo vería**. / Left off that list with the reason asserted rather than assumed.

## Un orden que se rompió y se corrigió / An order that broke and was put right

La prueba se escribió **después** del cambio y salió **verde a la primera**, que en esta casa es la
señal de que no ha demostrado nada. Se hizo fallar con la mutación de vuelta —`Background="Black"`— y
contestó lo que tenía que contestar:

```
Expected: #ff0b0d10
Actual:   Black
```

/ A test that passes first time has proved nothing until it has been made to fail.

## El verde / The green

```
UiTests             631/631
AccessibilityTests  135/135 en las dos pasadas / on both passes
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
El paseo / the walk: 136 declaraciones en 135 identidades; 135 pulsadas, 0 pendientes
```
