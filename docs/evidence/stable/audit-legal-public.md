# Revisión legal del repositorio público / Legal review of the public repository

Evidencia de la revisión legal completa del 2026-08-10 sobre el corte público, la primera desde que
el repositorio dejó de ser privado. Se corrigió, no solo se reportó. / Evidence for the full legal
review of 2026-08-10 over the public cut, the first since the repository stopped being private.
Findings were corrected, not merely reported.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Base: `e270112` («AP Reelume - first public cut»).

## Lo que se midió antes de tocar nada / What was measured before touching anything

| Comprobación / Check | Antes / Before | Después / After |
|---|---|---|
| Fuentes con cabecera de licencia / Sources with a licence header | 0 de 624 / 0 of 624 | 624 de 624 |
| Componentes en los avisos de terceros / Components in the third-party notices | 8 | 30 + motor .NET / + .NET runtime |
| Componentes en el SBOM real / Components in the real SBOM | 36 | 36 |
| Retención máxima de contenido TMDB / Maximum TMDB content retention | sin límite / unbounded | 180 días / days |
| Puertas que verifican lo legal / Gates verifying the legal surface | 0 | 8 |

El «antes» de las cabeceras se archivó como RED ejecutando `dotnet format --verify-no-changes
--severity warn` con `file_header_template` recién declarado: la puerta salió con código 2 y un
`error IDE0073: Falta un encabezado obligatorio en un archivo de código fuente` por cada uno de los
556 archivos `.cs` del árbol. Los 51 `.axaml` y los 17 `.ps1` no los cubre esa regla y se contaron
aparte, leyendo cada archivo. / The header "before" was archived as RED by running the format gate
with `file_header_template` freshly declared: it exited 2 with one missing-header error per `.cs`
file. The `.axaml` and `.ps1` files are not covered by that rule and were counted separately.

## Cabeceras SPDX / SPDX headers

`.editorconfig` declara `file_header_template` y sube `IDE0073` a `warning`, de modo que la puerta de
formato que ya se ejecutaba en cada verificación es la que exige la cabecera; no hizo falta una puerta
nueva. `dotnet format style --diagnostics IDE0073` aplicó las 556 cabeceras de C#, y un paso aparte
las 51 de `.axaml` y las 17 de `.ps1`, que esa regla no cubre. / The formatting gate that already ran
is what demands the header; no new gate was needed.

El comentario XML se validó compilando `ApSolutions.LocalMedia.Presentation` con `-warnaserror` antes
de aplicarlo a los 51 archivos restantes: el compilador de XAML de Avalonia lo acepta delante del
elemento raíz. / The XML comment was validated by compiling the Presentation project before applying
it to the remaining 51 files.

## Avisos de terceros contra el artefacto real / Third-party notices against the real artifact

El contraste se hizo contra tres fuentes que tenían que coincidir y no coincidían: el SBOM CycloneDX
del paquete sellado (36 componentes), el cierre de `packages.lock.json` del proyecto que se empaqueta,
y los DLL que de verdad viajan en `artifacts/package/layout`. Faltaban en los avisos, entre otros,
`Avalonia.Angle.Windows.Natives` (BSD-3-Clause, de The ANGLE Project Authors), `SkiaSharp`,
`HarfBuzzSharp`, `BouncyCastle.Cryptography`, `MicroCom.Runtime`, `Tmds.DBus.Protocol` y las tres
piezas de `SQLitePCLRaw` bajo Apache-2.0. / The comparison used three sources that had to agree and
did not: the sealed package's CycloneDX SBOM, the packaged project's lock-file closure, and the DLLs
that actually travel in the layout.

`ThirdPartyNoticeTests` cierra el hueco de forma permanente: lee el cierre del proyecto empaquetado,
descarta los recursos nativos de otros sistemas —que se restauran y luego se retiran del artefacto— y
exige nombre y versión resuelta en los dos idiomas. 5 pruebas. / closes the gap permanently.

