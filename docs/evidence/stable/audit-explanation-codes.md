# «¿Por qué crees que este archivo es esta película?» — «Identification.Signal.Title» / "Why do you think this file is this film?" — "Identification.Signal.Title"

Primer trabajo del tramo 6 de la §4, **y el peor defecto medido en toda la fase 6**: no es un control
muerto ni un hueco, es **una ruta de espacio de nombres puesta delante de quien revisa**. / §4's sixth
tranche, and the worst defect measured in this whole phase.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo medido / What was measured

`CandidateCardView` pintaba `ExplanationCodes` con un `Text="{Binding}"` pelado, y esos códigos son
rutas con puntos que escribe el dominio: / bare bindings over dotted paths the domain writes:

```
Identification.Duplicate.NeedsReview        Identification.Signal.Season
Identification.Duplicate.SameEpisode        Identification.Signal.Title
Identification.Duplicate.SameMovie          Identification.Signal.Year
Identification.Error.KindConflict           Identification.Warning.AmbiguousName
Identification.Signal.Duration              Identification.Warning.EpisodeContradiction
Identification.Signal.Episode
```

**Once códigos en `src/`, en dos archivos del dominio, y cero cadenas para ellos** en `Strings.es.axaml`
ni en `.en.axaml`. Lo que se veía, medido en la vista montada: / Eleven codes, zero strings; measured on
the mounted view:

```
text 'movie:603'
text '91 %'
text 'Sugerida'
text 'Por qué'
text 'Identification.Signal.Title'
text 'Identification.Warning.AmbiguousName'
help 'Identification.Signal.Title, Identification.Warning.AmbiguousName'
```

**La explicación es el motivo entero de esa pantalla.** Es la respuesta a «¿por qué crees que este
archivo es esta película?», y contestaba con un espacio de nombres. / The explanation is the whole point
of that screen, and it answered with a namespace.

**Y el oído lo tenía peor que la vista**: `ExplanationSummary` concatenaba los mismos códigos y viajaba
en `AutomationProperties.HelpText`, así que un lector de pantalla **recitaba las rutas**. / The ear had
it worse than the eye.

## Lo que ya estaba resuelto una vista más allá / What was already solved one view away

`ResourceKeyConverter` existe justo para esto, y su propio resumen lo dice: «los códigos de motivo
viajan como claves para que las palabras sigan al idioma en vez de decidirse cuando se calculó la
sugerencia». `RecommendationsRailView` pinta sus motivos con él. **La misma clase de dato, traducida en
una vista y en crudo en la otra.** / The same kind of data, translated in one view and raw in the other.

El converter aprende a resolver **una lista** además de una clave, porque un `HelpText` es una sola
cadena y una explicación son varios códigos. **Se une ahí y no en el modelo**: resolver un recurso
necesita la aplicación y su variante de tema, y eso no entra en un modelo de vista. / It joins in the
converter and not in the model, because resolving a resource needs the application and its variant.

## La clave ES el código / The key IS the code

Las once cadenas se declaran con el código por `x:Key` —`Identification.Signal.Title`—, no con un
nombre derivado. **Una transformación sería un segundo sitio donde se escribe el mismo nombre**, y los
dos divergirían la primera vez que alguien renombrara un código. / A mapping would be a second place
where the same name is written.

## Y la puerta, que es la mitad que importa / And the gate, which is the half that matters

La prueba **recorre `src/` buscando el literal del código**, en la misma forma que este repositorio ya
usa para los hosts de red no declarados. No lleva una lista propia: **el duodécimo código no puede nacer
en crudo**, porque el día que se escriba en el dominio esto falla hasta que sus palabras existan en los
dos idiomas. Y se afirma primero **que el barrido encontró al menos once**, porque un barrido que no
encuentra nada recorre el bucle sin medir un solo código — que es como esta forma de prueba se vuelve
ciega. / The scan asserts its own catch first, because a scan that finds nothing passes without
measuring anything.

## Y lo que quedó sin lectores se fue con ello / And what was left with no readers went with it

`ExplanationSummary` existía sólo para ese `HelpText`. Con el `HelpText` resolviéndose por el converter,
la propiedad se quedó **sin un solo lector en todo el árbol** — que es exactamente el defecto que este
cambio venía a arreglar, sólo que del otro lado. Se borra en el mismo commit. **Al arreglar un dato que
nadie pintaba, mira si lo que lo pintaba mal se queda sin trabajo.** / Fixing data nobody painted leaves
the thing that painted it badly with no job; look for it in the same change.

## El verde / The green

```
UiTests             673/673
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
ArchitectureTests   30/30
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```

`ResourceKeyConverter.cs` queda en **100 % de líneas y 78,6 % de ramas** sobre un suelo de `100 75`, así
que **subirá el suelo y CI pedirá la segunda vuelta**: es el precio conocido de que las mediciones las
tenga CI. Su rama sin cubrir es otra vez una guarda que nada puede tomar —`Application.Current is not
null` en algo que construye el AXAML— y **se deja a propósito**: quitarla aquí tocaría un converter que
comparten ocho vistas, y eso es una limpieza aparte y no parte de esta pieza. / Its uncovered branch is
another guard nothing can take, left on purpose because removing it touches eight views' converter.
