# WP-4 — Endurecimiento del actualizador / Updater hardening

Evidencia del paquete WP-4 de la auditoría profunda del 2026-08-08. / Evidence for the WP-4 package
from the deep audit of 2026-08-08.

## SEC-004 — La dirección de la que llegan los bytes es una promesa / The address the bytes come from is a promise

**El defecto / The defect.** El descargador seguía las redirecciones a mano y exigía HTTPS en cada
salto, pero aceptaba **cualquier host**: los assets de GitHub redirigen a un dominio de
almacenamiento que ni el código ni la declaración de privacidad nombraban. / The downloader
followed redirects by hand and required HTTPS on every hop, but accepted **any host** — and
GitHub's assets redirect to a storage domain neither the code nor the privacy statement named.

**RED (archivado / archived).**

- El candado registro↔declaración, extendido a los hosts adicionales, en rojo en ambos idiomas:
  `PRIVACY.es.md`/`PRIVACY.en.md` «does not mention: *.githubusercontent.com,
  objects.githubusercontent.com». / the registry↔statement lock, extended to additional hosts, red
  in both languages.
- `A_redirect_that_leaves_the_allowed_hosts_is_refused` — con servidor TLS real: el mismo servidor
  bajo otro nombre, la descarga se completaba en vez de rechazarse. / with a real TLS server: the
  same server under another name, and the download completed instead of being refused.

**La corrección / The fix.**

- `NetworkPurpose` gana `AdditionalHosts` y la regla de cobertura (`Allows`/`Matches`): un comodín
  cubre subdominios y **nunca** el dominio pelado, y la declaración de privacidad tiene que nombrar
  cada host adicional en ambos idiomas — el candado documental ahora los lee también. / gains
  `AdditionalHosts` and the coverage rule: one leading wildcard, subdomains only, never the bare
  domain; the documentation lock now reads the additional hosts too.
- `VerifiedUpdateDownloader` exige que **cada salto** quede dentro de la allowlist; sin lista
  explícita, la allowlist es la que el registro declara (el valor por defecto seguro). Un salto
  fuera es `UpdateRejection.UndeclaredHost`, la pantalla lo dice con nombre
  (`UpdateRefusedUndeclaredHost`, ES/EN), y nada se descarga. / requires **every hop** to stay
  inside the allowlist; unset, the allowlist is what the registry declares. A hop outside is
  `UndeclaredHost`, named on screen in both languages, and nothing is downloaded.
- `ArtworkCache` rehúsa cualquier dirección fuera de su propósito declarado (`image.tmdb.org`)
  **antes** de pedir un solo byte. / refuses any address outside its declared purpose **before**
  asking for a single byte.

## SEC-005 — Techos de respuesta / Response ceilings

**El defecto / The defect.** Ninguna respuesta tenía techo: los metadatos se leían enteros fueran lo
que fueran, el paquete se escribía completo aunque el servidor enviara de más (el hash lo habría
dicho al final, tras bufferizarlo todo), y un póster podía ocupar lo que el servidor quisiera. / No
response had a ceiling.

**RED (archivado / archived).** `Metadata_larger_than_a_release_description_reads_as_unreachable`
(1,2 MB de JSON válido se analizaban y devolvían una versión) y
`A_package_larger_than_the_release_declared_is_cut_off_at_the_excess` (300 KB contra ~4 KB
declarados: la escritura llegaba al final y la aserción `ActualSize < oversized.Length` falló). /
1.2 MB of valid JSON parsed and answered; a 300 KB body against ~4 KB declared was written whole.

**La corrección / The fix.**

- **Metadatos: 1 MB.** El proveedor lee en flujo con el techo en vigor y corta al superarlo: una
  fuente que responde megabytes a «¿cuál es la última versión?» no está respondiendo la pregunta,
  se parse lo que se parse. / metadata reads streamed under a 1 MB ceiling.
- **Paquete: los bytes declarados.** La escritura se corta en cuanto `received > SizeInBytes`, el
  parcial envenenado se borra y la excepción de verificación dice hasta dónde llegó. / the package
  write is cut off at the excess, the poisoned partial is deleted, and the verification exception
  reports how far it got.
- **Arte: 10 MB.** El póster se lee en flujo bajo el techo; uno que lo supera se rehúsa a medio
  camino y el arte anterior del título sobrevive. / artwork reads streamed under a 10 MB ceiling;
  an oversized answer is refused mid-stream and the previous artwork survives.

**GREEN.**

- Nuevas / new: `A_redirect_that_leaves_the_allowed_hosts_is_refused`,
  `A_package_larger_than_the_release_declared_is_cut_off_at_the_excess`,
  `Metadata_larger_than_a_release_description_reads_as_unreachable`,
  `Artwork_from_an_undeclared_host_is_refused_without_a_request`,
  `Oversized_artwork_is_refused_and_the_previous_artwork_survives`, `NetworkPurposeTests` 9/9 (el
  comodín cubre exactamente lo que escribió).
- Suites completas / full suites: IntegrationTests 361/361 (+1 skip declarado), PackagingTests
  106/106, Application.Tests 194/194, UiTests 349/349, DocumentationTests 58/58 (el candado lee los
  hosts adicionales), `dotnet format` limpio, `-warnaserror` 0/0.
- Los tests del actualizador nombran ahora su allowlist de loopback **explícitamente**; el valor
  por defecto del componente es el registro — el camino seguro es el que no exige recordar nada. /
  the updater tests now name their loopback allowlist explicitly; the component's default is the
  registry.

