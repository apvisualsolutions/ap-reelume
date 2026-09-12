# PLY-016 — El primer eslabón: de `UYVY` a `YUY2` sin tocar el color / The first link: `UYVY` to `YUY2` without touching colour

- Fecha / Date: 2026-09-12
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302.
- IDs: `PLY-016=IN_PROGRESS`
- Pruebas re-ejecutables / Re-runnable tests:
  `tests/ApSolutions.LocalMedia.MediaTests/Playback/PackedYuvConverterTests.cs`

## Veredicto / Verdict

**Los dos formatos llevan los mismos bytes en otro orden, así que el eslabón es un intercambio de
pares y no una conversión.** Diez casos de prueba nuevos, cuatro mutantes muertos, y la huella del
binario medida en cada vuelta para que «la prueba falló» no pueda significar «se midió el binario
anterior». /
**The two formats carry identical bytes in a different order, so the link is a pair swap and not a
conversion.**

## Lo que dijo la documentación, antes de razonar sobre ella

La regla 0 de este repositorio, aplicada a una API que no es de Avalonia. Dos páginas de Microsoft,
consultadas antes de escribir una línea:

| Pregunta | Fuente | Lo que contestó |
| --- | --- | --- |
| El orden de `YUY2` en memoria | *Recommended 8-Bit YUV Formats for Video Rendering* | «the first byte contains the first Y sample, the second byte contains the first U (Cb) sample, the third byte contains the second Y sample, and the fourth byte contains the first V (Cr) sample» |
| El orden de `UYVY` | la misma página | «This format is the same as the YUY2 format except the byte order is reversed — that is, the chroma and luma bytes are flipped» |
| Qué toma Direct3D | `DXGI_FORMAT` (dxgiformat.h) | `DXGI_FORMAT_YUY2` = **107**, «Width must be even», y el reparto `Y0→R8, U0→G8, Y1→B8, V0→A8` |
| Si existe un `UYVY` en DXGI | la enumeración entera | **No lo hay.** Por eso el intercambio es obligatorio y no una preferencia |

El 107 coincide con el que `D3d11UpscaleFormats.Battery` ya declaraba, leído del SDK el 2026-09-12
por otra vía. Dos caminos independientes al mismo número.

## El rojo, archivado

La prueba se escribió antes que el método, y falló dos veces por dos razones distintas. La primera
es la que una compilación da gratis; **la segunda es la que vale**, porque nombra el número:

```
error CS0117: 'PackedYuvConverter' no contiene una definición para 'UyvyToYuy2'
```

```
Assert.Equal() Failure: Collections differ
          ↓ (pos 0)
Expected: [30, 200, 60, 100]
Actual:   [0, 0, 0, 0]
```

El esqueleto de cuerpo vacío que produjo el segundo existió sólo para eso y no llegó a ningún
commit. Después, las cinco geometrías imposibles y las dos de filas fallaron de verdad —«No
exception was thrown» cinco veces, y «Exception type was not an exact match» las otras dos, que es
el `Slice` desbordando donde debía haber una guarda—.

## El control por mutación

Cuatro mutantes, uno cada vez, con la huella SHA-256 del ensamblado leída en cada vuelta. **La
huella importa tanto como el rojo**: sin ella, una prueba que falla puede estar midiendo el binario
de antes.

| Mutante | Huella del ensamblado | Quién lo cazó |
| --- | --- | --- |
| — (verde de partida) | `D6191B065366A086` | — |
| Copiar los cuatro bytes sin intercambiarlos | `FFC5864A7B3E50EF` | **tres** pruebas: el orden, las filas, y el control de la involución |
| Escribir con el paso del origen en vez del destino | `0172CEEF61DB1853` | sólo la de las filas, que es la única que los distingue |
| Convertir sólo la primera fila | `29F7F0E549486444` | sólo la de las filas |
| Quitar la guarda del paso del destino | `65EF6D3440C46709` | sólo la fila `(2, 1, 4, 3)` de la teoría de geometrías |
| Restaurado | `D6191B065366A086` | **la misma huella que el verde de partida**, byte a byte |

La última fila es la que cierra el círculo: el árbol que se commitea es exactamente el que dio
verde, no una variante parecida.

## Y aun así había dos puertas ciegas, que encontró `gate-auditor`

