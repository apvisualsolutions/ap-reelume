# PLY-018 — Lo que le pasa de verdad a un vídeo que se ve mal / What is actually wrong with a video that looks bad

- Fecha / Date: 2026-09-12
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, ffmpeg 8.x.
- IDs: `PLY-018=IN_PROGRESS`
- Pruebas re-ejecutables / Re-runnable tests:
  `tests/ApSolutions.LocalMedia.Domain.Tests/Playback/PictureAdjustmentTests.cs`,
  `tests/ApSolutions.LocalMedia.MediaTests/Playback/PackedYuvConverterTests.cs`,
  `tests/ApSolutions.LocalMedia.MediaTests/Playback/PictureAdjustmentOnRealFramesTests.cs`,
  `tests/ApSolutions.LocalMedia.Domain.Tests/Continuity/PreferenceResolutionTests.cs`,
  `tests/ApSolutions.LocalMedia.Application.Tests/Playback/ApplyPlaybackPreferencesTests.cs`,
  `tests/ApSolutions.LocalMedia.IntegrationTests/Playback/PlaybackPreferenceRepositoryTests.cs`,
  `tests/ApSolutions.LocalMedia.IntegrationTests/Data/MigrationHistoryTests.cs`

## Veredicto / Verdict

**La imagen no estaba borrosa: estaba aplastada en negro, y se estaba construyendo la pieza
equivocada.** `PLY-016` promete nitidez al ampliar, y el archivo que motivó la queja no tiene ese
defecto como el dominante. /
**The picture was not soft, it was crushed into black, and the wrong feature was being built.**

## Lo que se midió, y por qué cambió el plan

El propietario preguntó por qué el vídeo seguía viéndose mal y dio la ruta de un episodio suyo. Nada
de ese archivo entra en este repositorio —ni ruta ni título, que es la regla 5—, así que aquí van
sólo las cifras.

| Qué | Medido |
| --- | --- |
| Resolución | **720 × 404**, con píxeles no cuadrados (SAR 404:405, que distorsiona un 0,25 %: invisible) |
| Códec | **MPEG-4 Simple Profile**, de 2003 |
| Tasa de vídeo | **1,5 Mbit/s** |
| Audio | MP3 a 128 kbit/s |
| Los diez episodios | del mismo tamaño, así que es la copia entera y no un archivo suelto |

Y el dato que cambió la decisión, el brillo medio de cinco escenas del mismo archivo:

| Momento | Luma mínimo | **Luma medio** | Luma máximo |
| --- | --- | --- | --- |
| 00:03 | 13 | **116,7** | 227 |
| 00:12 | 11 | **43,3** | 232 |
| 00:33 | 10 | **69,1** | 209 |
| 00:47 | 13 | **28,5** | 161 |
| 01:02 | 13 | **34,2** | **97** |

Sobre 235, que es el blanco de rango limitado. **En la última escena no hay un solo píxel en toda la
pantalla más claro que 97.** Eso no es falta de detalle: es que la mitad de la imagen está por
debajo de donde un panel la distingue del negro.

**El rango es limitado y correcto** —el luma vive entre 10 y 232, nunca llega a 0 ni a 255—, así que
la aplicación no estaba oscureciendo nada: el archivo es así.

## La conclusión que costó el cambio de rumbo

**Ampliar con más nitidez no hace nada por una imagen que está a oscuras.** `PLY-016` estaba a medio
construir y su primer eslabón ya está cerrado y verificado; lo que faltaba era una fila que
prometiera que una persona puede hacer algo con una película oscura, y no existía. Ahora es
`PLY-018`.

**Y la aplicación no tenía ningún control de imagen**: buscando brillo, contraste, gamma y
saturación por todo `src/`, lo único que aparece es el tema de la interfaz y el estilo de los
subtítulos. Ni un ajuste para el vídeo.

## La aritmética, y por qué es la de ffmpeg

La comparación que el propietario miró y aprobó la produjo `ffmpeg -vf eq=gamma=1.6`. Cualquier otra
fórmula aquí sería una imagen distinta de la que se firmó, así que se reproduce la suya
—`libavfilter/vf_eq.c`, `LGPL-2.1-or-later`, compatible con la `GPL-3.0` de este árbol— con su
`gamma_weight` fijo en 1, que es su propio valor por defecto.

**Con los tres controles en su valor neutro la tabla es la identidad exacta**, y eso no es un
redondeo afortunado: es lo que permite que el valor por defecto no cueste ni cambie nada. La
comprobación está sobre los píxeles —la conversión de producción con y sin tabla tiene que dar el
mismo búfer— y no sobre la política.

