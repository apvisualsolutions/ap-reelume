# La bandeja vacía es la buena noticia, y estaba en blanco / An empty tray is the good news, and it was a blank panel

Segundo trabajo del tramo 6 de la §4, **y el que gasta dos pinceles que llevaban declarados en los
cuatro temas sin que nadie los pintara**. / §4's sixth tranche, and the piece that finally spends two
brushes declared in all four themes and painted by nobody.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que la §4 pide, y por qué tiene razón / What §4 asks, and why it is right

> `ReviewInboxView` — La bandeja vacía **es el estado deseable**: se pinta en `PositiveSurfaceBrush`
> con glifo, no como un vacío triste.

Y lo es: una bandeja de revisión sin nada dentro significa que **AP Reelume identificó todo lo que
encontró sin tener que preguntarle a nadie**. Un panel en blanco dice justo lo contrario — parece que
algo no cargó. / An empty review inbox means everything was identified without anybody being asked; a
blank panel says the opposite.

## Lo medido / What was measured

La vista **no tenía estado vacío ninguno**, y `IsEmpty` estaba en el modelo desde que se escribió: /
The view had no empty state at all, and `IsEmpty` had been on the model since it was written:

```csharp
public bool IsEmpty => Items.Count == 0;   // ReviewInboxViewModel, sin un solo lector en las vistas
```

Es el defecto de la casa otra vez, del lado del modelo: **una propiedad calculada correctamente que no
lee nadie**. / A property computed correctly and read by nobody.

**Y los pinceles tampoco tenían quien los gastara.** `PositiveSurfaceBrush` y `PositiveBorderBrush`
están declarados en **los cuatro** diccionarios de tema —claro, oscuro y los dos de alto contraste— y su
única aparición en todo `src/` era **su propia declaración**. Un recurso sin lectores es la décima forma
del defecto de la casa, la que además *parece* cobertura. Esta pieza es su primer lector. / A resource
with no readers is the tenth form of the house defect; this is its first reader.

## Lo que se afirma, y las dos mitades / What is asserted, and both halves

- La superficie existe, **es visible con la bandeja vacía** y lleva el color y el borde de los pinceles
  positivos **resueltos del tema**, no escritos en la prueba.
- Lleva **glifo** (`✓`): un estado que se distinguiera del resto sólo por el color no se distinguiría en
  los dos temas de alto contraste, donde el positivo es blanco sobre negro y negro sobre blanco.
- **La lista se esconde en el mismo movimiento.** Un estado vacío encima de una lista vacía dice nada
  dos veces.
- Y la otra dirección: **con un candidato dentro, la superficie no se ve y la lista sí**. Sin esa
  mitad, un bloque siempre visible pasaría la primera.

El glifo va en `Grid ColumnDefinitions="Auto,*"`, por la razón medida **nueve veces** en este árbol: un
`StackPanel` horizontal ofrece anchura infinita y el texto de al lado no envuelve nunca. / Ninth
measurement of that shape.

## Lo que no entra, con su razón / What does not go in, with its reason

La §4 lista tres estados: vacía, con pendientes y **cargando**. Los dos primeros se hacen; **el tercero
no existe**: no hay nada en el modelo que sepa que está cargando —ni `IsLoading` ni equivalente—, así
que pintarlo pediría inventar el estado antes que la vista. Se anota en vez de dejarlo en silencio. /
The third state has nothing in the model that knows it, so it is written down rather than left silent.

## El verde / The green

```
UiTests             676/676
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
ArchitectureTests   30/30
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```
