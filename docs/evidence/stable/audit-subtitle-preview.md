# Una vista previa que previsualizaba una cosa de cinco / A preview that previewed one setting out of five

Séptimo trabajo del tramo 4 de la §4. La fila pedía mover la vista previa a la superficie del
reproductor; **medirla encontró que no previsualizaba**. / §4 asked to move the preview; measuring it
found it was not previewing.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El defecto / The defect

`SubtitleStyleView` tiene cinco controles: familia tipográfica, tamaño, **color del texto**, **color
del fondo**, **opacidad del fondo** y grosor del contorno. Su vista previa pintaba con
`CardSurfaceBrush` y `TextPrimaryBrush` —los del tema— y sólo enlazaba `FontFamily`. **Cuatro de los
cinco mandos no cambiaban nada que nadie pudiera ver.** / Four of the five controls changed nothing
anybody could see.

Es el defecto de la casa **con el nombre más amable posible**: un panel llamado «vista previa» que no
previsualiza. Medido con un grep: `ForegroundHex` y `BackgroundHex` sólo aparecían en sus campos de
edición. / The house defect with the friendliest possible name.

## La corrección / The fix

- La muestra pinta **los colores elegidos**, con un convertidor que además aplica la opacidad **al
  color y no al control**: la opacidad de un control desvanecería también el texto que se apoya en él.
- La superficie de debajo pasa a **`PlayerSurfaceBrush`**, que es la fila de la §4 y no es cosmética:
  **un color juzgado contra el gris de una pantalla de ajustes no es el color que se verá sobre una
  película**. / A colour judged against a settings page is not the colour anybody sees over a film.
- **Un valor a medio escribir no tumba la vista previa.** Los dos colores se teclean, así que cada
  estado intermedio de cada pulsación llega al convertidor; lo ilegible cae a blanco opaco, que es lo
  que un subtítulo es. / A half-typed value falls back rather than failing.

**Y una hipótesis mía que la prueba refutó:** escribí `"#FF00"` en la lista de valores inválidos y
**Avalonia lo acepta** —es ARGB corto—. El convertidor estaba bien; la prueba estaba mal, y se corrigió
la prueba. / The converter was right and my assumption was wrong.

## La puerta que saltó, y por qué NO se relajó / The gate that fired, and why it was not relaxed

```
[Major] theme/SubtitleStyleView/TextBlock.Foreground: A colour is bound straight to view-model state,
so that state has no textual counterpart for anyone who cannot see the colour.
```

**La regla es buena y se queda**: un color que sustituye a un estado deja sin nada a quien no lo
distingue. Pero **una muestra es distinta en especie**: el color no está **en lugar de** otra cosa, el
color **es** la cosa que se está eligiendo, y su valor está escrito al lado en un campo que se puede
leer y editar. / A swatch differs in kind: the colour is not standing for something else.

Así que la puerta gana una **lista de excepciones nombrada** —vista, propiedad y origen exacto—, no una
relajación: una segunda brocha enlazada en esa misma vista **sigue fallando**, la lista sólo encoge, y
su longitud se afirma para que un patrón que dejara de casar no exceptúe todo en silencio. Es el mismo
patrón con que `RepositoryPrivacyTests` recibió `design/`: **se declara, no se afloja**. / Declared,
not loosened.

## Y una trampa del guion, medida de paso / And a trap in the script, measured on the way

`eng/run-accessibility.ps1` **no limpia `artifacts/accessibility` entre ejecuciones**, y su recuento
final lee **todos** los JSON que encuentre. Tras una ejecución fallida, la siguiente informó
`0 critical, 2 major, 0 minor` **con exit 0**: los dos eran del intento anterior. Desde limpio,
`0/0/0`. En CI da igual —el runner empieza vacío— pero **en local puede hacer creer que un defecto
sigue vivo, o peor, tapar que se arregló**. Borrar la carpeta antes es un segundo. / The count reads
every JSON it finds, including a previous run's.

## El verde / The green

```
UiTests             637/637
AccessibilityTests  135/135 en las dos pasadas, 0/0/0 desde limpio / on both passes, clean
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
El paseo / the walk: 136 declaraciones en 135 identidades; 135 pulsadas, 0 pendientes
```
