# La clave que abre el navegador / The key that opens the browser

Evidencia de **LIB-015**: el tráiler que TMDB conoce se ofrece desde las dos fichas y se abre en el
navegador, nunca dentro. / Evidence for **LIB-015**: the trailer TMDB knows about is offered from
both cards and opens in the browser, never inside.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-14.

## Tres mediciones, y las tres cambiaron el trabajo / Three measurements, all three changed the work

### 1. El lanzador endurecido que iba a reutilizarse no existe / The hardened launcher to reuse does not exist

El plan daba por hecho que «el lanzador externo endurecido —único sitio con `Process.Start`— sigue
exigiendo `https`». Contados los `Process.Start` del árbol, son **tres**, y **ninguno abre una
dirección**: uno entrega un `.msix` ya verificado al instalador de Windows, otro abre la carpeta de
las copias de seguridad, y el tercero —`ShellExternalPlaybackLauncher`— exige que la extensión esté
en la lista aprobada. No había nada que exigiera `https` porque no había nada que abriera una URL. /
The plan assumed a hardened launcher that already required `https`. There are three `Process.Start`
calls in the tree and none of them opens an address, so the launcher had to be written.

### 2. Una caché que habría servido la respuesta a una pregunta que nadie hizo / A cache that would have answered a question nobody asked

`append_to_response=videos` cambia la **dirección** de la petición de detalles, pero la clave de la
caché es `(proveedor, título, idioma, versión del proveedor)` y **no incluye la dirección**. Una
biblioteca con caché previa habría servido el payload antiguo —sin bloque `videos`— como si fuera la
respuesta nueva, y peor: una revalidación condicional con el `ETag` de esa representación puede
devolver `304` y **renovar el plazo**, de modo que la ficha nunca habría llegado a tener botón. /
The cache is keyed by provider, title, language and provider version — never by the address.

Subir `ProviderVersion` es la reacción evidente y **es la equivocada**: `provider_version` forma
parte de la clave primaria de `metadata_cache`, y el techo duro de 180 días de TMDB **sólo se aplica
al leer esa misma clave** (`TmdbMetadataProvider.GetPayloadAsync`). Las filas de la versión anterior
dejarían de leerse, y por tanto **nada podría borrarlas nunca**: se cumpliría la corrección a costa
de incumplir los términos. Lo que hace la migración es vaciar lo que pertenece a TMDB, que es la
única opción que no deja ni una respuesta equivocada ni una fila que sobreviva a su plazo. / Raising
the provider version would hide those rows instead of removing them, and the retention limit is only
enforced on the read of that same key, so the migration empties what belongs to TMDB instead.

### 3. Sin validar, once caracteres no son once caracteres / Unvalidated, eleven characters are not eleven characters

La primera versión de `TrailerLinkPolicy` compone la dirección sin comprobar la clave, que es lo que
un lector espera. Medida contra las mismas 26 pruebas, acepta **quince** formas: / The first version
composed without checking the key, and accepted fifteen shapes:

```
https://www.youtube.com/watch?v=https://evil.example/watch?v=dQw4w9WgXcQ
https://www.youtube.com/watch?v=javascript:alert(1)
https://www.youtube.com/watch?v=//evil.example
https://www.youtube.com/watch?v=dQw4w9WgXcQ\nfile:///C:/Windows
https://www.youtube.com/watch?v=dQw4w9Wg&cQ        (y ?, #, %, :, /, \, espacio, punto)
https://www.youtube.com/watch?v=dQw4w9WgXc         (diez)
https://www.youtube.com/watch?v=dQw4w9WgXcQQ       (doce)
```

### 4. La puerta de red tenía razón, y el plan no del todo / The network gate was right, and the plan was not quite

El plan decía que `NetworkPurposeRegistry` **no cambia** porque quien conecta es el navegador. Es
cierto sobre las conexiones y **no basta**: la regla de la casa recorre `src/` y falla ante cualquier
host que el registro no declare, así que la puerta se puso roja en cuanto la política nombró
`www.youtube.com`. / The plan said the registry does not change, which is true about connections and
not enough: the source-tree rule failed the moment the policy named the host.

```
TrailerLinkPolicy.cs names an undeclared host: www.youtube.com
```

Las dos salidas fáciles son malas. Declararlo como `NetworkPurpose` **afirmaría una conexión que no
existe** y ensancharía `IsDeclaredHost`, que es justo lo que el canario de red usa para decidir qué
está permitido abrir. Meterlo en la lista literal de la prueba —donde están `schemas.microsoft.com`
y `www.gnu.org`— lo enterraría entre detalles de marcado, y el documento de privacidad no diría
nunca que esta aplicación puede llevarte a YouTube. / Declaring it as a purpose would claim a
connection that is not made and would widen the check the canary trusts; hiding it in the test's
literal list would bury a real privacy decision among markup details.

