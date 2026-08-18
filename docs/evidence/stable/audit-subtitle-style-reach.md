# El estilo de subtítulos no llega a la imagen / The subtitle style never reaches the picture

`A11Y-002` queda bloqueado **por medición y no por observación**: tres hechos del árbol, cada uno
comprobable, que explican por qué el estilo se guarda y no se ve. / `A11Y-002` is blocked by
measurement rather than by observation.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## La prueba que no se puede escribir, y por qué / The test that cannot honestly be written

Lo obvio sería decodificar un fotograma con el estilo aplicado y otro sin él y comparar los mapas de
bits. **Esa prueba no se puede escribir con honestidad hoy**: no hay forma alguna de aplicar el
estilo, así que los dos fotogramas serían idénticos **por construcción** y la comparación no
distinguiría «no está implementado» de «está implementado y no funciona». Lo que sí se puede medir es
**la cadena**, y está rota en tres sitios que son hechos planos sobre el árbol. / The obvious test
would compare two frames; it cannot tell "not implemented" from "implemented and ineffective",
because the two frames would be identical by construction.

## Los tres hechos / The three facts

**1. La instancia nativa se construye sin ninguna opción de dibujado de subtítulos.** LibVLC toma ese
dibujado de opciones **de instancia**, y `LibVlcFactory` crea una instancia **por juego de opciones y
la conserva durante toda la vida del proceso**. Los dos juegos declarados son fijos:

```
--no-metadata-network-access  --no-sub-autodetect-file  --no-video-title-show   [+ --aout=dummy]
```

Ninguna es `--freetype-*` ni `--sub-text-scale`. Lo que no está ahí **no puede llegar a un dibujado
después**, porque no hay un «después»: la instancia ya existe.

**2. Ningún archivo de `src/` nombra jamás una de esas opciones.** Recorrido el árbol entero contra
las doce que LibVLC ofrece: cero. No hay otra ruta — ni una opción de medio, ni una llamada al
reproductor, ni una cadena compuesta en ejecución.

**3. El contrato del motor no tiene por dónde recibir un estilo.** `IMediaPlayerEngine` sabe de
subtítulos —selecciona una pista, acepta un archivo externo— y **ninguna de esas dos cosas es decir
cómo se dibuja el texto**. Ningún miembro con `Style` en el nombre.

## Lo que sí funciona, para que quede claro qué falta / What does work

El estilo **no es el problema**: `SubtitleStyle` valida, el repositorio lo guarda y lo lee de vuelta,
`PreferenceResolutionPolicy` lo resuelve archivo sobre serie sobre global, seis controles lo cambian
y sobrevive a cerrar la ventana. **Todo menos llegar.** / Everything except arriving.

## La prueba muerde, y se comprobó / The test bites, and that was checked

Introducida temporalmente `--freetype-fontsize=48` en las opciones de la instancia, **dos de las tres
afirmaciones se ponen rojas** con el mensaje que corresponde:

```
The shell instance is built with 1 subtitle drawing option(s): --freetype-fontsize=48.
1 source file(s) name a subtitle drawing option: src\...\LibVlcFactory.cs.
```

Los dos mensajes dicen lo mismo: **si esto deja de ser cero es una buena noticia y hay que volver a
medir `A11Y-002`, no relajar la prueba**. Revertido después. / Both messages say the same thing: if
this stops being zero, re-measure the matrix entry rather than editing the test.

## Lo que NO se hace aquí / What is not done here

**`A11Y-002` no cambia de estado en `FEATURES.md` todavía.** Cambiar un estado de la matriz obliga a
regenerar el manifiesto de verificación desde un paquete recién construido, y eso es parte de cortar
una versión, no de una sesión de trabajo. Va en el corte de 0.2.0, con el bloqueador nombrado en
`eng/generate-verification-manifest.ps1` y en `release-readiness.md`. / The matrix entry changes at
the version cut, where the manifest is regenerated from a freshly built package.

## Las puertas / The gates

```
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1   # 0 avisos / 0 warnings
dotnet test …MediaTests --filter SubtitleStyleReachTests               # 3 / 3
```

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
dotnet test tests/ApSolutions.LocalMedia.MediaTests/ApSolutions.LocalMedia.MediaTests.csproj -c Release -m:1 --no-build --settings eng/test.runsettings --filter "FullyQualifiedName~SubtitleStyleReachTests"
```