## La guarda que parecía defensiva y cazaba un defecto visible

`v <= 0` se escribió por costumbre. Al quitarla para medir si alguien la vigilaba, con brillo −0,5 y
contraste 2 la tabla devolvió **255 en el nivel 127 y 1 en el 128**: toda la zona oscura de la
imagen saldría **blanca**. Un `double` negativo convertido a `byte` no da cero, da lo que quede tras
la vuelta; y con una gamma distinta de 1 da `NaN`, que **sí** convierte a cero y esconde el mismo
agujero en este runtime y no necesariamente en otro.

Las tres filas que ahora lo miden llevan la monotonía de la curva como aserción, que es lo que un
desbordamiento rompe.

**Y una de esas filas falló por la aserción y no por el código**: pedía que el nivel 255 siguiera por
encima de 200 con brillo −0,9, cuando a ese brillo la imagen entera es casi negra por definición y
el 255 aterriza en 25. El control correcto es que la tabla siga distinguiendo negro de blanco.

## Una trampa de la previsualización de cobertura, medida de paso

`eng/preview-coverage-floors.ps1` **no ve un archivo nuevo que no esté commiteado**: busca con
`git diff --diff-filter=A`, y un archivo sin seguimiento no aparece ahí. Su «no new file falls
short» era cierto y no significaba nada sobre el archivo recién escrito.

**Y al intentar comprobarlo a mano se llegó a un 78 % que tampoco era cierto**, por sumar las ramas
de dos informes sin fusionar. La puerta **fusiona primero con `reportgenerator`** y lee un solo
informe, donde cada línea se queda con la mejor lectura. Reproducido con la fusión de verdad:
`PictureAdjustment.cs` mide **100 % de líneas y 100 % de ramas (16 de 16)**, y los dos controles
—`UpscalePolicy` y `YuvMatrixPolicy`, que llevan tiempo pasando la puerta— miden lo mismo. Sin esos
dos controles, el 78 % se habría creído.

## Y con todo eso medido, `gate-auditor` encontró seis puertas ciegas

Veinte mutantes aplicados, **seis vivos**. Los seis se volvieron a medir aquí antes de creérselos.

| Mutación que sobrevivía | Por qué nadie la veía | Qué la caza ahora |
| --- | --- | --- |
| El brillo entrando **antes** del contraste | con un solo mando en movimiento las dos fórmulas coinciden en todas partes, y las cinco filas movían uno cada una | una fila con los dos: brillo −0,5 y contraste 2 dan **129** bien y **1** mal |
| El motor olvidando `_pictureAdjustment = value` | ninguna prueba leía la propiedad después de escribirla | leerla, y comprobar que devuelve lo que se le dio |
| Construir la tabla siempre, también en neutro | **una tabla identidad da los mismos bytes que no tener tabla**, así que los dos lados de la comparación recorrían el mismo camino | `CarriesPictureLookup`, que hace visible un estado que desde fuera no se distingue |
| El motor aplicando **su** gamma fija de 1,6 | era el único valor que algo le pedía en todo el árbol | el control negativo: con la gamma bajo uno la media tiene que **bajar** |
| Ajustar sólo el primer luma de cada par | el muestreador avanzaba 16 píxeles, y **16 es par**: caía siempre en el primer luma. Daba 149,2, el mismo número que sin mutar | paso de 17, que alterna las dos mitades: ahora da 137,4 |
| Aflojar la guarda de la tabla a «más corta que 256» | sólo se probaba una longitud corta, así que una tabla de 512 quedaba **ignorada en silencio** | cinco longitudes, 1 a 512, y el nombre del parámetro |

**La primera es la peor de la tanda**, y no por su efecto —que es grande: el blanco a media luz— sino
por cuántas pruebas la dejaban pasar. **795 de 795 en `Domain.Tests`**, más las tres del motor. Un
punto ciego no es una prueba que falta, es una familia entera de filas que se mueven por el mismo eje.

### Y dos cosas que no eran mutantes

- **El presupuesto de recorte del diagnóstico dividía bytes entre 100 y contaba píxeles**: el listón
  real era el **4 %** de la imagen mientras el mensaje decía «una centésima». Un diagnóstico que
  nadie corre es justo donde sobrevive un número cuatro veces más flojo de lo que declara.
