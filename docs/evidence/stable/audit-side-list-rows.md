# Las filas de 36 px, y lo que estaban pintando mientras nadie miraba / The 36 px rows, and what they were painting while nobody looked

Octavo trabajo del tramo 4 de la §4. Pedía altura, truncado y tooltip; lo que la medición encontró
primero fue **qué decían esas filas**. / §4's fourth tranche; §4 asked for height, truncation and a
tooltip, and measuring asked first what those rows were saying.

**Corrección: cuando esto se escribió decía que cerraba el tramo, y no lo cerraba.** Quedaba
`LooseFileBanner`, cuya viñeta nunca se había tachado — [su auditoría](audit-loose-file-band.md). /
Correction: this said it closed the tranche, and it did not.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El defecto, en su octava forma / The defect, in its eighth form

Ni `MarkerEditorView` ni `DetectedMarkerReviewView` declaraban `ItemTemplate`. Un `ListBox` sin
plantilla pinta el `ToString()` de cada elemento, y los elementos son **records de dominio**. Medido en
la columna de 320 px: / Measured in the 320 px column:

```
row h=44 tip=(none) trim=None wrap=NoWrap w=292
  'IntroMarker { Id = 11110001-0000-4000-8000-000000000001, SeriesId = SeriesId { Value =
   d1f70001-0000-4000-8000-000000000001 }, Kind = Intro, Start = 00:00:30, End = 00:02:00,
   Origin = Manual, Confidence = , UserCorrected = False }'
```

Dos GUID y el nombre de la clase, en 292 px de ancho, **sin elipsis y sin tooltip**: lo que sobra se
cortaba y no había forma de leerlo. **Fijar la fila a 36 px sin arreglar la etiqueta habría
formalizado el defecto** — una fila más baja truncando un GUID hacia un tooltip que enseña el mismo
GUID. / Formalising it is what fixing only the height would have done.

**La misma ausencia llegó a dos sitios más:**

- **El selector de tipo de marca pintaba `Intro`, `Recap` y `Credits`** —los nombres de los miembros
  del `enum`, sin traducir, en español— porque **no existía ninguna clave** para los tres valores de
  `MarkerKind`. / The kind picker painted the enum member names, untranslated.
- **`UserCorrected` no lo pintaba nadie.** Es lo que aceptar o corregir una detección escribe, y lo
  que la protege de la siguiente pasada del detector: pulsar «Aceptar detección» cambiaba el modelo y
  **dejaba la lista con exactamente el mismo aspecto**. / Pressing accept changed the model and left
  the list looking identical.

## Lo que las filas dicen ahora, medido en los dos idiomas / What the rows say now, measured in both languages

```
es-ES MarkerList          h=36 'Introducción · 0:30–2:00'
es-ES MarkerList          h=36 'Créditos · 46:40–50:00'
es-ES MarkerList          h=36 'Resumen · 1:01:40–1:03:25'
es-ES DetectedMarkerList  h=36 'Introducción · 0:10–0:35'
es-ES DetectedMarkerList  h=36 'Créditos · 46:40–50:00 · confirmada'
en-US MarkerList          h=36 'Intro · 0:30–2:00'
en-US MarkerList          h=36 'Credits · 46:40–50:00'
en-US MarkerList          h=36 'Recap · 1:01:40–1:03:25'
en-US DetectedMarkerList  h=36 'Intro · 0:10–0:35'
en-US DetectedMarkerList  h=36 'Credits · 46:40–50:00 · confirmed'
```

El separador es **el que `QualityLabel` ya usaba en esta misma columna**, para que dos listas vecinas
puntúen igual; la hora aparece sólo cuando la hay. / The separator is the one already used in this
column; the hour shows only when there is one.

