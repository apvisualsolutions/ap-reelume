# Volver a verlo todo dejó de ser borrar a mano lo que se escribió / Getting everything back stops being a manual delete

Cuarto trabajo del tramo 3 de la §4, y el **primer control nuevo de la fase 6**: el botón que vacía la
búsqueda. / §4's third tranche gains the search's clear button — phase 6's first new control.

Fecha / Date: 2026-08-20. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Qué faltaba / What was missing

Con una búsqueda escrita, la única forma de volver a ver la biblioteca entera era **seleccionar el
texto y borrarlo**, y después pulsar Aplicar. La §4 lo pide por su nombre, y es de las pocas cosas que
un teclado hace peor que un ratón. / Getting the whole library back took selecting the text, deleting
it, and pressing Apply.

## Las tres cosas que un control nuevo trae consigo / What a new control brings with it

Ninguna es opcional y las tres van en **este** cambio:

1. **Su cadena, en los dos idiomas**: `LibrarySearchClearAction` — «Borrar la búsqueda» / "Clear the
   search". El mismo recurso es el `Content` y el `AutomationProperties.Name`, como en el resto del
   árbol.
2. **Su escena de paseo**, dentro de la que ya recorre la biblioteca. Se pulsa **con una búsqueda
   puesta y la lista reducida a un título**, que es el único estado en el que hace algo, y la sonda es
   **el recuento volviendo a subir**: de 1 a 2. Después la escena vuelve a dejar la búsqueda como
   estaba, porque lo que sigue —abrir la ficha— cuenta con ella.
3. **Su notificación**. El comando lleva predicado sobre `Search`, así que el `setter` llama a
   `RaiseCanExecuteChanged`: un comando que no anuncia se pregunta una vez al construirse y **conserva
   esa primera respuesta para siempre**, que es el defecto por el que `ARQ-004` cambió veinticuatro
   clases. / A command that never announces keeps its first answer forever.

## Deshabilitado, no ausente / Disabled, not absent

Con la caja vacía el botón **existe y no se puede usar**. Es una decisión y va escrita en el marcado:
está en una fila de cuatro controles que se quedan quietos, y uno que apareciera y desapareciera
**movería los otros tres cada vez que alguien teclea una letra**. La gramática que este árbol ya usa
para lo contrario —ausente cuando ofrecerlo sería ofrecer algo que no puede ocurrir— vive en
`PrivacySettingsView`. / A button that came and went would move the three beside it on every keystroke.

## El verde / The green

```
El paseo / the walk: 135 declaraciones en 134 identidades; 134 pulsadas, 0 pendientes
UiTests             627/627
AccessibilityTests  135/135 en las dos pasadas / on both passes
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```

**El trinquete del paseo subió a 134 y volvió a 0 pendientes en el mismo cambio**, que es exactamente
lo que la regla pide de un control nuevo. / The ratchet moved to 134 and returned to zero pending in
the same change.

## Y lo que se revirtió, que también es una decisión / And what was reverted

El selector de temporada de `ShowDetailsView` —la otra pieza que le queda al tramo— se empezó y **se
deshizo**: el modelo ya tenía `SelectedSeason` y `HasSeasonChoice` cuando llegó una prioridad nueva, y
dejar propiedades que **ninguna vista pinta** es la sexta forma del defecto de la casa, escrita a
propósito. Queda pendiente entero, con su decisión ya tomada dentro de este documento: **con una sola
temporada el selector es ausente, no deshabilitado**, porque un control que sólo puede contestar lo que
ya dice es una pregunta que nadie hizo. / Leaving a view model with properties no view paints is the
house defect on purpose, so it was reverted whole.
