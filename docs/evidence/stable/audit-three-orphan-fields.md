# Tres campos que se escribían en cada lectura y no leía nadie / Three fields written on every read and read by nothing

El defecto de la casa —registrado y nunca alimentado— en los datos en vez de en el contenedor. Los
tres los nombró la auditoría de cobertura de `Domain` del 2026-08-30 y los dejó de pie porque tocarlos
alcanza a `Infrastructure`; aquí se miden y se van. / The house defect — registered and never fed —
in the data rather than in the container. The `Domain` coverage audit of 2026-08-30 named all three
and left them standing because touching them reaches `Infrastructure`; here they are measured and
they go.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-30.

## La medición / The measurement

Un `grep` de `\.Language\b` sobre `src/` devuelve trece coincidencias y **ninguna** es de estos dos
campos: son `track.Language` de la selección de pista, `key.Language` de la clave de caché, y el
espacio de nombres `Presentation.Language`. `WatchedTitle.Id` no aparece leído en ningún sitio:
`RecommendationPolicy.Summarize` promedia géneros, reparto, nota y año, y no toca el identificador.
/ A `grep` for `\.Language\b` over `src/` returns thirteen hits and **none** is either field: they
are `track.Language` from track selection, `key.Language` from the cache key, and the
`Presentation.Language` namespace. `WatchedTitle.Id` is read nowhere: `Summarize` averages genres,
cast, rating and year and never touches the identifier.

## Por qué se van en vez de alimentarse / Why they go rather than get fed

**`Language` no puede decir lo que su nombre promete.** `TmdbMetadataProvider` lo rellena con
`requestedLanguage` —el idioma **pedido** en esa vuelta del bucle de reserva—, no con el idioma en el
que TMDB contestó. TMDB sirve el título con lo que tenga cuando no hay traducción, así que un lector
futuro habría recibido lo contrario de lo que el campo promete. Un campo que no puede ser cierto es
peor que un campo ausente, porque el ausente no engaña a nadie. / **`Language` cannot say what its
name promises.** `TmdbMetadataProvider` fills it with `requestedLanguage` — the language **asked
for** on that turn of the fallback loop — not the one TMDB answered in. TMDB serves a title in
whatever it has when there is no translation, so a future reader would have been told the opposite of
what the field promises. A field that cannot be true is worse than an absent one, because the absent
one deceives nobody.

**`WatchedTitle.Id` ya tiene quien haga su trabajo.** La señal de «esto ya se ha visto» llega al
marcador por el otro lado, como `RecommendationCandidate.IsWatched`, que es lo que la fórmula pesa
con `FreshnessWeight`. Y no hay duplicados que deduplicar: la proyección es `FROM titles t`, y
`titles.id` es único. / **`WatchedTitle.Id` already has something doing its job.** The "this has been
seen" signal reaches the score from the other side, as `RecommendationCandidate.IsWatched`, which the
formula weighs with `FreshnessWeight`. And there are no duplicates to remove: the projection is
`FROM titles t`, and `titles.id` is unique.

## Dos puertas que pasaban por no mirar nada / Two gates that passed by looking at nothing

Con los campos se van dos pruebas, y las dos merecen quedar escritas porque su forma se repite:

- `Both_answers_say_which_language_they_came_back_in` afirmaba `Assert.Equal("en-US", result.Language)`
  sobre un record que la propia prueba acababa de construir con `"en-US"`. Afirmaba que un valor
  **existía**, no que fuera cierto. Y su comentario llevaba escrita la asimetría que la invalidaba —
  «un lookup que cayó al fallback se guarda bajo el idioma que nadie recibió»— sin sacar la
  conclusión. Se reescribe para afirmar la referencia, que sí viaja.
- `A_watched_title_carries_the_identifier_of_what_was_watched` decía en su propio resumen que «nada
  en esta capa lee el identificador». Se va con el campo.

/ Two tests go with the fields, and both are worth writing down because their shape repeats.
`Both_answers_say_which_language_they_came_back_in` asserted `"en-US"` on a record the test had just
built with `"en-US"` — that a value **existed**, not that it was true — and its own comment carried
the asymmetry that invalidated it without drawing the conclusion; it is rewritten to assert the
reference, which does travel. `A_watched_title_carries_the_identifier_of_what_was_watched` said in
its own summary that nothing in that layer reads the identifier.

## El alcance real / The actual reach

Diez sitios de construcción, todos cazados por el compilador y ninguno por búsqueda de texto: tres en
`src/` y siete en cuatro suites. Un parámetro quedó muerto de camino —`ParseSearchResults` recibía el
idioma sólo para escribirlo en el record— y se retira con él, porque un parámetro que nadie lee es la
misma forma un nivel más abajo. / Ten construction sites, every one caught by the compiler and none
by text search: three in `src/` and seven across four suites. One parameter died on the way —
`ParseSearchResults` took the language only to write it into the record — and goes with it, because a
parameter nothing reads is the same shape one level down.

## Cómo se verificó / How it was verified

`dotnet build -c Release -warnaserror` sin una advertencia, `dotnet format --verify-no-changes
--severity warn` limpio, y las suites que leen los tres tipos: `Domain.Tests`, `Application.Tests`,
`UiTests`, `IntegrationTests`, `MediaTests` y `AccessibilityTests`. / `dotnet build -warnaserror`
without a warning, `dotnet format` clean, and the suites that read all three types.
