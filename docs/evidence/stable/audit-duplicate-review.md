# Dos copias del mismo título se comparaban desplazándose entre ellas / Two copies of one title were compared by scrolling between them

Quinto trabajo del tramo 6 de la §4. La fila pide comparación lado a lado, monoespaciado y estado
vacío: **dos se hacen y el tercero se rechaza con su medición**. / §4's sixth tranche: of the three
things the row asks for, two are done and the third is refused with its measurement.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lado a lado, que es de lo que va la pantalla / Side by side, which is what the screen is for

Las versiones se apilaban una debajo de otra en un `ItemsControl`. **Comparar dos archivos así es
desplazarse entre ellos**, que es justo lo que esta pantalla existe para no tener que hacer. Pasan a un
`UniformGrid` de dos columnas, y una tercera copia **envuelve bajo la primera** sin que nadie decida
dónde va. / Comparing two files that way means scrolling between them, which is what this screen exists
to avoid.

**No virtualiza, y aquí da igual — en ningún otro sitio de este árbol daría.** Un grupo de versiones son
las copias de un título, y `GroupMediaVersions` **lanza con menos de dos**: esta lista mide dos o tres,
nunca diez mil. Es la excepción que confirma la medición del `WrapPanel` contra el
`VirtualizingStackPanel` (7× el tiempo, 455× los controles vivos sobre diez mil entradas). / It does not
virtualize, and that is fine here and nowhere else.

## Las cifras, alineadas / The figures, lined up

`Quality` da cosas como `3840×2160 HDR HEVC` y `1920×1080 H264`. **Sólo se leen como una comparación si
los caracteres caen unos debajo de otros**, así que toman `FontFamilyMono` — el tercer consumidor de la
familia declarada ayer, que es lo que la justificó. / The figures only read as a comparison when the
characters line up.

## Y un borde que no tenía pincel / And a border with no brush

Cada tarjeta llevaba `BorderThickness="1"` **sin `BorderBrush` ninguno**, así que la línea que separa
los datos de un archivo de los del siguiente era la que el tema base diera por defecto — no una
decisión, un resto. Toma `ShellBorderBrush`, como el resto de las superficies del árbol. / A thickness
with no brush is not a decision, it is a leftover.

## Lo que se rechaza, con su número / What is refused, with its number

La §4 pide estado vacío con cadena nueva. **No lo puede ver nadie:**

- `GroupMediaVersions.ExecuteAsync` **lanza** `ArgumentException` con `Versions.Count < 2`, así que un
  grupo nunca tiene menos de dos.
- `ShellView` monta esta vista dentro de `IsVisible="{Binding HasDuplicates}"`, y `HasDuplicates` es
  `Duplicates is not null`, así que sin grupo **la vista no existe**.

Una cadena que ningún estado alcanza es peor que no tenerla: no se puede probar, no se puede traducir
contra nada, y afirma que la pantalla tiene un caso que no tiene. **Décima discrepancia §4↔árbol**, la
segunda de la clase «estado inalcanzable» — la primera fue la lista de hosts de privacidad. / A string
no state can reach is worse than none.

## Y el encabezado, que era de página / And the heading, which was a page's

El título usaba `FontSizeTitle` **sin nivel de encabezado ninguno**, en un panel cuyo nivel 1 es de
`ReviewInboxView`. Pasa a nivel 2 y `FontSizeSubtitle`, como las secciones de Ajustes y las dos de
metadatos. Van cuatro vistas en las que la misma medición ha dicho lo mismo: **la jerarquía se decide
sobre el panel ensamblado.** / Four views now where the same measurement has said the same thing.

## El verde / The green

```
UiTests             687/687
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```