Lo que se hace es una **segunda lista cerrada**, `NetworkPurposeRegistry.HandedOff`: destinos que la
aplicación entrega al sistema operativo y a los que nunca conecta. `IsDeclaredHost` sigue siendo
ciego a ella —una prueba lo afirma—, la puerta del árbol acepta declarado **o** entregado, y el
documento de privacidad gana su propia sección en los dos idiomas, con la misma exigencia de
correspondencia exacta que ya tenía la tabla de conexiones. / A second closed list: destinations
handed to the operating system and never connected to, blind to `IsDeclaredHost` by design.

### 5. Un número escrito en cinco sitios / One number written down in five places

La migración 17 puso en rojo **cuatro pruebas de tres archivos que este cambio no tocaba** —copia de
seguridad, rechazo de versión anterior, recuperación—, porque cada uno afirmaba `16L` por su cuenta.
Ahora los tres leen el conteo del manifiesto embebido; `SqliteBootstrapTests` conserva sus literales
a propósito, porque fijar el esquema exacto, por número y por nombre, es su trabajo. / Migration 17
turned four tests red in three files it had not touched, each holding its own literal.

De paso: `eng/verify-docs.ps1` **comprobaba un número y anunciaba otro**. El mensaje final llevaba el
`56` escrito a mano, así que subir el trinquete a 57 dejó la puerta diciendo «56 feature IDs» mientras
verificaba 57. Ahora el mensaje imprime lo que acaba de medir. / The docs gate checked one number and
announced another, because the sentence had the count written into it by hand.

### 6. La puerta de cobertura sólo ve lo confirmado / The coverage gate only sees what is committed

Con los archivos nuevos preparados en el índice, la puerta anunció «no source file is new against
origin/main» y salió verde. Confirmado el commit y reejecutada —que es el orden que este repositorio
ya tiene escrito—, midió lo que había: / With the new files merely staged, the gate announced there
was nothing new. Run again after the commit, it measured what was there:

```
src/…/Domain/Metadata/TrailerLinkPolicy.cs            100,00  100,00  PASS
src/…/Application/Metadata/IExternalLinkLauncher.cs   n/a     n/a     PASS (no instrumentable lines)
src/…/Windows/Metadata/ShellExternalLinkLauncher.cs    43,75   60,00  FAIL   (suelo 96/96)
```

La mitad que faltaba es la que habla con el shell, y era inalcanzable por construcción: conducirla
abría un navegador de verdad en la máquina que mide. La corrección es entregarle la llamada como
parámetro, con `Process.Start` por defecto, de modo que la ruta de aceptación se ejerce contra un
doble que **registra lo que recibe**. Compra dos cosas: la cobertura y —lo que importa más— poder
afirmar **qué llega al shell**: la dirección entera como única instrucción, `UseShellExecute` en
verdadero, y ni un argumento compuesto alrededor. Siete pruebas nuevas. / The missing half was
unreachable by construction; handing the call in covers it and buys the assertion that matters.

## El rojo / The red

```
SqliteBootstrapTests                        3 de 7 en rojo / 3 of 7 red
MigrationHistoryTests.Upgrading_empties…    [FAIL] tmdb: esperado 0, obtenido 1
TmdbContractTests.The_videos_ride_on…       [FAIL] falta append_to_response=videos
TrailerLinkPolicyTests                      15 de 26 en rojo / 15 of 26 red
TrailerTests                                5 de 9 en rojo / 5 of 9 red
NetworkPrivacyTests.No_source_file_names…   [FAIL] TrailerLinkPolicy.cs names an undeclared host
RotatingBackupTests / SchemaDowngradeTests / FailedMigrationTests   [FAIL] esperado 16, obtenido 17
```

Los cuatro últimos **no se buscaron**: aparecieron al correr las suites enteras, que es exactamente
para lo que están. / The last four were not looked for; they turned up on running the full suites.

El rojo del lanzador **no se ejecuta, a propósito**, y se dice en vez de fingirlo: medir lo que hace
sin sus guardas exige entregar al shell exactamente lo que las guardas existen para no entregar —un
`javascript:` o un `file:` abriría algo en la máquina que mide—. Lo que sí está medido es la capa de
encima, donde quince claves componían direcciones hasta que la política dejó de componerlas. / The
launcher's red is deliberately not run, and that is stated rather than faked.