Dos cosas que los avisos no decían y ahora dicen: el artefacto es autocontenido y transporta el motor
de .NET completo bajo MIT, y el paquete de VideoLAN trae unos trescientos complementos con licencias
propias, algunas GPL-2.0-or-later. Lo segundo queda nombrado como pregunta del dictamen profesional,
no resuelto aquí. / Two things the notices did not say and now do.

## Términos de la API de TMDB / TMDB API terms

Dos desviaciones reales, ambas corregidas:

1. **La frase de atribución era un resumen, no la frase.** Los términos fijan el texto; el programa
   decía «usa la API de TMDB… no está avalado ni certificado», sin «y las API de TMDB» y sin «ni
   aprobado de ningún otro modo». Corregido en `Strings.es.axaml`, `Strings.en.axaml`, `NOTICE` y los
   dos README, con una prueba que compara el recurso carácter a carácter. / The attribution was a
   summary rather than the sentence.
2. **La caché no tenía techo de antigüedad.** Los términos prohíben conservar más de seis meses lo
   obtenido de TMDB. La caducidad blanda era de un día, pero tres caminos del proveedor devolvían la
   copia guardada sin mirar su edad: sin token configurado, con la red caída, y con una respuesta sin
   cuerpo. Una entrada de dos años se habría servido. Ahora `TmdbOptions.RetentionLimit` (180 días) se
   aplica antes de cualquier decisión y la entrada vencida **se borra**, para lo que
   `IMetadataCache` gana `RemoveAsync`. / The cache had no age ceiling.

RED archivado con dos pruebas que fallaban antes del arreglo:
`Content_kept_longer_than_the_TMDB_retention_limit_is_neither_served_nor_kept` y
`Expired_retention_is_enforced_even_when_no_token_is_configured`. / RED archived with two tests that
failed before the fix.

Pendiente y nombrado: el logotipo de TMDB, que sus términos piden además del texto. Incorporar la
marca de un tercero es decisión del propietario. / Pending and named: the TMDB logo.

## Una puerta que mentía en local / A gate that lied locally

`PinnedDependencyTests` escaneaba `*.csproj` desde la raíz sin filtrar, así que en la máquina que
tiene el runner self-hosted instalado dentro del árbol encontraba los proyectos de ejemplo que
GitHub Actions desempaqueta bajo `.runner/` y fallaba con versiones ancladas en línea que no son del
proyecto. En CI pasaba, porque allí ese directorio no existe: una puerta roja en local y verde en la
tubería es la peor forma de estar equivocada. `RepositoryLayout.ProjectFiles()` excluye lo que
empieza por punto, que es exactamente lo que git ignora. / A gate red locally and green in the
pipeline is the worst way for a gate to be wrong.

## Estado legal como documento / Legal status as a document

`docs/legal/LEGAL.es.md` y `.en.md` reúnen lo resuelto y lo abierto, entran en la lista de documentos
públicos bilingües de `BilingualHeadingTests` y se enlazan desde los dos README. Nombran seis puntos
que quedan del propietario, entre ellos la notificación de exportación que el reglamento
estadounidense pide para código con criptografía publicado en abierto —el paquete lleva BouncyCastle
para verificar la firma minisign— y el dictamen profesional de `REL-004`. / Name six points that
remain the owner's.

## Puertas / Gates

| Puerta / Gate | Resultado / Result |
|---|---|
| `dotnet format --verify-no-changes --severity warn` | 0 |
| `dotnet build -c Release -warnaserror` | 0 advertencias, 0 errores / 0 warnings, 0 errors |
| `eng/verify-docs.ps1` | 100 Markdown, 28 localizados / localized |
| DocumentationTests | 65/65 |
| IntegrationTests | 376/377 (1 omitida por biblioteca personal ausente / 1 skipped) |
| Domain.Tests | 356/356 |
| Application.Tests | 195/195 |
| UiTests | 378/378 |

## Lo que esta revisión no es / What this review is not

No es un dictamen jurídico. Lo hizo quien programa, leyendo los términos aplicables y el código, y su
valor es haber medido en vez de suponer. Los seis puntos de `docs/legal/LEGAL.es.md` siguen abiertos y
`REL-004` sigue sin verificar. / It is not a legal opinion.