## SEC-003 — El hash esperado ya no viaja sin firmar / The expected hash no longer travels unsigned

**El defecto / The defect.** El actualizador leía el SHA-256 esperado del cuerpo de la publicación —
el mismo JSON sin firmar que nombra el paquete al que ese hash avala. Quien pudiera alterar la
respuesta podía alterar a la vez el paquete y su huella. / The updater read the expected SHA-256
from the release body — the same unsigned JSON that names the package the hash vouches for. Whoever
could alter the answer could alter the package and its digest together.

**La decisión (WP-5, 2026-08-09) / The decision.** Firma detached minisign: cada publicación firma
`SHA256SUMS.txt` con una clave cuyo público viaja embebido en el binario; la privada vive fuera del
repositorio (secreto de Actions `RELEASE_SIGNING_SECRET_KEY` + copia custodiada del propietario).
Capa distinta de Authenticode, que sigue siendo la decisión económica del propietario. / Detached
minisign signing; a different layer from Authenticode, which remains the owner's economic decision.

**RED (archivado / archived).**

> `A_newer_release_is_offered_downloaded_verified_and_handed_over_only_once_confirmed` — FAIL:
> "The release's checksums are not signed by the release key, so the hash vouches for nothing."

Las notas que ayer producían una oferta hoy se rechazan: la política nueva contra el cuerpo viejo. /
The notes that yesterday produced an offer are refused today: the new policy against the old body.

**La corrección / The fix.**

- `Minisign` (Infrastructure): el formato leído y escrito directamente — Ed25519 + Blake2b-512
  (BouncyCastle.Cryptography, gestionado y anclado), verificación de la firma del archivo **y** de
  la global que sella el comentario de confianza; firma y generación de claves en el mismo archivo,
  consumidas por `eng/tools/ReleaseSigning` y las pruebas, para que las dos mitades del formato no
  puedan divergir. / the format read and written directly; verify checks the file signature **and**
  the global one; signing and keygen live in the same file, consumed by the release tool and tests.
- `UpdateRelease.Sha256Signed` es un **veredicto del verificador**, no una afirmación de la fuente;
  `UpdatePolicy` rechaza por `UnsignedChecksums`, con su texto ES/EN en pantalla. / a verdict from
  the verifier, not a claim from the source; the policy refuses by name, bilingual on screen.
- El proveedor extrae el bloque de sumas y la firma de las notas, verifica sobre los bytes canónicos
  (LF, LF final — los mismos que escribe `package-x64`), y **el hash sale solo del bloque
  verificado**: una línea sin firmar no puede eclipsar a la firmada. / the provider verifies over
  the canonical bytes and takes the hash only from the verified block.
- Tubería: `package-x64` escribe `SHA256SUMS.txt` canónico y firma cuando la clave es alcanzable
  (`RELEASE_SIGNING_SECRET_KEY` en Actions, `RELEASE_SIGNING_KEY_FILE` en local), verificando la
  firma recién hecha contra la clave embebida; `build-release-notes` embebe el `.minisig`;
  `prepare-release` bloquea una release sin firma verificable; `release.yml` la comprueba y la sube.
  / the pipeline signs when the key is reachable, embeds the signature in the notes, blocks an
  unsigned release, and ships the `.minisig`.
- Par de claves generado el 2026-08-09; pública embebida (`UpdateSigningKey`) y en
  `eng/release-signing.pub`; privada como secreto de Actions y copia local del propietario con
  instrucciones de custodia, fuera de todo repositorio. / key pair generated; public embedded and in
  the repo; private as an Actions secret plus the owner's guarded copy, outside every repository.
- Modelo de confianza documentado en PRIVACY ES/EN (qué prueba la firma y qué no), SMARTSCREEN ES/EN
  (verificación manual con minisign) y RELEASING ES/EN (el formato de notas y el paso de firma). /
  trust model documented in PRIVACY, SMARTSCREEN, and RELEASING, both languages.

**GREEN + puertas / GREEN + gates.**

- Nuevos / new: `A_release_whose_checksums_travel_unsigned_is_refused_by_that_name`,
  `A_signature_from_another_key_or_over_other_bytes_proves_nothing`,
  `An_unsigned_line_cannot_shadow_the_signed_checksums`,
  `A_provider_without_the_embedded_key_treats_every_release_as_unsigned`,
  `A_release_whose_checksums_are_not_signed_is_refused` (Domain).
- Paseo E2E real / real end-to-end walk: `package-x64` con la clave local — firma creada, verificada
  contra la clave embebida, notas con el bloque, `ReleaseNotesTests` ofreciendo la versión con la
  firma **real**. / with the local key — signature made, verified against the embedded key, notes
  carrying the block, `ReleaseNotesTests` offering the version under the **real** signature.
- Suites / suites: Domain.Tests 357/357, IntegrationTests 367/367 (+1 skip declarado), UiTests
  349/349, ArchitectureTests 16/16, DocumentationTests 58/58, ReleaseNotesTests 4/4;
  `dotnet format` limpio; `-warnaserror` 0/0; verify-docs en verde.

**Estado / Status.** SEC-004, SEC-005 y SEC-003 corregidos: WP-4 completo. La capa Authenticode
(SmartScreen, identidad ante Windows) sigue siendo la mitad económica de WP-5, del propietario. /
SEC-004, SEC-005, and SEC-003 fixed: WP-4 complete. The Authenticode layer stays the owner's
economic half of WP-5.
