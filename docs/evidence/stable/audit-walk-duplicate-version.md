# La copia que se reproduce, elegida con el ratón / The copy that plays, chosen with the mouse

El último control de la tanda 5, y el único de las cuatro últimas sesiones que **no destapó ningún
defecto**: la comparación de duplicados hace lo que dice. / The last control of batch 5, and the only
one in four sessions that turned up no defect: the duplicate comparison does what it says.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 61 | **62** |
| Pendientes / Pending | 67 | **66** |

Con esto **la tanda 5 queda cerrada**: la bandeja de revisión y los duplicados, siete controles,
todos pulsados. / Batch 5 is closed: the review inbox and duplicates, seven controls, all pressed.

## Dónde vive / Where it lives

La escena no monta nada nuevo: continúa la primera del paseo, donde la aplicación **vigilando una
carpeta** cataloga una copia soltada después de abrir la ventana y la agrupa con la que ya estaba. Ese
grupo es el que una persona abre desde su ficha, y abrirlo lleva a Revisión, con la comparación debajo
de la bandeja. Todo lo caro —el vídeo de verdad, el vigilante, el escaneo— ya estaba pagado. / The
scene continues the walk's first one rather than staging anything new.

## La sonda es la preferencia, no la pantalla / The probe is the preference, not the screen

Un radio marcado no prueba nada aquí, y por una razón concreta: **sin preferencia guardada la política
ya contesta con una de las dos copias**, por calidad. Leer `IsEffective` habría llamado a «la copia
mejor» y a «la copia que alguien eligió» lo mismo. La sonda es `preferred_media_file_id` del grupo:
**nulo antes de pulsar**, y después exactamente el archivo del radio pulsado. / Without a stored
preference the policy already answers with one of the two copies, so reading the screen would have
conflated "the better copy" with "the copy somebody chose".

Y el radio que se pulsa es el que **no** es ya el efectivo: las dos copias son el mismo material, así
que cuál prefiere la política por su cuenta es asunto suyo, y el paseo se lo pregunta a la pantalla en
vez de suponerlo. / The radio pressed is the one that is not already effective, and which that is gets
asked rather than assumed.

## El registro / The ledger

El control se ancla por su dato —el `ShortPath` de su fila— y se anota como `{Binding ShortPath}`, que
es como el inventario lo declara: un mando repetido en una lista se nombra por la fila a la que
pertenece. Es el segundo del repositorio así, con el `{Binding Title}` de la biblioteca. / The control
is anchored by its own data and recorded under the binding the inventory declares it with.

## Las puertas / The gates

`dotnet format --verify-no-changes --severity warn`, compilación con `-warnaserror` (0 avisos),
accesibilidad (95) y `eng/check-walk-coverage.ps1`: **129 controles declarados en 128 identidades; 62
pulsados, 66 pendientes**. / All green.
