# El póster llega a las dos fichas / The poster reaches both cards

Evidencia del cierre de **ART-A01** y de la decisión 14 del propietario: el cabecero de la ficha
dibuja el póster de verdad, con el arte generado debajo. / Evidence for closing **ART-A01** and the
owner's decision 14: both cards' headers draw the real poster, with the generated art beneath them.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-28.

## Lo que había, medido antes de tocar nada / What was there, measured first

| Pregunta / Question | Medición / Measurement |
| --- | --- |
| ¿Qué guarda `PosterPath`? / What does `PosterPath` hold? | El `poster_path` de TMDB tal cual: `/wXsQ….jpg`, una ruta **remota** |
| ¿Alguna vista lo lee? / Does any surface read it? | No. `MovieDetailsViewModel` sólo derivaba `Initials` del título |
| ¿Hay imágenes en la aplicación? / Any images at all? | `<Image>` en `src/**/*.axaml`: **0** |
| ¿Existe la caché? / Does the cache exist? | Sí, `ArtworkCache`, completa y probada, con techo de 10 MB (SEC-005) |
| ¿Está registrada? / Is it registered? | **No**, y `CompositionDescriptorTests` **afirmaba su ausencia** |
| ¿Está declarado el host? / Is the host declared? | Sí: `image.tmdb.org`, propósito `ArtworkCache`, con consentimiento |

Es decir: **todo estaba construido menos los dos extremos**. La decisión del 2026-08-09 (ART-A01)
retiró el registro en vez de dejarlo mudo, y la del 2026-08-21 dejó las portadas fuera de 0.2.0. La
del propietario del 2026-08-28 revierte la primera para el cabecero de la ficha. / Everything was
built except the two ends. ART-A01 retired the registration rather than leave it silent; the owner's
decision of 2026-08-28 reverses it for the film card's header.

## Por dónde entra la red, y por dónde no / Where the network is, and where it is not

**Una ficha no abre nunca una conexión.** El puerto tiene dos miembros y son asimétricos a propósito:
`Find` sólo mira el disco, y todo lo que sale a la red está detrás de `FetchAsync`, que se llama
**una vez**, desde `ApplyIdentification`, que es el momento en que alguien ya ha consentido hablar
con el proveedor. / **A card never opens a connection.** `Find` only ever looks at the disk;
everything that reaches the network is behind `FetchAsync`, called once, from the one moment somebody
has consented to talk to the provider.

Lo mide `ArtworkCacheTests`: `Find` antes de traer nada responde `null` con **0 peticiones**, después
de traer responde el archivo con **1**, y una dirección distinta del mismo título responde `null`
todavía con **1**. / Measured: `Find` answers null at 0 requests, the file at 1, and a different
address for the same title still at 1.

## Una ruta remota es una entrada no confiable / A remote path is untrusted input

`PosterAddressPolicy` comprueba **antes** de componer, por la misma razón que `TrailerLinkPolicy`:
componer primero deja una dirección malformada en existencia y a partir de ahí todo lector tiene que
acordarse de desconfiar. Refusa, entre otras: una segunda barra —que sacaría la ruta de
`/t/p/w780/`—, un `..`, una dirección entera de otro, un esquema, una consulta, un fragmento,
codificación por porcentaje, y un dígito que Unicode conoce y ASCII no. / It checks before it
composes, for the reason `TrailerLinkPolicy` does.

Y hay una aserción de propiedad además de la lista: **lo que se construye siempre es `https`, sobre el
host declarado, bajo el segmento del tamaño, sin consulta ni fragmento**. / And a property assertion
beside the list: whatever is built is always https, on the declared host, under the size segment.

## Un tamaño y no dos, con su cesión escrita / One size and not two, with the cession written down

La ficha dibuja el póster dos veces —elevado a 158×237 y sangrado por el cabecero tras un
degradado—, y TMDB sirve un tamaño por dirección: dos tamaños serían dos descargas y dos entradas de
caché por título. `w780` son 780×1170, más del doble de los píxeles del póster elevado, y suficiente
a lo ancho de un cabecero de 1.180 px cuyo lado cercano queda cubierto al 95 %. / One address per
size at TMDB, so two sizes would be two downloads per title. `w780` is 780×1170.

**Una descarga, y un descodificado.** El convertidor cachea por ruta, así que las dos superficies
comparten el mismo `Bitmap` — asertado con `Assert.Same` sobre los dos `Image` efectivamente
visibles. / One download, and one decode: asserted with `Assert.Same` over the two effectively
visible images.

## Lo que se dibuja cuando no hay póster / What is drawn when there is none

El arte generado **se queda debajo, siempre**, y las iniciales sólo mientras no hay imagen: dos letras
existen para decir qué título es esto antes de que ningún color haya enseñado nada, y sobre el póster
serían una segunda respuesta a una pregunta que la imagen ya contestó. / The generated art stays
underneath always, and the initials only while there is no picture.

