# Cortar una publicación

Cómo se produce y se verifica un artefacto de AP Reelume para Windows 11 x64. La versión inglesa está
en [RELEASING.en.md](RELEASING.en.md).

## Lo que necesita

| Herramienta | Para qué | Comprobación |
|---|---|---|
| SDK de .NET 10.0.302 | compilar y publicar | `dotnet --version` |
| SDK de Windows 10 u 11 | sellar el MSIX | `MakeAppx.exe` bajo `C:\Program Files (x86)\Windows Kits\10\bin` |
| PowerShell 7 | ejecutar los scripts | `pwsh --version` |
| Git | reproducibilidad y SBOM | `git --version` |

No hace falta Visual Studio. El paquete no se construye con un `.wapproj`; el porqué está en
[ADR-0004](../adr/0004-seal-the-package-with-makeappx.md).

## De dónde sale la versión

De un único sitio: `<Version>` en `Directory.Build.props`. Todo lo demás se deriva.

| Origen | Valor | Regla |
|---|---|---|
| `Directory.Build.props` | `0.1.0` | SemVer, elegido a mano |
| `Package.appxmanifest` | `0.1.0.0` | SemVer más la revisión que MSIX reserva |
| Nombre del MSIX | `APSolutions.LocalMedia_0.1.0_x64.msix` | identidad, versión, arquitectura |
| Nombre del ZIP | `ApReelume-0.1.0-win-x64.zip` | nombre público, versión, runtime |
| MSIX ARM64 | `APSolutions.LocalMedia_0.1.0_arm64.msix` | la misma regla, otra arquitectura |
| ZIP ARM64 | `ApReelume-0.1.0-win-arm64.zip` | la misma regla, otro runtime |

El manifiesto lleva su versión escrita, no sustituida, para que siga siendo XML válido que una prueba
pueda leer. `FileAssociationPackageTests` compara las dos, y `eng/package-x64.ps1` se detiene si
difieren. **Subir de versión son dos ediciones, y una prueba avisa si sólo hace una.**