**`Confidence` se queda fuera, y eso es una decisión, no el mismo olvido.** Es el detector discutiendo
consigo mismo, y un porcentaje en la fila invita a discutir sobre un número con el que no se puede
hacer nada. `UserCorrected` sí se dice porque **lo puso una persona** y decide qué puede tocar la
siguiente detección. Escrito aquí porque **dejarlo fuera en silencio es indistinguible de haberlo
olvidado**. / Written down because leaving it out silently is indistinguishable from forgetting.

## Los 36 px no salen de `Height` a solas / The 36 does not come from `Height` alone

Medido antes: la fila daba **44 px con `MinHeight 0`**. La altura de una fila es el relleno del
`ListBoxItem` más una línea de texto, así que el `Setter` que nombra el número no es el que lo decide
— la misma forma que el `ProgressBar` que leía 4 donde se pidió 3. Van los dos: `Height 36` y
`Padding 8,0`. / The row's height is the container's padding plus a line of text.

Y **el selector es de descendencia, no de hijo**: el contenedor se genera dentro del panel de
elementos, así que `ListBox.side-list > ListBoxItem` no casa con nada y **fallaría en silencio**. /
A descendant selector, because a child selector would match nothing and fail silently.

Las tres cosas viven en `DesignTokens.axaml` bajo `side-list` y `row-label`, porque **las cuatro
listas son un solo cambio de la §4** y repetirlas por vista es como tres acaban de acuerdo y la cuarta
no. / The four lists are one §4 change.

## Las cuatro, una por una / The four, one by one

| Vista / View | Lo que era / What it was | Lo que es / What it is |
|---|---|---|
| `MarkerEditorView` | sin `ItemTemplate`, fila de 44 | plantilla, `row-label`, fila de 36 con tooltip |
| `DetectedMarkerReviewView` | sin `ItemTemplate`, fila de 44 | igual, más el estado de confirmada |
| `PlayerVersionsView` | `Grid` con la etiqueta **envolviendo** | `Height 36` y truncado: en una fila de alto fijo, envolver **esconde** la segunda línea |
| `TrackSelectorView` | `DisplayMemberBinding` | plantilla con truncado y tooltip — **las dos son excluyentes**, así que la vieja se va, no se sobrescribe |

El nombre de una pista es **la única cadena de esta columna que nadie puede acortar**: sale del
archivo. / A track's name is the one string here nobody can shorten.

El scroll horizontal ya estaba en `Disabled` en las dos listas, medido; ahora además está **declarado**,
porque la §4 dice «jamás» y lo que no se declara depende de un valor por omisión. / Already `Disabled`
when measured; now declared, because a default is not a decision.

## Lo que la prueba mide y lo que no puede / What the test measures and what it cannot

La opción de un desplegable se mide **en un `ComboBox` propio**, no en los del selector de pistas: un
desplegable tiene que estar abierto para que sus contenedores existan, y abrir el real pediría un motor
y un repositorio de preferencias para contestar a una pregunta sobre un estilo. Lo que une las dos
mitades se afirma al lado: **los dos selectores llevan `side-list`**, y esto es lo que `side-list` le
hace a una opción. Y los contenedores se buscan por el **árbol lógico**, porque un desplegable abierto
vive en una raíz de ventana emergente propia y no está bajo los visuales del control. / The containers
are found through the logical tree: an open dropdown lives in a popup root of its own.

## El verde / The green

```
UiTests             655/655
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
DocumentationTests  87/87
ArchitectureTests   30/30
verify-docs.ps1     215 Markdown, 32 localizados / localised, 58 feature IDs, 46 MVP IDs
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```

Las nueve del converter cubren sus ramas por separado —el reloj a los dos lados de la hora, el valor
que nunca se le dio, y la dirección en la que no va—, porque **una pieza cuya única cobertura es el
camino feliz a través de una vista es una pieza cuyos respaldos no ha ejecutado nadie**. / Nine of them
cover the converter's own branches: a piece whose only cover is the happy path through a view is a
piece whose fallbacks nobody has run.
