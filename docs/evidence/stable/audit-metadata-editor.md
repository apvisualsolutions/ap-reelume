# Ocho campos sin etiqueta a la vista, y una sospecha de la §4 que el código ya había descartado / Eight fields with no visible label, and a §4 suspicion the code had already ruled out

Tercer trabajo del tramo 6 de la §4. La fila pide los tres mensajes como bloques con glifo y anota «la
forma en que hoy pueden solaparse». **Medir dijo que no pueden — y encontró otra cosa peor al lado.** /
§4's sixth tranche: the row asks for the three messages as blocks and notes how they can overlap today.
Measuring said they cannot, and found something worse beside them.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## La sospecha de la §4, descartada / The §4 suspicion, ruled out

Los tres mensajes vivían los tres en `Grid.Row="9"`, así que la sospecha era razonable. Pero los tres
salen del **mismo `result.Outcome` en el mismo método**: / All three sat in one grid row, so the
suspicion was reasonable — but all three come out of one `result.Outcome` in one method:

```csharp
private void ApplyResult(MetadataWriteResult result)
{
    HasConflict         = result.Outcome == MetadataWriteOutcome.Conflict;
    IsUnidentified      = result.Outcome == MetadataWriteOutcome.NotIdentified;
    HasNoProviderAnswer = result.Outcome == MetadataWriteOutcome.Unavailable;
```

Exactamente uno es verdadero en cada llamada, y **`grep` de las asignaciones dice que ése es el único
sitio**. No se solapan. **Novena discrepancia §4↔árbol, y de la clase buena**: el documento señaló un
riesgo que el código ya había cerrado. / Exactly one is ever true, and that is the only assignment site.

**Aun así se separan en tres filas.** La garantía vive dentro de un método privado, y una fila
compartida convierte a cualquier segundo escritor futuro en tres mensajes pintados uno encima de otro —
la clase de cosa que nadie ve hasta que alguien la reporta. Separarlas cuesta dos filas de `Grid`. /
The guarantee lives inside a private method; separating the rows costs two grid rows.

Y la prueba lo afirma **por geometría, no por índice de fila**: lo que importa es si se dibujan encima,
no cómo se escribió la rejilla. Montada sin contexto de datos, que es lo único que pone los tres en
pantalla a la vez. / Asserted by geometry and not by row index, with all three on screen at once.

## Lo que la fila no sabía: ocho campos mudos / What the row did not know: eight silent fields

Los ocho `TextBox` del editor llevaban sus palabras **sólo** en `AutomationProperties.Name`: / All eight
text boxes carried their words in the accessible name only:

```xml
<TextBox Grid.Row="2" Text="{Binding OriginalTitle}"
         automation:AutomationProperties.Name="{DynamicResource MetadataOriginalTitleLabel}" />
```

**Un lector de pantalla oía «Título original» y quien mirase veía ocho cajas idénticas.** Título, título
original, sinopsis, año, géneros, cartel, fondo y el texto alternativo de la ilustración. Las ocho
cadenas existían en los dos idiomas desde siempre; faltaba pintarlas. / A screen reader heard the label
and anybody looking saw eight identical boxes; the strings had always existed.

**Es el mismo defecto que el spinner de escaneo, ocho veces.** Y tiene la misma causa de fondo: **una
casilla lleva sus palabras en su contenido y un `TextBox` no**, así que las casillas de bloqueo que
estaban justo al lado siempre se leyeron bien y nadie notó que sus vecinas no. La prueba afirma sobre
**todos** los campos, porque el noveno que se añada se equivocaría igual. / A checkbox carries its words
in its content and a text box does not, which is why the lock boxes beside these always read fine.

## Las dos gramáticas, y por qué son dos / The two grammars, and why two

Conflicto y «sin identificar» son los dos **«lo que pediste no ocurrió»**, y toman
`WarningSurfaceBrush` con `WarningBorderBrush` y el glifo `⚠`. «El proveedor no contestó ahora mismo»
**no es un fallo ni es culpa de nadie**, así que se queda como dato en texto secundario, sin caja. El
glifo es lo que separa a los dos del tercero **donde el color no puede**: los dos temas de alto
contraste. / A conflict and an unidentified title are both "what you asked for did not happen"; a
provider with no answer right now is neither a failure nor anybody's fault.

## Y dos cosas de forma que venían de paso / And two shape fixes that came along

- **El título del editor pasa a nivel 2 y a `FontSizeSubtitle`.** No tenía nivel de encabezado ninguno,
  y es una sección del panel de la biblioteca — cuyo nivel 1 es de `LibraryView`, medido. Es la misma
  lección que la página de Ajustes: **una decisión de jerarquía se mide sobre el panel ensamblado.**
- **La fila de acciones pasa a `WrapPanel`**, por la razón escrita ocho veces aquí.

## El verde / The green

```
UiTests             680/680
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
ArchitectureTests   30/30
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```
