# El tráiler que ya está en el disco / The trailer already on the disk

Evidencia de **LIB-014**: la ficha de una película ofrece su tráiler cuando el archivo ya existe
junto a ella. / Evidence for **LIB-014**: a film's card offers its trailer when the file already
exists next to it.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-14.

## Por qué sólo el local / Why only the local one

El tráiler de TMDB es una **clave de YouTube**, no un archivo. Alcanzarlo desde dentro de la
aplicación significa llegar al vídeo por una vía que sus términos no amparan —sólo su reproductor o
su incrustación oficial—, y la incrustación pediría un WebView con hosts que
`NetworkPurposeRegistry` no declara, más publicidad y cookies en una aplicación que promete no tener
telemetría. Así que dentro se reproduce **sólo un archivo que ya tienes**; la clave remota es otra
entrada del plan y su sitio es el navegador. / The remote trailer is a YouTube key, not a file, and
reaching it from inside would take a route their terms do not allow.

## La medición que decidió la forma / The measurement that decided the shape

No hizo falta inventar un camino de reproducción: ya existe uno para archivos que no están en la
biblioteca. `OpenLooseFile`, el que se usa al abrir un vídeo desde el Explorador, ya hace las tres
cosas que un tráiler necesita: / A playback route for files outside the library already existed, and
it already does the three things a trailer needs:

```
OpenLooseFile.ExecuteAsync
  ├─ MediaFileExtensions.IsApproved(...)   rechaza lo que no es contenedor aprobado
  ├─ File.Exists(...)                      rechaza lo que no está
  └─ new MediaFileId(Guid.NewGuid())       identificador de sesión: nunca una fila del catálogo
```

Un tráiler **es** un archivo suelto que resulta estar al lado de la película, así que la forma es una
política pura que **nombra** el candidato y el caso de uso que ya existe para abrirlo. Sin segundo
camino de reproducción, sin fila nueva, sin instancia nativa nueva. / A trailer *is* a loose file
that happens to sit next to the film, so the shape is a pure policy that names the candidate and the
use case that already exists to open it.

## El rojo / The red

```
TrailerTests.The_film_card_offers_the_trailer_only_when_there_is_one          [FAIL]
TrailerTests.The_action_is_named_in_both_languages(Strings.es.axaml)          [FAIL]
TrailerTests.The_action_is_named_in_both_languages(Strings.en.axaml)          [FAIL]
TrailerTests.The_composition_asks_the_policy_and_opens_the_trailer…           [FAIL]
```

Las trece pruebas de la política llegaron con ella —un archivo que no compila no es un rojo
archivado—, y por eso el rojo que se archiva es el de la interfaz, que sí se podía medir contra el
código de ese momento. / The policy's thirteen tests arrived with it, so the archived red is the
interface's.

## La corrección / The fix

`TrailerDiscoveryPolicy`, pura y en el dominio, con las dos convenciones que Plex, Jellyfin y Kodi
escriben igual: un hermano `<película>-trailer.<ext>` o una carpeta `Trailers` bajo la de la
película. Trece pruebas la fijan, y **cuatro de ellas son la guarda que importa**: /
`TrailerDiscoveryPolicy`, pure and in the domain, with the two conventions all three libraries write.
Four of its thirteen tests are the guard that matters:

- **Nada fuera de la carpeta de la película** se nombra jamás —ni la carpeta de otra, ni
  `C:\Windows\Temp`, ni un subdirectorio más hondo que `Trailers`—. Una lista de candidatos vale lo
  que valga quien la construyó, y el decodificador es el mayor riesgo asumido de este proyecto.
- **La extensión pasa por la lista aprobada**, así que un `.ps1` o un `.exe` con el nombre correcto no
  es un tráiler.
- **La película nunca es su propio tráiler.**
- **Dos candidatos dan el mismo resultado en cualquier orden**, porque un listado de directorio no
  promete ninguno.

Las rutas se comparan resueltas y sin distinguir mayúsculas, nunca como cadenas: una barra final o
otra capitalización son la misma carpeta, y tratarlas como distintas es exactamente cómo se cuela
algo por una comprobación de contención. / Paths are compared resolved and case-insensitively, never
as strings.

El listado del disco vive en la composición, no en la política; una carpeta que no se puede leer no
es un fallo que reportar, es una película sin tráiler que ofrecer. / The listing lives in the
composition; a folder that cannot be read is a film with no trailer to offer.

## Verde / Green

| Puerta / Gate | Resultado / Result |
|---|---|
| `TrailerDiscoveryPolicyTests` | 13 de 13 / of 13 |
| `TrailerTests` | 4 de 4 / of 4 |
| `ApSolutions.LocalMedia.Domain.Tests` | 369 de 369 / of 369 |
| `ApSolutions.LocalMedia.UiTests` | 422 de 422 / of 422 |
| `ApSolutions.LocalMedia.AccessibilityTests` | 79 de 79 / of 79 |
| `eng/verify.ps1` completo / full | verde / green |
