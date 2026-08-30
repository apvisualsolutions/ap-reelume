# La raíz de un disco no se reescribía, y nada lo decía / A drive root was never rewritten, and nothing said so

Una biblioteca guardada en la raíz de un disco se resolvía como `Remapped` y después **no se
reescribía ni una ruta**, sin error, sin aviso y con la restauración terminando en verde. La décima
tanda lo nombró y lo dejó de pie a propósito, con su rojo pendiente; aquí se mide y se cierra. / A
library stored at the top of a disk resolved as `Remapped` and then **no path was rewritten**, with
no error, no warning, and the restore finishing green. The tenth batch named it and left it standing
on purpose, with its red pending; here it is measured and closed.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-30.

## La costura / The seam

`RootRemapPolicy.Normalize` conserva a propósito el separador de `"D:\"`, porque `"D:"` en Windows
nombra el **directorio actual** de esa unidad y no su raíz. Toda otra ruta lo pierde, para que una
carpeta no se convierta en dos. De esa asimetría —deliberada y correcta— colgaban dos defectos, uno
por cada lado del empalme:

1. `IsUnder` preguntaba si la ruta empieza por `root + '\'`. Para `"D:\"` eso es `"D:\\"`, con lo que
   **no empieza ninguna ruta real**, así que `Rewrite` no encontraba dueño y devolvía la ruta tal
   cual.
2. La concatenación era `owner.NewPath + normalized[owner.OldPath.Length..]`. Con un destino en la
   raíz, `"F:\"` y un sufijo que ya trae su separador daban **dos**.

/ `RootRemapPolicy.Normalize` deliberately keeps the separator of `"D:\"`, because `"D:"` on Windows
names that drive's **current directory** rather than its root. Every other path loses it, so one
folder cannot become two. Two defects hung off that deliberate, correct asymmetry, one on each side
of the join: `IsUnder` asked whether the path starts with `root + '\'`, which for `"D:\"` is
`"D:\\"` and **no real path begins with that**, so `Rewrite` found no owner and handed the path back
unchanged; and the concatenation was `owner.NewPath + suffix`, which with a destination at a root
gave **two** separators.

## El rojo, medido antes de tocar nada / The red, measured before touching anything

Dos pruebas nuevas en `RootRemapPolicyTests`, ejecutadas contra el árbol sin corregir:

```
A_library_at_the_top_of_a_disk_is_rewritten_like_any_other [FAIL]
  Expected: "F:\library\shows\episode.mkv"
  Actual:   "D:\shows\episode.mkv"

A_library_moved_to_the_top_of_a_disk_keeps_one_separator [FAIL]
  Expected: "F:\shows\episode.mkv"
  Actual:   "F:\\shows\episode.mkv"
```

La primera es el defecto entero en una línea: la ruta **sale del otro lado igual que entró**, en el
disco que la persona acaba de decir que ya no usa. / The first is the whole defect in one line: the
path **comes out the far side exactly as it went in**, on the disk the person has just said they no
longer use.

## La corrección / The fix

Un separador con nombre, `IsUnder` preguntando por el que la raíz ya trae, y el empalme uniendo por
**exactamente uno**:

```csharp
var suffix = normalized[owner.OldPath.Length..];
return owner.NewPath.TrimEnd(Separator) + Separator + suffix.TrimStart(Separator);
```

Cuatro combinaciones, y las cuatro salen bien: raíz normal a carpeta, raíz de disco a carpeta,
carpeta a raíz de disco y raíz a raíz —esta última no llega a `Rewrite`, porque un origen igual a su
destino se devuelve antes—. / Four combinations and all four come out right: ordinary root to
folder, drive root to folder, folder to drive root, and root to root — which never reaches `Rewrite`,
because a source equal to its destination is returned earlier.

## Lo que se corrigió además / What else was corrected

El comentario de `A_drive_root_keeps_the_separator_that_makes_it_a_root` decía que el defecto quedaba
«para su propio cambio con su propia evidencia». Ese cambio es éste, así que la frase deja de ser
cierta y se retira: **una nota que sobrevive a lo que describía es la forma en que este árbol acumula
afirmaciones falsas sobre sí mismo**. / The comment on
`A_drive_root_keeps_the_separator_that_makes_it_a_root` said the defect was "left for its own change
with its own evidence". That change is this one, so the sentence stops being true and goes: **a note
that outlives what it described is how this tree accumulates false claims about itself.**

## Cómo se verificó / How it was verified

`Domain.Tests` en verde con las dos pruebas nuevas —585 antes, 585 después de que las dos nuevas
entren y la de los campos huérfanos salga—, y las suites que leen `RootRemapPolicy` fuera de
`Domain`: `IntegrationTests` (`DisasterRecoveryTests`, `RestoreValidationTests`), `UiTests`
(`RestoreWizardTests`) y `AccessibilityTests` (`CanonicalJourney`). / `Domain.Tests` green with both
new tests, and the suites that read `RootRemapPolicy` outside `Domain`: `IntegrationTests`, `UiTests`
and `AccessibilityTests`.
