# Cinco raíces, cinco invitaciones a cambiar de carpeta, y cuatro contestaban a nadie / Five roots, five invitations to change a folder, and four answered nobody

Cuarto trabajo del tramo 7 de la §4. / §4's seventh tranche, fourth piece.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## La caja que aparecía en todas partes / The box that appeared everywhere

Cada fila de reasignación llevaba su `TextBox`, incluidas las de las carpetas que **están exactamente
donde la copia las dejó**. Una restauración de cinco raíces ofrecía cinco invitaciones a cambiar algo y
cuatro de ellas eran la respuesta a una pregunta que nadie había hecho. / Every row carried a text box,
including the ones whose folder is exactly where the backup left it.

`NeedsFolder` la enciende sólo donde se está pidiendo algo: **una carpeta que falta** —que el dominio
llama «un hecho, no un error» donde define el estado— **o un conflicto**, que es el único estado que
detiene una restauración. Y una fila que alguien ya reescribió conserva la suya, para poder corregir una
respuesta equivocada. / Only where something is being asked for.

## El estado que seguía diciendo «falta» / The status that went on saying "missing"

`StatusKey` salía del último ensayo, así que una fila seguía diciendo que la carpeta no estaba **mientras
quien la leía estaba mirando la carpeta que acababa de escribir**. Ahora cuenta con lo tecleado. Un
conflicto **no** se tapa así: es el único estado que bloquea, y sigue siendo lo que es hasta que un
ensayo diga otra cosa. / A conflict is not covered up this way.

## ⚠ Y la guarda que nada puede tomar, por CUARTA vez en esta tanda / And the branch nothing can take, for the fourth time in this batch

El primer intento puso un ternario delante del `switch` que ya existía:

```csharp
public string StatusKey => HasRemap && _status != RootRemapStatus.Conflict
    ? "RestoreRootRemapped"
    : _status switch { … RootRemapStatus.Remapped => "RestoreRootRemapped", … };
```

Y **dejó muerto el arm `Remapped`**: una raíz reasignada es, por definición, una cuya ruta nueva
difiere, así que `HasRemap` es cierto y el ternario corta antes de llegar. La cobertura lo dijo en un
comando: `sin ejecutar: línea 78`. / It left the `Remapped` arm dead, and coverage said so in one
command.

Reescrito como cuatro arms, **todos alcanzables**: conflicto; sin cambios; falta y nadie ha escrito;
y todo lo demás es reasignada. `Unchanged` no necesita guarda, porque su caja nunca aparece y por tanto
nadie puede teclear en ella. Resultado: **100 % de líneas y 93,3 % de ramas** sobre un suelo de 100/75.
/ Four arms, all reachable.

**Es la cuarta vez en esta misma tanda**: el converter de marcadores, el de recursos, las notificaciones
de la copia y ahora esto. La forma tiene una señal fija — **la cobertura la encuentra sola, y llega
antes que el razonamiento**. / Fourth time in this batch, and coverage found it each time before the
reasoning did.

## Las rutas / The paths

Van en `FontFamilyMono` —sexto consumidor— y truncan con `PathSegmentEllipsis`. La §4 pide truncado por
la izquierda «porque las rutas se distinguen por el final»; éste **conserva el final y además la letra
de unidad**, que es lo que importa cuando dos raíces viven en discos distintos, y es lo que la vista
previa de renombrado ya usa para el mismo problema. Mismo problema, mismo tratamiento. / It keeps the
end and the drive letter, which the rename preview already uses for the same problem.

La fila pasa a `Grid` de dos columnas: la carpeta a la izquierda y lo que la fila es a la derecha.

## Lo que queda anotado / What is written down

La §4 pide **pasos numerados** y un **estado vacío**. Los pasos son una reorganización de la vista
entera y no una fila; y el vacío hay que medirlo antes: `Roots` se llena de un ensayo, así que «sin
raíces que reasignar» no es «la lista está vacía» sino «ninguna fila pide nada» — que **sí** es
alcanzable y es una frase distinta. Van en su pieza. / Both are written down rather than guessed at.

## El verde / The green

```
UiTests             698/698
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```