## Los pasos

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
pwsh ./eng/package-x64.ps1
pwsh ./eng/verify-package.ps1 -Mode Verify
pwsh ./eng/package-arm64.ps1
pwsh ./eng/verify.ps1 -Configuration Release -Runtime win-x64
```

`eng/package-arm64.ps1` va después del x64 a propósito: compara su payload con ese layout para
comprobar que las dos arquitecturas llevan la misma aplicación, y sin él la comparación no puede
hacerse. `eng/verify.ps1` construye los dos por su cuenta, así que en una verificación normal basta
con ese último comando.

**El artefacto ARM64 no está certificado en hardware.** Se construye, se sella y se verifica todo lo
que puede verificarse sin una máquina ARM64, y `arm64-matrix.json` enumera las seis cosas que no.
Publicarlo es una decisión que hay que tomar habiendo leído ese archivo, no un efecto de que exista.

### 1. `eng/package-x64.ps1`

Publica autocontenido para `win-x64`, monta el layout, lo sella y escribe todo lo que acompaña al
artefacto. Dos cosas que no hace un `dotnet publish` normal:

- **Quita las cargas de LibVLC para otras arquitecturas.** Un publish `win-x64` de esta aplicación
  trae también `win-x86` y `win-arm64`: 512 MB que se quedan en 234 MB al retirarlas.
- **Mete la licencia dentro.** `LICENSE`, `NOTICE` y los avisos de terceros en ambos idiomas viajan en
  el payload, porque es una condición de distribuir el binario y no un adorno de la página de
  descarga.

Deja en `artifacts/package/`: el MSIX, el ZIP, `SHA256SUMS.txt`, `sbom/`, `contents.json` y
`packaged/AppxManifest.xml`.

### 2. `eng/verify-package.ps1`

Recorre el ciclo de vida y compara dos compilaciones. Escribe `lifecycle.json` y
`reproducibility.json`.

El ciclo de vida corre sobre el **paquete desempaquetado**, con una carpeta de datos distinta por
ciclo mediante `AP_LOCALMEDIA_DATA_ROOT`. Redirigir `LOCALAPPDATA` no serviría: .NET resuelve esa
carpeta con `SHGetFolderPath` y no lee esa variable.

Las cuatro fases que pertenecen a Windows —instalar, actualizar, reparar y desinstalar un paquete
mediante el propio sistema— quedan **declaradas como bloqueadas** cuando no hay máquina virtual
limpia, elevación ni firma. `MsixLifecycleTests` exige que sigan bloqueadas mientras el entorno sea
ese, y exige que pasen en cuanto deje de serlo. Un bloqueo no se puede leer como un aprobado.

La comparación de reproducibilidad crea dos copias limpias del árbol —incluidos los cambios en el
índice, mediante `git stash create`— en dos carpetas distintas y compara el payload archivo por
archivo. **Añada al índice lo que vaya a publicar antes de ejecutarla**: un fichero sin rastrear no
existe para una copia limpia, y el script se detiene si encuentra alguno.

### 3. `eng/verify.ps1`

La verificación completa. Construye el paquete, ejecuta el ciclo de vida y luego la suite entera, el
formato, la documentación y la auditoría de dependencias.

### 4. `eng/generate-verification-manifest.ps1`

Regenera `docs/evidence/mvp/verification-manifest.json` desde la matriz y desde el artefacto recién
construido. **Ejecútelo al cortar la versión, no en cada compilación**: el manifiesto se versiona, y
un MSIX registra el instante en que se selló, así que sus hashes cambian con cada sellado aunque el
contenido sea idéntico. El commit y los hashes que registra son los del paquete que se publica, no
los de la copia de trabajo.

Se niega a escribir un manifiesto donde un compromiso sin resolver no declare su bloqueo, o donde uno
resuelto lo arrastre.

## Publicar en GitHub

`.github/workflows/release.yml` se dispara con una etiqueta `v*`. Hace lo mismo que haría usted a
mano y sube el MSIX, el ZIP, los hashes con su firma y el SBOM como artefactos de la ejecución.
**No** publica una release por su cuenta ni sube nada a ninguna Store. Usa un único secreto,
`RELEASE_SIGNING_SECRET_KEY`: la clave minisign que firma `SHA256SUMS.txt` para que el actualizador
verifique las huellas contra la clave pública embebida en el binario (SEC-003). La firma Authenticode
sigue sin existir, y eso no cambia aquí.

Para firmar en local en vez de en el workflow: apunte `RELEASE_SIGNING_KEY_FILE` a su copia de la
clave privada (que vive fuera de todo repositorio) antes de ejecutar `eng/package-x64.ps1`. Sin
clave, el paquete se construye igual pero `prepare-release` bloquea la publicación: una release sin
firma es una que ninguna instalación aceptará.

Antes de publicar:

1. Que `SHA256SUMS.txt` **y** `SHA256SUMS.txt.minisig` acompañen a los archivos, en el mismo sitio.
2. Que las notas enlacen [SMARTSCREEN.es.md](SMARTSCREEN.es.md) y su versión inglesa.
3. Que las notas digan que el artefacto no está firmado con Authenticode. No lo dé por sabido.

### Las notas de la publicación las lee el actualizador

El actualizador independiente (`REL-003`) no descarga nada que las notas no describan. Lee la
publicación marcada como `latest` y saca de ella la versión de la etiqueta, el artefacto por su
arquitectura y, del cuerpo de las notas, tres cosas más. Sin cualquiera de ellas la versión **no se
ofrece**, y la aplicación dice por cuál.

**No las escriba a mano.** `eng/package-x64.ps1` las genera en
`artifacts/package/release-notes.md` a partir de los dos changelogs y de los hashes que acaba de
calcular, y lo único que hay que hacer al publicar es pegar ese archivo. `ReleaseNotesTests` toma lo
generado, se lo entrega al proveedor real dentro de la respuesta que GitHub devolvería y le pregunta
a la política real si ofrecería esa versión: no comprueba un formato, comprueba que alguien con el
artefacto instalado recibiría la actualización.

El formato que produce, y que el actualizador espera, es este:

````markdown
## Español

Qué cambia, en una o dos frases.

## English

What changed, in a sentence or two.

## SHA256SUMS

```
<hash>  APSolutions.LocalMedia_<versión>_x64.msix
<hash>  APSolutions.LocalMedia_<versión>_arm64.msix
```

## Firma / Signature

```
untrusted comment: signature from AP Reelume release key
<firma en base64>
trusted comment: timestamp:<unix>	file:SHA256SUMS.txt	prehashed
<firma global en base64>
```
````

Cuatro reglas que conviene tener presentes:

- **Los dos idiomas o ninguno.** Confirmar una actualización es leer qué cambia, y un resumen que la
  persona no puede leer convierte la confirmación en un trámite.
- **La línea del hash se busca por el nombre del archivo**, así que tiene que ser exactamente el del
  artefacto. Son las mismas líneas de `SHA256SUMS.txt`, copiadas tal cual.
- **La firma es el contenido de `SHA256SUMS.txt.minisig`, tal cual.** El actualizador la verifica
  contra la clave embebida antes de creer ningún hash; sin ella, o con líneas alteradas, la versión
  no se ofrece (SEC-003).
- **Una pre-release o un borrador no se ofrecen nunca**, aunque estén marcados como `latest`.

Marcar la casilla de comprobación automática es cosa de quien usa la aplicación; está desactivada de
fábrica y, mientras lo esté, la aplicación no abre ninguna conexión por su cuenta.

## Publicar en winget

`eng/package-x64.ps1` deja también el manifiesto del gestor de paquetes de Windows en
`artifacts/package/winget/`, generado desde el propio archivo: el hash es el que se publica, el
ejecutable que declara está comprobado dentro del ZIP, y las descripciones salen de los dos README.
`WingetManifestTests` comprueba las tres cosas contra el artefacto real.

winget es la vía de distribución que **no cuesta nada y no necesita certificado**: acepta un ZIP que
contiene una aplicación portable y verifica el SHA-256 que ya se publica. Enviarlo es abrir un pull
request contra [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) con esa carpeta.

**Antes hay dos condiciones que hoy no se cumplen:**

1. **La descarga tiene que ser pública.** El manifiesto apunta a la release de GitHub, y en un
   repositorio privado esa dirección no responde a nadie. Compruébelo con
   `pwsh ./eng/build-winget-manifest.ps1 -Verify`, que pregunta a la dirección en vez de suponerla.
2. **Tiene que existir esa release.** El manifiesto nombra `v<versión>`; sin la etiqueta publicada no
   hay nada que descargar.

Lo mismo bloquea al actualizador independiente: consulta la API de GitHub, y para un repositorio
privado responde 404. Como la ausencia de publicación es una respuesta resuelta, la aplicación diría
«ya tienes la versión más reciente» a todo el mundo. **Publicar el repositorio y cortar una release
son requisitos de que el actualizador funcione, no adornos.**

Sólo se declara x64. El ARM64 se construye y se verifica en cada ejecución pero no se publica hasta
que `PRD-003` esté resuelto, y una entrada en un gestor de paquetes es publicación.

## Antes de nada: `eng/prepare-release.ps1`

```bash
pwsh ./eng/prepare-release.ps1
```

Responde una sola pregunta —si este árbol podría publicarse— y produce todo lo que una publicación
necesita. Comprueba las condiciones que nadie recuerda: que el árbol esté limpio y empujado, que la
versión se haya subido **en los dos sitios**, que el repositorio responda a desconocidos, que ningún
compromiso MVP verificado se haya quedado sin evidencia, que el paquete no diga estar firmado, que el
ciclo de vida no tenga fases fallidas y que las dos compilaciones limpias sigan siendo idénticas.

**No hace nada irreversible.** No crea etiquetas, no publica, no empuja y no cambia ningún ajuste del
repositorio. Cuando algo bloquea, lo dice y se detiene; cuando no bloquea nada, imprime los cinco
pasos que quedan y que ejecuta una persona. Publicar sigue siendo un acto deliberado de quien ha
leído ese informe.

Con `-SkipBuild` reutiliza el artefacto que ya haya en `artifacts/package` en lugar de construirlo
otra vez.

## Qué revisar antes de etiquetar

- `pwsh ./eng/verify.ps1 -Configuration Release -Runtime win-x64` termina limpio, dos veces.
- `docs/FEATURES.md` no tiene ningún compromiso MVP sin evidencia enlazada.
- `contents.json` dice `"signed": false` y su nota menciona SmartScreen.
- `lifecycle.json` no tiene ninguna fase en `Failed`, y las bloqueadas llevan su razón.
- `reproducibility.json` no tiene diferencias ni exclusiones.
- `artifacts/package-arm64/arm64-matrix.json` se lee antes de decidir si ARM64 se publica, y su
  `parityWithX64` no muestra ningún archivo de la aplicación en una sola arquitectura.
