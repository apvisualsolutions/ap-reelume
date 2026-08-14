# El fuente correspondiente viaja con la versión / The corresponding source travels with the release

Cierra por ingeniería las dos preguntas de licencia que `REL-004` dejaba a un dictamen: bajo qué
apartado del §6 de la LGPL-2.1 queda amparado cómo viaja LibVLC, y si la oferta escrita basta para el
§3 de la GPL-2.0. / Closes by engineering the two licence questions `REL-004` left to an opinion.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-14.

## Por qué las preguntas existían / Why the questions existed

Leídos los textos que el propio paquete entrega, no de memoria: / Read from the texts the package
itself ships, not from memory:

- **LGPL-2.1 §6(b)** —la opción que uno esperaría para enlace dinámico— exige un mecanismo que
  «(1) use en tiempo de ejecución una copia de la biblioteca **ya presente en el sistema del
  usuario**». Aquí las DLL las trae el artefacto, así que la primera condición **no se cumple
  literalmente**. La segunda —funcionar con una versión modificada que instale el usuario— sí, y el
  aviso ya lo decía. De ahí la ambigüedad. / The first condition of 6(b) is not literally met.
- **§6(c)**, la oferta escrita, obliga a entregar «los materiales de 6(a)», que incluyen **el trabajo
  que usa la biblioteca** para poder relinkar. El aviso ofrecía sólo el fuente de LibVLC y
  LibVLCSharp. / The written offer must also cover the work that uses the library.
- **GPL-2.0 §3(b)** exige una oferta válida para «cualquier tercero», y el aviso encaminaba las
  peticiones al canal privado de `SECURITY.md`. / Section 3(b) requires an offer to any third party.

## La salida que no admite interpretación / The way out that needs no interpreting

**LGPL-2.1 §6(d)** y el **último párrafo del §3 de la GPL-2.0** dicen lo mismo: si el ejecutable se
ofrece para descarga desde un lugar designado, ofrecer acceso equivalente al fuente **desde ese mismo
lugar** *es* distribuir el fuente. No hay nada que interpretar. Y había una circunstancia
afortunada: `release.yml` **no publicaba ninguna versión** —sólo subía artefactos de Actions—, así que
el canal se pudo diseñar bien en vez de corregirlo después. / Both licences accept equivalent access
from the same designated place, and the release channel had not been built yet.

## Lo que se midió por el camino / What was measured on the way

**1. La versión que el aviso nombraba no existe.** Decía «libvlc 3.0.23.1», que es la versión del
paquete NuGet, cuyo cuarto dígito es su propia revisión de empaquetado. `libvlc.dll` declara
`FileVersion = 3.0.23`, y `https://download.videolan.org/vlc/3.0.23.1/` responde **404**: VideoLAN
publica `vlc-3.0.23`. El aviso apuntaba a un fuente correspondiente que no se podía obtener con ese
número. / The notice named a version whose source does not exist.

**2. Una descarga que responde no es una descarga que funciona.** La primera ejecución del guion pidió
el archivo de LibVLCSharp a `code.videolan.org` y recibió **una página anti-bot**: 4.445 bytes de HTML
que empiezan por `3C 21` —`<!`— guardados bajo el nombre `LibVLCSharp-3.10.0.tar.gz`, camino de
adjuntarse a una versión como si fueran el código fuente. El nombre de un archivo no prueba nada sobre
él. Ahora se comprueba la firma del formato y un suelo de tamaño antes de que nada lo dé por bueno, y
la fuente pasó al espejo que sí lo sirve. / The first run downloaded an anti-bot page and would have
attached it as source code.

```
antes / before   4 445 bytes   3C 21 ...   (HTML: "Making sure you're not a bot!")
después / after  4 439 087 bytes  1F 8B     (gzip)
```

## Lo que hace ahora / What it does now

`eng/fetch-corresponding-source.ps1`, ejecutado por `release.yml` antes de subir el artefacto, trae el
fuente correspondiente y lo deja junto al binario para que se adjunte a la misma versión: / Fetches
the corresponding source and leaves it beside the binary:

| Archivo / File | Tamaño / Size | Comprobación / Check |
|---|---:|---|
| `vlc-3.0.23.tar.xz` | 26 486 988 | SHA-256 contra el que **VideoLAN publica** / against the digest VideoLAN publishes |
| `LibVLCSharp-3.10.0.tar.gz` | 4 439 087 | firma gzip y suelo de tamaño; su huella se registra y la versión la firma / gzip magic and a size floor; its digest is recorded and the release signs it |

`eng/corresponding-source.json` es el registro, y una prueba compara su campo `carriedBy` contra lo que
la compilación **resolvió de verdad**: subir la versión de un paquete sin subir la del fuente ofrecería
el fuente de otra cosa, en silencio. Otra prueba exige que el aviso nombre el árbol de fuente y su
dirección, para que no vuelva a apuntar a una versión inexistente. / A test compares the registry
against what the build actually resolved.

El aviso, reescrito, nombra el sitio del que se descarga, explica por qué los dos números de versión no
son el mismo, dice que el trabajo que usa la biblioteca es este programa y **es público** bajo
`GPL-3.0-or-later` —lo que faltaba para 6(a)/6(c)—, y conserva la oferta escrita para canales donde «el
mismo sitio» no significa nada, como una tienda, ahora explícitamente **para cualquier tercero**. / The
notice now covers what 6(a) and 3(b) asked for.

## Lo que esto no es / What this is not

No es un dictamen jurídico ni lo sustituye: es cumplimiento verificable contra el texto de las
licencias. Lo que consigue es que las dos preguntas dejen de ser interpretativas — la opción elegida
es la que ambas licencias enuncian sin condiciones. De `REL-004` queda lo que de verdad necesita
criterio ajeno: **marca, dominio y la notificación de exportación**. / This is not a legal opinion and
does not replace one.

## Verde / Green

| Puerta / Gate | Resultado / Result |
|---|---|
| `LicenceTextTests` | 22 de 22 / of 22 |
| `ApSolutions.LocalMedia.DocumentationTests` | 87 de 87 / of 87 |
| `eng/fetch-corresponding-source.ps1` | ejecutado: 2 archivos, uno verificado contra su digest publicado / run: two archives, one verified against its published digest |