- **«El negro sigue negro» era falso**, y es lo que hay que saber antes de juzgar la función: una
  imagen de rango limitado **no contiene el nivel 0**. Su negro es 16, esta curva lo manda a 45 y la
  conversión lo deja en **34** en pantalla. **Las barras del letterbox SÍ se agrisan.** Es lo que
  hace el `eq` de ffmpeg y lo que el propietario miró y aprobó, así que es el comportamiento; pero
  un comentario que lo niega haría que el siguiente se fiara de un extremo que ningún vídeo alcanza.

## La preferencia que lo recuerda, y las cinco puertas ciegas que traía dentro

El ajuste viaja en `PlaybackPreference.Picture`, al lado de la velocidad, con los tres ámbitos que ya
existían: `File`, `Series` y `Global`, resueltos **campo a campo** y no objeto a objeto. La migración
`0023` añade tres columnas `REAL NULL`, y el repositorio las lee **al final** de su lista —índices 20
a 22— porque su lectura es posicional y meterlas junto a la velocidad habría desplazado en silencio
los seis campos del estilo de subtítulos.

**La decisión que gobierna todo lo demás: un neutro almacenado NO es un silencio.** `NULL` significa
«este ámbito no dice nada» y deja contestar al siguiente; un neutro guardado significa «aquí alguien
lo deshizo» y gana sobre el ámbito más ancho. De ahí que la migración **no rellene** las filas ya
almacenadas: hacerlo convertiría cada silencio en esa decisión, y un ajuste global no volvería a
alcanzar ninguna película ya vista.

Y el ajuste se escribe en el motor **al abrir, responda o no un ámbito**, que es justo lo contrario
de la regla de las pistas de al lado. El motor es un `singleton` y sobrevive al archivo, así que sin
esa escritura la película siguiente heredaría el ajuste de la anterior.

### Las cinco que `gate-auditor` encontró, cada una probada por mutación

| Mutación que sobrevivía | Por qué nadie la veía | Qué la caza ahora |
| --- | --- | --- |
| Quitar las tres columnas de imagen del `ON CONFLICT … DO UPDATE` | el round-trip sólo insertaba sobre una fila que no existía, y **en producción la fila casi siempre existe ya** —un volumen, una pista—, así que la única rama que se usa era la única sin medir | ajustar dos veces el mismo ámbito, con un volumen dentro para forzar el conflicto |
| Guardar `0/1/1` donde no hay ajuste | la prueba de «un campo sin poner sigue sin poner» afirmaba velocidad, subtítulo y estilo, y no la imagen | afirmar también `Picture`, que es lo que el comentario de esa suite lleva prohibiendo desde que se escribió |
| Que la migración rellenara las filas viejas | `pragma_table_info` describe la **forma** de la columna sobre una base recién creada, que **no tiene filas**; la promesa de la cabecera no la comprueba nadie | migrar a la 22, escribir una fila, migrar a la 23 y leerla: sigue callada |
| Que la serie ganara al archivo, sólo para la imagen | ninguna fila ponía valor en archivo **y** en serie a la vez, y sin esa fila los dos órdenes contestan lo mismo | los tres ámbitos con valor, más la vuelta sin el archivo |
| Dejar la guarda de `NULL` sólo en la primera columna | la fila fuera de rango ejercía el `catch` del dominio, nunca la fila a medias — y ese fallo es un `InvalidOperationException`, que el `catch` de al lado **no** atrapa | una fila con brillo y gamma y el contraste en `NULL` |

**La tercera es la más cara de las cinco si se escapa**, porque no rompe nada: deja cada película ya
vista llevando un neutro que nadie decidió, y el ajuste global deja de llegarles para siempre sin un
solo error por ninguna parte.

### Y una sexta, vista por inspección y no por mutación

`if (engine is IPictureAdjustable adjustable)` **no tenía rama de fallo en toda la suite**: el doble
de la prueba implementa la interfaz por construcción, así que un motor envuelto en un decorador
dejaría morir el ajuste en silencio y ninguna prueba lo diría. Ahora hay un motor de prueba que
reenvía todo `IMediaPlayerEngine` y **se deja atrás** esa interfaz, que es exactamente lo que un
decorador haría sin querer.

## Lo que queda

- El control en pantalla: el panel de imagen dentro del engranaje del reproductor (`ADR-0012`).
- El coste por fotograma medido contra un presupuesto, que el criterio de la fila promete y todavía
  no tiene ni una cifra de tiempo.
- El juicio final sobre el valor por defecto, que es del propietario y se firma por el ojo.