## La corrección, en el orden que fija el plan / The fix, in the order the plan fixes

**Migración → proveedor → política → interfaz**, para que ningún commit deje el código leyendo una
columna que no existe.

1. **`0017_trailer_key.sql`**, con su entrada en `Manifest.json` y el SHA-256 **recalculado**
   (`Get-FileHash`, LF sin BOM, comprobado antes contra la migración 0016). Es la **primera
   migración de este repositorio que corre sobre datos que escribió una versión anterior**, y por eso
   trae la primera prueba que migra hasta la 16, escribe filas y luego aplica la 17 — con una fila de
   otro proveedor como control, que sobrevive.
2. **El proveedor** pide `append_to_response=videos` sobre la petición de detalles **que ya se hace**:
   ni una llamada más, ni un host nuevo. Elige en tres pasadas —oficial en el idioma pedido, luego
   cualquiera en ese idioma, luego cualquiera— y sólo `type=Trailer` con `site=YouTube`.
3. **`TrailerLinkPolicy`** valida `^[A-Za-z0-9_-]{11}$` **antes** de componer nada, deletreado con
   `char.IsAsciiLetterOrDigit` en vez de con un motor de expresiones regulares: `char.IsLetterOrDigit`
   habría aceptado dígitos de cualquier alfabeto que Unicode conozca.
4. **`IExternalLinkLauncher`** y su adaptador de Windows, que repiten la comprobación en el sitio que
   habla con el shell. Dos refusos, y el segundo no es el que se espera: `https://www.youtube.com@
   example.invalid/` es una dirección `https` válida cuyo host es `example.invalid`, y todo lo que
   está a la izquierda de la arroba está ahí para que lo lea una persona.
5. **Las dos fichas** ofrecen el botón sólo con clave bien formada. La de serie también, porque TMDB
   tiene vídeos de series; lo que no tiene una serie es un archivo único al lado del que colgar un
   tráiler local, que es por lo que `LIB-014` fue sólo de películas.

## Lo que no cambia, y conviene que siga sin cambiar / What does not change, and should not

`NetworkPurposeRegistry.Declared` **no gana ningún propósito ni ningún host**, y `IsDeclaredHost`
sigue diciendo que no a `www.youtube.com`. La aplicación no abre ninguna conexión a YouTube: entrega
una dirección al sistema operativo y quien conecta es el navegador de quien pulsa, con sus ajustes,
sus extensiones y su consentimiento. Lo que sí gana el registro es la lista de al lado —lo entregado,
que no es lo conectado—, porque un host que aparece en el código sin motivo escrito al lado es
precisamente lo que la regla de la casa existe para impedir. / The declared purposes gain nothing and
`IsDeclaredHost` still says no; what the registry gains is the list beside it.

`TrailerKey` **no es campo bloqueable**: `MetadataField` es la lista de lo que una persona edita y
protege, y no hay superficie que edite un identificador de vídeo. Una prueba dice exactamente eso —
bloquear todos los campos no retiene la clave— para que el día que exista un editor sea esa prueba la
que falle. / The trailer key is deliberately not lockable, and a test says so.

## Verde / Green

| Puerta / Gate | Resultado / Result |
|---|---|
| `TrailerLinkPolicyTests` | 26 de 26 / of 26 |
| `TrailerTests` | 9 de 9 / of 9 |
| `ExternalLinkLauncherTests` | 19 de 19 / of 19 |
| `MetadataMergePolicyTests` | 6 de 6 / of 6 |
| `ApSolutions.LocalMedia.Domain.Tests` | 403 de 403 / of 403 |
| `ApSolutions.LocalMedia.Application.Tests` | 204 de 204 / of 204 |
| `ApSolutions.LocalMedia.IntegrationTests` | 431 de 431 (1 omitida: el fixture del proceso hijo) / of 431 (1 skipped: the child-process fixture) |
| `ApSolutions.LocalMedia.UiTests` | 435 de 435 / of 435 |
| `ApSolutions.LocalMedia.PackagingTests` | 136 de 136 / of 136 |
| `ApSolutions.LocalMedia.DocumentationTests` | 84 de 84 / of 84 |
| `dotnet build -warnaserror` | 0 advertencias, 0 errores / 0 warnings, 0 errors |
| `dotnet format --verify-no-changes` | limpio / clean |
| `eng/verify-docs.ps1` | 130 documentos, 30 localizados, 57 IDs, 46 MVP / 130 docs, 30 localized, 57 IDs, 46 MVP |
| `eng/verify.ps1` completo / full | verde / green |
