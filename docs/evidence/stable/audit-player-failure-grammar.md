# Un fallo del reproductor se pintaba con la superficie del shell / A player failure wore the shell's own surface

Primer trabajo del tramo 4 de la §4, y el segundo de los seis pinceles de gramática que se gasta. /
§4's fourth tranche opens, and the second of the six grammar brushes is spent.

Fecha / Date: 2026-08-20. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Qué estaba mal / What was wrong

`PlayerFailureSurface` se pintaba con **`ShellSurfaceBrush`**, que es la superficie sobre la que la
aplicación dibuja **todo lo demás**. Así que la única pantalla que tiene que decir «esto no ha
funcionado» se parecía a la que dice qué códec está en uso. / The one screen that has to say "this did
not work" looked like the one that says which codec is in use.

```
A_failure_wears_the_danger_surface_and_a_glyph_that_is_not_the_warning_one
  Expected: #fffdecea
  Actual:   #fff8fafc
```

## La corrección, y la decisión que lleva dentro / The fix, and the decision inside it

1. **`DangerSurfaceBrush` con `DangerBorderBrush`**, que es el segundo par de los seis pinceles de
   gramática que estaban declarados en los cuatro temas **sin un solo lector**. El primero lo gastó el
   distintivo de «no disponible». / The second of the six grammar brushes to be read by anything.
2. **Glifo propio, `✕`, y no el `⚠` del aviso.** Es la decisión, y la prueba la afirma: se exige que el
   glifo del fallo **difiera** del del aviso. Un fallo y un aviso que compartieran glifo y sólo
   cambiaran de color se distinguirían **por el color**, que es exactamente lo que esta gramática
   existe para no hacer. / A failure and a notice sharing a glyph would be told apart by colour alone.
3. **`RecoveryActionsSurface` pasa a `WrapPanel`** y entra en la tabla cerrada de
   `WrappingSurfaceTests`, que sube de cuatro filas a cinco con su suelo anticeguera.

`✕` es el **quinto** texto literal del árbol que no es idioma, y queda anotado en `SURFACES` junto a
la razón por la que es distinto del cuarto. / The tree's fifth literal symbol, noted with the reason it
differs from the fourth.

## El verde / The green

```
UiTests             630/630
AccessibilityTests  135/135 en las dos pasadas / on both passes
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
El paseo / the walk: 136 declaraciones en 135 identidades; 135 pulsadas, 0 pendientes
```

## Y un rojo de CI que no es del código / And a CI red that is not the code's

Dos runs seguidos fallaron en **`Install ffmpeg`**, antes de compilar nada:

```
Failed to fetch results from V2 feed at 'https://community.chocolatey.org/api/v2/Packages(Id='ffmpeg',
Version='9.0.0')' … Response status code does not indicate success: 504 (Gateway Timeout).
```

Es la **novena** causa de la familia «rojo que se lee mal» y la segunda que es de un servicio ajeno,
después del 503 de `actions/setup-dotnet`. Se reconoce igual que aquélla: **por el paso donde cae** —
antes de que el árbol se toque— y **por la duración**. No se toca el flujo; se reejecuta. / The ninth
cause, and the second that belongs to somebody else's service.