Y una ruta que no nombra una imagen —borrada a mano, a medio escribir, o que nunca fue una imagen— es
«sin póster» y nunca una excepción. Son archivos que esta aplicación escribió en su propia caché, así
que las tres cosas pasan. / A path that names no picture is "no poster", never an exception.

## La caché de imágenes tiene tope, y ésa es la mitad que se olvida / The picture cache is bounded

Un póster `w780` descodificado son 780×1170 a cuatro bytes por píxel: **unos 3,5 MB en memoria**, pese
el archivo lo que pese. Un diccionario sin tope indexado por ruta guardaría uno por cada título que
alguien abriese, así que quien pasee por cien películas llevaría encima **un tercio de gigabyte** de
imágenes que ya nadie dibuja. El tope son **8** entradas —las dos superficies de una ficha comparten
una— y la más antigua sale. / A decoded `w780` poster is about 3.5 MB in memory whatever the file
weighs, so an unbounded dictionary would have somebody browsing a hundred films carrying a third of a
gigabyte of pictures nothing draws. The bound is 8 entries.

**Se suelta y no se libera, y eso es deliberado.** Las dos fichas siguen montadas mientras el shell
enseña una de ellas, así que un `Image` puede estar sujetando la imagen que se desaloja: un `Bitmap`
liberado bajo un control que lo dibuja es un fallo duro, mientras que una referencia soltada sólo
cuesta un descodificado que alguien pagará otra vez. / **Dropped and not disposed, deliberately**: a
disposed bitmap under a control that draws it is a crash; a dropped reference is only a decode
somebody may pay for again.

## Las dos fichas, no una / Both cards, not one

El prototipo levanta el póster de la serie a **136×204** contra el mismo muro sangrado que usa la
película, así que es **una cadena y dos vistas** y no dos cadenas: el mismo puerto, el mismo
convertidor y el mismo archivo. Lo afirma `Assert.Same` sobre los dos `Image` de cada ficha — el
convertidor cachea por ruta para el proceso, no por vista. / The prototype raises the show's poster at
136×204 against the same bled wall, so it is one chain and two views: the same port, the same
converter, the same file.

## Verde / Green

```
Domain.Tests        559 superadas, 0 con error
Application.Tests   259 superadas, 0 con error
UiTests           1.004 superadas, 0 con error
IntegrationTests    485 superadas, 0 con error, 1 omitida
AccessibilityTests  146 superadas, 0 con error
ArchitectureTests    30 superadas, 0 con error
```

Cobertura de lo nuevo y lo tocado, con las cuatro suites que lo ejercitan: / Coverage of what is new
and what was touched, over the four suites that exercise it:

| Archivo / File | Línea / Line | Rama / Branch |
| --- | --- | --- |
| `PosterAddressPolicy.cs` | 100 | 100 |
| `CacheTitleArtwork.cs` | 100 | 100 |
| `CachedPosterConverter.cs` | 100 | 100 |
| `ArtworkCache.cs` | 100 | 100 |
| `ApplyIdentification.cs` | 100 | 100 |
| `MovieDetailsViewModel.cs` | 100 | sube sobre su suelo / rises above its floor |
| `ShowDetailsViewModel.cs` | 100 | 85, **igual que su suelo** / same as its floor |

`IArtworkStore.cs` no aparece en ningún informe: es un contrato sin líneas instrumentables, que es
como la puerta lo espera. **`ArtworkCache.cs` y `MovieDetailsViewModel.cs` suben por encima de sus
suelos declarados** (95/64 y 100/83), y eso es un rojo de la puerta de cobertura cuya corrección es el
número del artefacto `coverage-debt` del run que lo mida. `ShowDetailsViewModel.cs` mide exactamente
su suelo, 100/85, y por eso no se toca. / Two files rise above their declared
floors, which is a coverage-gate red whose fix is CI's own artefact.

## Una puerta que se creía un comentario / A gate that believed a comment

`ExplanationCodeTests` recorre `src/` buscando literales `"Identification.…"` para exigirles palabras
en los dos idiomas. Una referencia cruzada de documentación —`cref="Identification.ApplyIdentification"`—
es una cadena entrecomillada que empieza igual, y la puerta pidió una entrada de diccionario **para un
nombre de clase**. Es la forma que ARQ-013 corrigió, vista desde el otro lado: una puerta que lee
fuente como texto y se cree un comentario. El escáner excluye ahora los `cref=` y sigue cazando lo
que existe para cazar. / A documentation cross-reference is a quoted string starting with the same
word, so the gate asked for a dictionary entry for a class name. The scanner now excludes `cref=`.

## Lo que sigue sin hacerse / What is still not done

Las portadas de la cuadrícula de Biblioteca y de las tres filas de Inicio **siguen fuera**, y la
decisión del 2026-08-21 sigue en pie por su razón medida: arrastran la cuadrícula, que cuesta 7× el
tiempo y 455× los controles vivos por perder la virtualización. Esto son **dos fichas**, no tres
superficies de rejilla. / The library grid's covers and Home's three rows are still out, for the
measured reason of 2026-08-21. This is two cards, not three grid surfaces.
