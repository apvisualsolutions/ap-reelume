# El logotipo de TMDB en Créditos / The TMDB logo in Credits

Evidencia del último punto abierto de los términos de TMDB: identificar su uso **con su logotipo**,
menos prominente que el del propio producto. La frase de atribución estaba fijada desde la revisión
legal; la marca no estaba. / Evidence for the last open point of TMDB's terms: identifying their use
**with their logo**, less prominent than the product's own. The attribution sentence had been pinned
since the legal review; the mark had not.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-10.

## Rojo archivado / Red archived

`TmdbLogoTests`: 7 con error de 7. El primero es el que importa — *«tmdb-logo.svg is missing, so the
credits draw a mark from nowhere»* — porque describe exactamente lo que faltaba. /
`TmdbLogoTests`: 7 failed of 7; the first message is the one that matters, because it names exactly
what was missing.

## Verde / Green

| Suite | Resultado / Result | Qué prueba / What it proves |
|---|---|---|
| `TmdbLogoTests` | 7 de 7 / of 7 | El archivo es el de TMDB, la vista dibuja su vector, la marca es menor que el nombre del producto, hay texto alternativo en los dos idiomas y nada es un enlace |
| `CreditsViewTests` | 4 de 4 / of 4 | La marca llega **a la pantalla** con la altura declarada y con anchura mayor que su altura |
| `TmdbContractTests` | 13 de 13 / of 13 | La frase de atribución sigue siendo la exigida, palabra por palabra |

## El archivo es el que TMDB publica / The file is the one TMDB publishes

No hay que creerle a quien lo descargó. TMDB incrusta la huella SHA-256 del recurso en su propia
dirección, así que la dirección **es** la comprobación. / Nobody has to trust whoever downloaded it:
TMDB embeds the asset's SHA-256 in its own address, so the address **is** the check.

| Dato / Item | Valor / Value |
|---|---|
| Origen / Source | `https://www.themoviedb.org/assets/2/v4/logos/v2/blue_short-<huella>.svg` |
| Huella que anuncia la dirección / Digest the address announces | `8e7b30f73a4020692ccca9c88bafe5dcb6f8a62a4c6bc55cd9ba82bb2cd95f6c` |
| Huella del archivo versionado / Digest of the versioned file | `8e7b30f73a4020692ccca9c88bafe5dcb6f8a62a4c6bc55cd9ba82bb2cd95f6c` |
| Tamaño / Size | 2 065 bytes, sin CRLF / no CRLF |
| Ruta / Path | `src/ApSolutions.LocalMedia.Presentation/Assets/tmdb-logo.svg` |

De las cinco variantes que publican se tomó `blue_short`, la marca «TMDB», que es la que acompaña a
una frase que ya dice TMDB; las otras cuatro son el logotipo cuadrado y dos versiones de «THE MOVIE
DB». / Of the five variants they publish, `blue_short` — the "TMDB" wordmark — was taken.

## Lo que se dibuja es su vector, no una imitación / What is drawn is their vector, not a lookalike

Avalonia no dibuja SVG. Traer `Avalonia.Svg.Skia` para una marca de 16 px habría añadido media docena
de paquetes y sus licencias al artefacto, justo después de cerrar la obligación de transportarlas. La
vista lleva la geometría del archivo —los 1 517 caracteres del atributo `d`, y los tres tonos del
degradado— y `TmdbLogoTests` compara las dos fuentes carácter a carácter. Una aproximación dibujada a
mano pasaría una revisión por captura de pantalla y muere en esa comparación. / Avalonia draws no
SVG, and a renderer for one 16-pixel mark would have added half a dozen packages and their licences
to the artifact right after the obligation to carry them was closed. The view carries the file's
geometry and a test compares the two character for character.

El archivo se excluye de `AvaloniaResource` a propósito: nada lo carga en ejecución, y empotrar dos
kilobytes que nadie lee es el defecto que este repositorio lleva un mes cazando. Su consumidor es la
prueba, y por eso sigue vivo. / The file is deliberately excluded from `AvaloniaResource`: nothing
loads it at runtime, and embedding two kilobytes nobody reads is the defect this repository keeps
catching. Its consumer is the test.

## «Menos prominente», medido / "Less prominent", measured

La especificación decía «24 px frente a 48 px del nombre del producto». Ese 48 no existe: el nombre
se dibuja a `FontSize="24"` en el raíl de navegación, y con el logotipo a 24 la condición habría
dejado de ser comprobable. Se midió y se corrigió. / The specification said "24 px against 48 px for
the product name". That 48 does not exist: the name is drawn at `FontSize="24"`, and a logo at 24
would have made the condition uncheckable.

| Medida / Measure | Valor / Value | De dónde sale / Where it comes from |
|---|---|---|
| Alto declarado del logotipo / Declared logo height | 16 | `CreditsView.axaml` |
| Tamaño del nombre del producto / Product name size | 24 | `ShellView.axaml` |
| Alto renderizado del logotipo / Rendered logo height | 16,0 | `CreditsViewTests` |
| Ancho renderizado del logotipo / Rendered logo width | > alto / > height | `CreditsViewTests` |

La última fila no es decorativa: una geometría que Avalonia no sabe leer se dibuja con anchura cero y
es indistinguible, en cualquier otra comprobación, de una que funciona. / That last row is not
decorative: a geometry Avalonia cannot parse draws at zero width and is indistinguishable, in every
other check, from one that works.

## Capturas / Captures

`CreditsViewTests.The_credits_are_captured_in_both_languages` escribe
`artifacts/ui-captures/TMDB-logo/credits-es-ES.png` y `credits-en-US.png` en cada ejecución. Muestran
la marca sobre la frase de atribución, con el degradado verde-azul y a la mitad de la altura del
encabezado de la vista. / The captures are written on every run and show the mark above the
attribution sentence.

## Qué queda de los términos de TMDB / What is left of TMDB's terms

Nada pendiente. Atribución, retención de 180 días y logotipo están los tres cerrados y con prueba. El
uso comercial sigue siendo la única puerta que cambiaría el cuadro, y sólo si algún día se cobrara por
el programa. / Nothing pending. Attribution, the 180-day retention and the logo are all three closed
and pinned by tests. Commercial use stays the only door that would change the picture, and only if
the program were ever charged for.
