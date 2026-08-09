# SmartScreen y esta descarga

AP Reelume se distribuye **sin firma de código**. No hay certificado detrás de este artefacto, ni de
prueba ni comprado. Este documento explica qué verá al instalarlo, por qué, y qué puede comprobar
usted en lugar de confiar en una firma que no existe.

La versión inglesa está en [SMARTSCREEN.en.md](SMARTSCREEN.en.md).

## Qué verá

Al abrir el MSIX o el ejecutable del ZIP por primera vez, Windows mostrará un aviso de
**Microsoft Defender SmartScreen**: «Windows protegió su PC» o «Editor desconocido». El botón para
continuar suele estar detrás de «Más información».

Ese aviso es correcto. No es un falso positivo ni un error que haya que sortear: Windows está
diciendo exactamente la verdad, que es que **no sabe quién publicó este archivo**.

## Por qué no está firmado

Un certificado de firma de código lo emite una autoridad comercial y cuesta una cuota anual. Este
proyecto es gratuito y no tiene ingresos, así que no hay ninguno. Además, un certificado nuevo tampoco
elimina el aviso de inmediato: SmartScreen construye reputación con el tiempo y con el número de
descargas, de modo que una firma recién emitida sigue avisando durante un tiempo.

Lo que este proyecto **no** hará es dar a entender que sí está firmado. El paquete declara su propia
condición: el informe del artefacto lleva `"signed": false`, y una prueba de la suite falla si alguna
vez dijera lo contrario.

## Qué puede comprobar en lugar de la firma

Una firma responde a «¿quién publicó esto?». Sin ella quedan dos preguntas que sí puede responder por
su cuenta, y que juntas cubren casi lo mismo.

### 1. Que el archivo es el que se publicó

Cada publicación incluye `SHA256SUMS.txt`. Compare el hash del archivo que descargó:

```powershell
Get-FileHash .\APSolutions.LocalMedia_0.1.0_x64.msix -Algorithm SHA256
```

El resultado debe coincidir, ignorando mayúsculas, con la línea correspondiente de `SHA256SUMS.txt`.
Si no coincide, el archivo no es el publicado y no debe abrirlo.

Además, `SHA256SUMS.txt` va firmado: cada publicación incluye `SHA256SUMS.txt.minisig`, una firma
[minisign](https://jedisct1.github.io/minisign/) hecha con la clave del proyecto, cuya mitad pública
está en el repositorio (`eng/release-signing.pub`) y embebida en el binario. Con minisign instalado:

```powershell
minisign -Vm SHA256SUMS.txt -p release-signing.pub
```

El actualizador integrado hace esta comprobación solo en cada actualización; a mano solo hace falta
si descarga los archivos usted mismo.

### 2. Que lo publicado se corresponde con el código

Las compilaciones son reproducibles. Dos compilaciones del mismo commit, desde dos copias limpias del
repositorio en dos carpetas distintas, producen el mismo contenido archivo por archivo. Puede
comprobarlo usted mismo:

```powershell
pwsh ./eng/package-x64.ps1
pwsh ./eng/verify-package.ps1 -Mode Verify
```

`artifacts/package/reproducibility.json` recoge la comparación. El contenedor MSIX en sí **no** es
idéntico entre compilaciones —un paquete registra el instante en que se selló—, pero todo lo que hay
dentro sí lo es.

## Qué contiene el paquete

- El SBOM viaja dentro del artefacto, en `sbom/`, en formatos CycloneDX y SPDX.
- La licencia GPL-3.0-or-later y los avisos de terceros viajan en `LICENSE`, `NOTICE` y `licenses/`.
- El paquete **no** declara ninguna capacidad más allá de `runFullTrust`, que es la que necesita
  cualquier aplicación de escritorio. No pide red, ni ubicación, ni acceso a bibliotecas del sistema.
- El paquete **no** lleva ningún token de acceso. La identificación remota sólo funciona si usted pone
  uno a mano en `AP_LOCALMEDIA_TMDB_TOKEN`; sin ese acto deliberado, la aplicación no abre ninguna
  conexión de metadatos. El comprobador de actualizaciones, desactivado de fábrica, es la otra
  conexión posible; la tabla completa está en la declaración de privacidad. La verificación de una
  actualización (SHA-256 y tamaño) prueba que la descarga no se alteró en tránsito; la autenticidad
  descansa en la cuenta de GitHub que publica las versiones, porque el artefacto no va firmado.

## Instalación

**Requisito de sistema.** La aplicación se dirige a **Windows 11** (22H2, compilación 22621, o
posterior), y el paquete lo declara como mínimo. Es una decisión deliberada, no un descuido:
Windows 10 no es un objetivo de esta aplicación.

**MSIX.** Windows exige que un paquete esté firmado por un certificado en el que confíe. Sin firma, el
MSIX de esta publicación sirve para inspección y archivo, no para instalación por doble clic. Use la
ruta del ZIP.

**ZIP.** Descomprímalo donde quiera y ejecute `ApSolutions.LocalMedia.Windows.exe`. No requiere
instalación, ni permisos de administrador, ni escribe en el registro. Los datos van a
`%LOCALAPPDATA%\APSolutions\LocalMedia`, salvo que nombre otra carpeta con `AP_LOCALMEDIA_DATA_ROOT`.

Para desinstalar, borre la carpeta que descomprimió. Sus datos siguen donde estaban; bórrelos aparte
si eso es lo que quiere.

## Qué no hacemos

- No pedimos que desactive SmartScreen, Defender ni ninguna otra protección.
- No publicamos instrucciones para saltarse el aviso más allá de lo que Windows ya ofrece.
- No afirmamos, en ninguna parte de la interfaz ni de esta documentación, que la aplicación esté
  firmada o verificada por Microsoft.
