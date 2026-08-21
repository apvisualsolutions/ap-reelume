# Una bandeja que el sistema no tiene era una frase, y el volcado que decide si compartes pedía scroll lateral / A tray the system does not have was a sentence, and the dump that decides whether you share asked for a sideways scroll

Cuarto trabajo del tramo 5 de la §4, **y el que lo cierra**. Dos piezas pequeñas y una puerta que
resultó ser más estricta que el árbol por accidente. / §4's fifth tranche, and the piece that closes
it: two small changes and a gate that turned out stricter than the tree by accident.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## La bandeja que no hay / The tray that is not there

`LifecycleCloseToTrayUnavailable` se decía en texto llano, del mismo color que las etiquetas de al lado.
**No es un dato sobre la máquina: es una elección que no se pudo cumplir** —exactamente lo mismo que
dicen los tres avisos de sonido—, así que toma la misma superficie, el mismo borde y el mismo glifo. /
It is not a fact about the machine, it is a choice that could not be honoured.

Va en `Grid ColumnDefinitions="Auto,*"` y no en un `StackPanel` horizontal. **Es la novena vez que esa
forma se mide en este árbol**: un stack ofrece anchura infinita, así que un texto con `TextWrapping`
al lado de un glifo no envuelve nunca y se sale por el lado. / Ninth measurement of that shape here.

## El volcado que alguien lee para decidir / The dump somebody reads before deciding

`DiagnosticsPreviewView` tenía `TextWrapping="NoWrap"`. Es **el único texto de la aplicación que existe
para leerse antes de una decisión** —qué saldría de este equipo si se pulsa exportar— y un texto así no
puede esconder su mitad derecha detrás de un scroll lateral. El techo de 320 px se queda, así que un
volcado largo baja dentro de su caja en vez de empujar la página. / The one piece of text that exists
to be read before a decision does not get to hide its right-hand half.

**Los 13 px de la §4 se rechazan con la regla del propio árbol.** La escala es 28/20/14/12 y no tiene
13; un escalar declarado para **un solo consumidor** es el defecto que este repositorio ya nombró dos
veces, y `FontSizeMono` se consideró y se dejó sin declarar por ese mismo motivo. El bloque toma
`FontSizeBody`, que es un token y no un número escrito en el marcado. **Octava discrepancia §4↔árbol.**
/ §4's 13 px is refused with the tree's own rule about scalars nobody spends.

## Y la puerta que era más estricta que el árbol sin que nadie lo decidiera / And the gate that was stricter than the tree without anybody deciding

`LifecycleSettingsTests` exigía que **ningún** `Text`/`Content` de esa vista fuera literal. El `⚠` es
literal en todas las demás vistas que lo llevan —los tres avisos de audio, los dos del estado de vídeo,
el distintivo de «no disponible»— y **ninguna tiene una puerta que lo prohíba**: esta vista era más
estricta que el árbol por accidente, no por decisión. / The glyph is literal in every other view that
carries it and none of them has a gate against it.

**Lo que la puerta protege son palabras sin traducir, y ahora eso es lo que dice**: un literal pasa sólo
si **no contiene ninguna letra**. Una palabra no puede colarse como símbolo —una sola letra en cualquier
posición la suspende— y se afirma además que **queda algún literal**, porque «ninguna palabra» es
también lo que informaría una pantalla en la que no se pinta nada. / What the gate protects is
untranslated words, and that is what it now says.

## El verde / The green

```
UiTests             671/671
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
ArchitectureTests   30/30
DocumentationTests  87/87
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```

**Con esto el tramo 5 cierra**, y `PrivacySettingsView` cerró sin tocarla: sus dos gramáticas ya
conviven, un hijo invisible no deja hueco, el contorno punteado ya llega, y el estado vacío que la §4
pide a su lista de hosts **no lo puede ver nadie** porque `NetworkPurposeRegistry.Declared` es estática
y declara cuatro. Los tres números están en `NEXT-SESSION`. / The tranche closes, and the privacy page
closed without being touched — with its three measurements written down rather than its silence.