Los cuatro mutantes de arriba murieron, y eso no bastó. El auditor corrió **en un worktree aparte
con estos mismos cambios aplicados** y nombró dos mutaciones que la tanda dejaba pasar. Las dos se
volvieron a medir aquí antes de creérselas, y las dos eran ciertas:

| Mutación que sobrevivía | Por qué pasaba | Huella | Qué la caza ahora |
| --- | --- | --- | --- |
| `width * SourceBytesPerPixel` → `2 * SourceBytesPerPixel` en las dos guardas de paso | **las cinco filas de la teoría nombraban `width: 2`**, y ahí los dos números coinciden: se medía «al menos un macropíxel», no «al menos una fila» | `45EC2F02C2386260` | dos filas con `width: 4` |
| `source.Length < sourceStride * height` → `destinationStride * height` | la prueba de filas cortas llevaba **los dos pasos iguales**, así que no distinguía cuál de los dos leía la guarda | `C2095F12CB57883A` | la misma prueba con `8` y `4` |
| La misma primera mutación, pero en `UyvyToBgra` | **la teoría nueva se copió de una que ya estaba ciega**, y llevaba así desde que se escribió | `A30B9737D5AA3833` | dos filas con `width: 4` también allí |
| Intercambiar las mitades del macropíxel en vez de sus pares | **también es una involución**, así que la prueba de ida y vuelta y su control la aprobaban las dos | `BB9A6B8CAA94E718` | esa prueba nombra ahora los ocho bytes del paso sencillo |

**La tercera fila es la lección de la tanda**: al copiar una teoría se copió su punto ciego, y la
vieja llevaba meses sin medir el factor que decía medir. Corregir una cosa obliga a preguntar quién
más la escribe.

**La cuarta es la trampa propia de este eslabón.** Ser su propia inversa parece una propiedad fuerte
y es lo contrario: la comparten **cuatro** conversiones equivocadas —las mitades, la inversión
completa, y cada uno de los dos pares por separado—, así que «dos veces devuelve el original» y «no
es una copia» juntas siguen dejando pasar tres permutaciones falsas.

Y una guarda que no existía salió de la misma revisión: **con el mismo búfer de entrada y salida la
conversión destroza la imagen en silencio** —`{200, 30, 100, 60}` sale `{30, 30, 60, 60}`, la luma
duplicada sobre la croma—, porque el primer byte que escribe es el segundo que le falta por leer.
Es la tentación propia de este método y no del de BGRA, que escribe el doble de lo que lee. Ahora se
rechaza, y la excepción nombra el búfer. Las dos mitades de la comprobación de longitud se partieron
en dos por lo mismo: mientras las dos lanzaban la misma excepción nombrando el origen, la del
destino se podía borrar sin que ninguna prueba lo notara.

Verde final: **185 de 185** en `MediaTests`, con la huella `EFD562502E79AC1E` idéntica a la de antes
de las cuatro mutaciones de esta sección.

## La trampa que este eslabón tiene y no se ve

**El intercambio es su propia inversa**, así que «convertirlo dos veces devuelve el original» es
verdad también de una función que no hace nada. Esa prueba lleva su control al lado —el intermedio
**tiene** que diferir del original— y sin él habría sobrevivido el primer mutante, que es el más
obvio de todos.

Y los cuatro bytes de la prueba principal son cuatro valores distintos a propósito: con dos iguales,
una permutación equivocada pinta lo mismo. Es la lección de los primarios saturados, en otro
formato.

## Lo que este eslabón NO mide, y quién lo medirá

- **Nada de producción lo llama todavía.** La subida a la textura es el eslabón siguiente, y hasta
  que exista esto es aritmética probada y no una ruta viva. Es deliberado —el plan ordena los
  eslabones así porque cada uno se mide sin el siguiente— y es también el defecto característico de
  esta casa, así que queda escrito en vez de supuesto.
- **Que el procesador de vídeo acepte estos bytes ya está medido**, pero en la otra punta:
  `PLY16-d3d11-probe.md` mandó `YUY2` sintético a las dos tarjetas. Lo que nadie ha medido es un
  fotograma real de LibVLC recorriendo el camino entero.
- **El coste no se ha cronometrado**, y la razón es que el número que importaría es una resta: este
  bucle sustituye al que hoy escribe BGRA, que multiplica, recorta tres canales y escribe el doble
  de bytes por píxel. Medirlo aislado daría un número sin la mitad que cuenta.
