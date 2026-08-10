<!--
Puedes escribir en español o en inglés. / Write in Spanish or English.
Borra las secciones que no apliquen; deja las casillas honestas. Una casilla marcada sin haber
hecho lo que dice cuesta más que una sin marcar. / Delete what does not apply. An honestly
unchecked box costs less than a dishonestly checked one.
-->

## Qué cambia y por qué / What changes and why

<!-- El efecto para quien usa el programa, no el diff. / The effect on the person using the program. -->

## Qué medí / What I measured

<!--
El rojo antes del arreglo y el verde después, con su salida. Si el cambio no admite una prueba,
di por qué. / The red before the fix and the green after, with output. If the change admits no
test, say why.
-->

## Puertas / Gates

- [ ] `dotnet format --verify-no-changes --severity warn`
- [ ] `dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1`
- [ ] Suites afectadas con / Affected suites with `-m:1 --settings eng/test.runsettings`
- [ ] `pwsh -File eng/verify-docs.ps1`
- [ ] Cobertura si añado archivos / Coverage if I added files (`eng/check-coverage.ps1`)

## Lo que la revisión mira / What review looks at

- [ ] Todo archivo fuente nuevo lleva su cabecera `SPDX-License-Identifier: GPL-3.0-or-later`. / New source files carry the SPDX header.
- [ ] Las cadenas visibles y los documentos públicos están en los dos idiomas. / User-facing strings and public documents exist in both languages.
- [ ] Las superficies nuevas se recorren con teclado y tienen nombre accesible. / New surfaces are keyboard-reachable and named.
- [ ] No añado ninguna conexión de red fuera de `NetworkPurposeRegistry`. / No network host outside the registry.
- [ ] No hay nada personal en el diff: ni rutas de mi máquina, ni nombres de mi biblioteca. / Nothing personal in the diff.
- [ ] Si añado o quito una dependencia, actualicé los avisos de terceros en los dos idiomas. / Third-party notices updated in both languages if dependencies changed.
- [ ] Changelog actualizado en `docs/CHANGELOG.es.md` y `docs/CHANGELOG.en.md`. / Changelog updated in both languages.

## Alcance / Scope

- [ ] Es un solo cambio; los refactores de paso van aparte. / One change; drive-by refactors go separately.
- [ ] Encaja en el alcance de [FEATURES.md](../blob/main/docs/FEATURES.md) y no contradice la hoja de ruta. / It fits the feature matrix and does not contradict the roadmap.

<!--
Al abrir este pull request aceptas publicar tu aportación bajo GPL-3.0-or-later. No hay CLA.
/ By opening this pull request you agree to publish your contribution under GPL-3.0-or-later.
-->
