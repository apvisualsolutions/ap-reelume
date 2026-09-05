# Catálogo de elementos

Qué dibuja cada control, en todos sus estados, y con qué token lo dibuja.

Esto no es una propuesta: es la lectura de
`design/Catálogo de elementos - AP Reelume.dc.html` —el catálogo que el propietario señaló como
canónico— pasada a los tokens de `Theme/DesignTokens.axaml`. Cuando el prototipo y este documento
discrepen, manda el prototipo y este documento está mal. Cuando este documento y un `.axaml`
discrepen, manda este documento y el `.axaml` está mal.

Los números del prototipo son CSS sobre una página de 1600 px. Se copian tal cual salvo donde este
árbol ya tiene una decisión medida en contra, y esas cesiones están escritas al final con su razón.

## Cómo leer una ficha

Cada elemento lleva sus estados en el orden en que el catálogo los ordena —**reposo · sobre ·
pulsado · foco · deshabilitado**— y, cuando el control tiene además una elección, **elegido** antes
que los cinco. Junto a cada estado va el token que lo pinta, no el color: un hexadecimal escrito aquí
sería correcto en uno de los cuatro diccionarios y falso en los otros tres.

Tres reglas valen para todo lo que sigue y no se repiten en cada ficha:

- **Deshabilitado lleva borde punteado** en las cuatro variantes de acción. Es la señal que no es
  color, y separa «deshabilitado» de «ausente» sin comparar dos grises.
- **El foco es un anillo doble**, igual en los cuatro temas: 1 px del color del fondo y 3 px del color
  de foco por fuera. En alto contraste claro el borde y el foco son el mismo negro y lo único que los
  separa es la geometría, que es justamente por lo que son dos anillos y no uno grueso.
- **Ninguna elección se dice sólo con color.** Cada control que elige lleva un segundo signo —un
  glifo, una barra, una marca— porque en los dos diccionarios de alto contraste el relleno de acento
  y el relleno normal resuelven al mismo blanco o al mismo negro.

## Acción · los cinco estados

Cuatro variantes, y la primaria aparece **una sola vez por pantalla**.

### Primaria

Alto 38 en el prototipo, `ControlHeight` (36) aquí, radio de píldora, peso semi-negrita, relleno
`20,0` a los lados.

| Estado | Relleno | Tinta | Borde |
| --- | --- | --- | --- |
| Reposo | `PrimaryActionBrush` | `PrimaryActionTextBrush` | igual que el relleno |
| Sobre | `PrimaryActionHoverBrush` | `PrimaryActionTextBrush` | igual que el relleno |
| Pulsado | `PrimaryActionPressedBrush` | `PrimaryActionTextBrush` | igual que el relleno |
| Foco | reposo + anillo doble | `PrimaryActionTextBrush` | igual que el relleno |
| Deshabilitado | `ControlFillDisabledBrush` | `TextDisabledBrush` | punteado |

### Secundaria

El mismo alto y el mismo radio, relleno `16,0`, sin fondo propio.

| Estado | Relleno | Tinta | Borde |
| --- | --- | --- | --- |
| Reposo | transparente | `TextPrimaryBrush` | `ButtonBorderBrush` |
| Sobre | `ControlFillHoverBrush` | `ControlTextActiveBrush` | `ButtonBorderBrush` |
| Pulsado | `ControlFillPressedBrush` | `ControlTextActiveBrush` | `ButtonBorderBrush`, 2 px |
| Foco | reposo + anillo doble | `TextPrimaryBrush` | `ButtonBorderBrush` |
| Deshabilitado | `ControlFillDisabledBrush` | `TextDisabledBrush` | punteado |

### De icono

Cuadrada de `ControlHeight` por `ControlHeight`, sin relleno interior, con el glifo centrado por su
propia geometría. El radio de píldora sobre un objetivo cuadrado la dibuja redonda: un solo número
para el círculo y para la píldora.

Los cinco estados son los de la secundaria. Lo único suyo es que **no lleva la compensación óptica**
—véase «La alineación vertical»—, porque una forma no tiene línea base que compensar.

### Enlace

Sin caja: sólo la palabra, en `AccentBrush`, semi-negrita.

| Estado | Tinta | Subrayado |
| --- | --- | --- |
| Reposo | acento | no |
| Sobre | acento aclarado | sí |
| Pulsado | acento oscurecido | sí |
| Foco | acento + anillo doble | no |
| Deshabilitado | `TextDisabledBrush` | tachado |

## Selección · cada control en sus formas

Aquí está la distinción que más se equivoca: **un menú y un desplegable no eligen igual**.

### Píldora de opción

Alto 32 en el prototipo, `ControlHeight` aquí, radio de píldora, relleno `0 15`, y **siempre** un
glifo de estado (`●` elegida, `○` sin elegir) además del color.

| Estado | Relleno | Borde | Tinta | Peso |
| --- | --- | --- | --- | --- |
| Elegida | `AccentSubtleBrush` | `AccentBrush` | `AccentInkBrush` | semi-negrita |
| Sin elegir | `ControlFillBrush` | **transparente** | `TextSecondaryBrush` | medio |
| Sobre | `ControlFillHoverBrush` | `ButtonBorderBrush` | `ControlTextActiveBrush` | medio |
| Foco | como sin elegir + anillo doble | transparente | `TextSecondaryBrush` | medio |

Lo que más se ha dibujado mal: **la píldora sin elegir no lleva borde y su texto no es el primario.**
Un borde permanente y una tinta plena hacen que las tres opciones parezcan tres elegidas.

Dónde: los cuatro temas, el idioma, el tipo de raíz, las pestañas de tipo de la biblioteca, el tipo
de marcador, el selector de temporada de una serie.

### Desplegable

Alto 32 en el prototipo, `ControlHeight` aquí, radio de píldora, relleno `0 13`. Lleva la etiqueta a
la izquierda al 72 % de opacidad y en tamaño de leyenda, el valor en semi-negrita, y el galón al
final.

| Estado | Relleno | Borde | Galón |
| --- | --- | --- | --- |
| Cerrado | `ControlFillBrush` | `ComboBoxBorderBrush` | `IconChevronDown` |
| Abierto | `AccentSubtleBrush` | `AccentBrush` | `IconChevronUp` |
| Sobre | `ControlFillHoverBrush` | `ComboBoxBorderBrush` | `IconChevronDown` |
| Foco | cerrado + anillo doble | `ComboBoxBorderBrush` | `IconChevronDown` |
| Deshabilitado | `ControlFillDisabledBrush` | punteado | `TextDisabledBrush` |

El panel que abre: relleno 4, radio `CornerRadiusMedium`, superficie `CardSurfaceBrush`, borde
`ComboBoxDropDownBorderBrush`, y las filas separadas por 2.

Sus filas, y aquí **sí** hay acento, que es lo que el prototipo dibuja:

| Estado de la fila | Relleno | Borde | Peso |
| --- | --- | --- | --- |
| Elegida | `ComboBoxItemBackgroundSelected` | `AccentBrush`, **1 px** | semi-negrita |
| Sobre | `ControlFillHoverBrush` | transparente | normal |
| Reposo | transparente | transparente | normal |

Dónde: los cinco desplegables de ajustes y filtros, más el de velocidad, que abre hacia arriba.

**El de velocidad tiene tres cosas propias** y todas vienen del prototipo. Sus filas son de tres
columnas —marca, nombre y nota: `● Normal · 1×`, `2× · más rápida`—, porque el número solo no dice
hacia dónde va: `0,75×` y `1,25×` están a la misma distancia de lo normal y se leen igual. Su panel
sale **por arriba**, que es lo único razonable en el borde inferior del reproductor. Y su cara
cerrada dice el multiplicador donde la fila dice la palabra: la píldora es `VELOCIDAD 1× ▲` y la fila
es `● Normal · 1×`.

La marca no es adorno junto al lavado y el borde: en los dos contrastes altos el relleno de la fila
elegida y el de reposo son el mismo color, así que la marca es lo único que queda diciendo cuál está
en vigor — la misma razón por la que las píldoras de apariencia llevan glifo de estado y el rail
lleva barra.

### Fila de menú

**Esto no es un desplegable y no se pinta como uno.** Un menú dice dónde estás, no qué has escogido
de una lista, y el prototipo lo dibuja con un lavado neutro y **ningún borde de acento**.

Alto 34 en el prototipo, `ControlHeight` aquí, radio `CornerRadiusSmall`, relleno `8,0`, hueco 11
entre el icono y la palabra.

| Estado | Relleno | Borde | Tinta | Peso |
| --- | --- | --- | --- | --- |
| Actual | `SelectionFillBrush` | `SelectionStrokeBrush` | `TextPrimaryBrush` | semi-negrita |
| Reposo | transparente | transparente | `TextSecondaryBrush` | normal |
| Sobre | `ControlFillHoverBrush` | transparente | `ControlTextActiveBrush` | normal |
| Foco | como esté + anillo doble | — | — | — |

`SelectionFillBrush` es el `rgba(127,145,170,.16)` del prototipo: un gris azulado al 16 %, el mismo
en claro y en oscuro. `SelectionStrokeBrush` es **transparente** en claro y oscuro, y el único color
del tema en los dos altos contrastes, donde el lavado no dice nada y la geometría tiene que decirlo
todo.

Dónde: el índice lateral de ajustes, las filas de los menús desplegables del rail, y cualquier
`ListBoxItem` que no sea una tarjeta.

### Destino de navegación

46 × 42, radio 12, y **el acento es una barra, no un borde**: 3 px de ancho a la izquierda del botón,
con 11 px de aire arriba y abajo, presente o ausente.

| Estado | Relleno | Barra | Tinta del glifo |
| --- | --- | --- | --- |
| Actual | `SelectionFillBrush` | `AccentBrush` | `TextPrimaryBrush` |
| Reposo | transparente | ausente | `TextSecondaryBrush` |
| Sobre | `ControlFillHoverBrush` | ausente | `ControlTextActiveBrush` |
| Foco | como esté + anillo doble | — | — |

La acción del pie del rail —«Añadir medios»— comparte el tamaño y se distingue por lo único que a los
cinco destinos se les niega: un borde de hairline. Nunca es «actual», así que no tiene ni lavado ni
barra.

### Interruptor

42 × 24, radio de píldora, pomo de 18 con 2 px de aire, transición de `MotionDuration`.

| Estado | Vía | Borde | Pomo |
| --- | --- | --- | --- |
| Activado | `AccentBrush` | `AccentBrush` | `AccentTextBrush`, a la derecha |
| Desactivado | `ControlFillBrush` | `ComboBoxBorderBrush` | `TextSecondaryBrush`, a la izquierda |
| Foco | como esté + anillo doble | — | — |
| Deshabilitado | `ControlFillDisabledBrush` | punteado | `TextDisabledBrush` |

Y **su estado va escrito al lado**, no sólo en la posición del pomo.

### Fila elegible

La fila de una lista donde se escoge una cosa entre varias —versiones, marcadores, pistas,
duplicados—. Relleno `11,9`, radio `CornerRadiusMedium`.

| Estado | Relleno | Borde |
| --- | --- | --- |
| Elegida | `AccentSubtleBrush` | acento aclarado, 1 px |
| Reposo | `ControlFillBrush` | `ShellHairlineBrush` |
| Sobre | `ControlFillHoverBrush` | `ButtonBorderBrush` |
| Foco | reposo + anillo doble | `ShellHairlineBrush` |
| Deshabilitada | sin relleno | punteado, tinta `TextDisabledBrush` |

## Estado, entrada y distintivos

### Los cinco tonos de estado

| Tono | Relleno | Borde | Signo | Para |
| --- | --- | --- | --- | --- |
| Neutro | `ControlFillBrush` | `ShellHairlineBrush` | `○` | un proceso en marcha |
| Positivo | `PositiveSurfaceBrush` | `PositiveBorderBrush` | `✓` | al día, vacío deseable |
| Advertencia | `WarningSurfaceBrush` | `WarningBorderBrush` | `!` | los ocho rechazos |
| Error | `DangerSurfaceBrush` | `DangerBorderBrush` | `✕` | los siete fallos |
| Ausente | sin relleno | punteado | — | el control no está |

### Campos

Alto `ControlHeight`, radio `CornerRadiusMedium`, relleno `11,0`, borde `TextControlBorderBrush`
sobre `ControlFillBrush`. **Las rutas van siempre en monoespaciado.** Un campo con error cambia el
borde a `DangerBorderBrush` y nada más; un campo vacío escribe su marcador en `TextDisabledBrush`.

### Distintivos

Píldoras de 11 px en negrita, relleno `10,3`:

- **Disponible** — `PositiveSurfaceBrush` con tinta `PositiveBorderBrush`.
- **No disponible** — `WarningSurfaceBrush` con tinta `WarningBorderBrush`, y el triángulo de aviso.
- **Película** y **Serie** — un tinte neutro con el glifo del tipo. En una carátula de rail va sólo el
  glifo; la palabra sólo cabe en la rejilla de la biblioteca.

Y los tres símbolos de progreso, que son literales del árbol y se quedan: `○` sin empezar, `◐` en
curso, `●` visto. El nombre accesible viene de su clave, nunca del símbolo.

## Contenedores y tipografía

### Fila de lista

**Rejilla `1fr auto`, nunca una fila horizontal.** Un `StackPanel` horizontal ofrece ancho infinito a
sus hijos, y eso es lo que dibujó «Quitar» en x = 2146 dentro de una ventana de 1600. Relleno `13,11`,
radio `CornerRadiusMedium`.

### Tarjeta y hairline

Dos tokens y no uno: `ShellHairlineBrush` separa superficies y `ButtonBorderBrush` delimita lo que se
puede pulsar. Una tarjeta lleva `CardSurfaceBrush`, hairline, `CornerRadiusMedium` y `ElevationShadow`.

### Panel superpuesto

**Las dos dimensiones acotadas y la alineación explícita.** Acotar sólo el ancho dejó un diálogo
saliéndose por arriba y por abajo; sin alineación, un panel se midió a 1280 × 1400.

### Tipografía

| Papel | Tamaño | Peso | Token |
| --- | --- | --- | --- |
| Display | 32 | 300 | `FontSizeDisplay` |
| Subtítulo | 20 | 600 | `FontSizeSubtitle` |
| Cuerpo | 14 | 400 | `FontSizeBody` |
| Leyenda | 12 | 400 | `FontSizeCaption` |
| Antetítulo | 10,5 · .18em | 400 | `hero-overline` |

Las rutas y los códigos van en monoespaciado. El peso 300 se gasta sólo en títulos de pantalla y en
el héroe de Home.

## Los iconos

Todos vienen de la misma función del prototipo, `icon(n, s)`: un SVG de 24 × 24 con `fill:none`,
`stroke:currentColor`, `stroke-width:1.6` y remates y uniones redondos. **No son una fuente de
pictogramas**, y no se mezclan con una: un glifo de Segoe Fluent es sólido y viene de otra tradición
de dibujo.

Viven en `Theme/Icons.axaml` como geometrías, convertidas y no redibujadas: los `path` van literales y
los `rect` y `circle` del prototipo se convirtieron en los arcos que los dibujan. El grosor no es un
número único —Avalonia escala la geometría a los límites del control y luego la traza—, así que cada
tamaño lleva el suyo: `1,6 × tamaño ÷ 24`.

**Y cada geometría empieza por `M0 0 M24 24`, que es el lienzo y no dibujo.** Sin él, los límites de
una geometría son los de su propia tinta, y `Stretch="Uniform"` estira cada trazo por un factor
distinto hasta llenar la caja: medido el 2026-08-29, eso dejaba los iconos entre **1,12× y 1,74×** más
grandes que el prototipo y hasta **4,5 px descentrados**, y volvía falsa la premisa de la fórmula del
grosor —que la tinta llena el lienzo—. Dos `moveto` que no dibujan nada devuelven la caja a `0,0
24×24`, y una puerta lo parsea para las treinta y una. La medición está en
[el lienzo que la portación no copió](../evidence/stable/audit-icon-canvas.md).

Las clases de tamaño valen lo que dice su nombre: `size-20` es `Width="20"`, que es literalmente
`icon(n, 20)`. **Llevaron un `-2` del 2026-08-25 al 2026-08-29** —`size-20` daba 18— porque los
iconos se veían grandes y se encogió la caja a ojo; no podía funcionar, porque el exceso era un
factor distinto para cada icono y estaba en la geometría, no en la clase.

**Los tamaños que el prototipo gasta son nueve**, contados el 2026-08-29 con
`icon\([^)]*,\s*[0-9]+\)` —el patrón tiene que aceptar una **expresión** como primer argumento, o se
pierden diez llamadas, entre ellas el conmutador de reproducción—:

```
 12 → 2 usos     14 → 8     16 → 5     20 → 3     26 → 1
 13 → 2          15 → 10    18 → 5     22 → 1
```

Y así se reparten contra lo que la aplicación declara, medido contexto a contexto:

| contexto | clase | prototipo |
|---|---|---|
| play, pausa y parada del transporte y del mini | `size-22` | 22 |
| atrás y adelante del transporte, destinos del rail | `size-20` | 20 |
| cromo del reproductor: mini, pantalla completa, cerrar, volumen | `size-18` | 18 |
| búsqueda, fila de menú | `size-16` | 16 |
| acciones personales | `size-15` | 15 |
| galón, aviso, **el play de una tarjeta** | `size-14` | 14 · **el play, 15** |
| el glifo de tipo sobre una portada | `size-12` | 12 |

Los 13 y el 26 del prototipo no tienen clase porque **esta aplicación no dibuja nada en esos dos
sitios**: son un `check` de una lista de opciones y la carpeta grande de un estado vacío que aquí se
resuelve de otra manera.

**El play de una tarjeta es la única desviación deliberada, y su precio está medido.** El prototipo lo
dibuja a 15 y aquí va a 14, un píxel menos. Subirlo a 15 se probó el 2026-08-29 y **movió la entrada
de biblioteca 44 px hacia abajo en 6 de las 36 combinaciones** de `HomeLayoutTests` —las de
1366 × 768 a escala 150 en español, que es la más apretada que la aplicación admite—, porque algo
envuelve una línea más. La ganancia era **0,55 px de tinta**; el coste, 44 de desplazamiento. Ochenta
a uno en contra, así que se queda en 14. Si algún día esa fila deja de ir justa, el cambio son dos
caracteres.

Dos formas son de esta aplicación y lo dicen: `IconStop`, porque su transporte tiene una parada donde
el prototipo tiene un solo conmutador, y `IconChevronUp`, que es `IconChevronDown` del revés.

## La alineación vertical

El problema que vuelve, y por qué vuelve. **Esta sección se reescribió entera el 2026-08-29**, con
la primera medición que leyó píxeles en vez de cajas; lo que decía antes está al final, porque el
error importa más que la corrección.

Una tipografía no tiene ascendente y descendente simétricos, así que una etiqueta centrada a la caja
dibuja su tinta por debajo del medio. **En pantalla ese error es 1 px**, medido rasterizando un botón
real con `CaptureRenderedFrame()`.

La compensación es **1 px y va en el contenido del botón** —la etiqueta cuando es lo único que hay,
el panel cuando hay un icono al lado—, nunca en la etiqueta suelta cuando comparte fila con un icono:

- Un margen sobre la etiqueta **hace crecer el panel donde la etiqueta vive**, y ese panel se centra
  como un todo. Así que mueve también el icono, sólo que la mitad y en la otra dirección: en pantalla
  dejaba el icono en el centro del botón y la palabra **2 px por encima**.
- Un margen sobre el contenido mueve por igual todo lo que el botón dibuja, y por eso no puede
  separar dos piezas que van juntas. Es la única forma que las mantiene alineadas entre sí **y**
  centradas en el botón.

`Path.icon` sigue sin llevar compensación: una forma se centra por su geometría y un botón cuyo
contenido entero es un icono no necesita ninguna.

**Lo que decía esta sección hasta el 2026-08-29, y por qué era falso.** Decía que la compensación era
de **5 px**, derivada de una asimetría de **2,43 px** calculada con las métricas de la fuente, y que
un margen en el `TextBlock` movía «sólo la palabra». Las tres afirmaciones eran incorrectas:

- **2,43 px es el número del modelo, no el de la pantalla.** El rasterizado dice 1.
- **5 px movía la palabra 3**, de 1 px baja a 2 px alta: sobrecorregía pasándose del centro.
- **Un margen en la etiqueta no mueve sólo la palabra.** Mueve el panel, y con él el icono. Esa
  premisa es la que dejó **53 botones** con su icono y su palabra 2 px separados.

Y el motivo de que nadie lo viera: `ButtonInkTests` medía la caja y `ButtonOpticalCentreTests` la
tinta **calculada** desde las métricas. Ninguna dibujaba nada. La segunda está retirada —su método
asumía **siempre** un descendente, y sobre «Guardar el informe», que no tiene ninguno, contestaba
2,43 px donde el rasterizado mide 0,0— y la sustituye `ButtonPixelCentreTests`, que rasteriza y lleva
la palabra como **parámetro**: el centro de la tinta no es propiedad de la fuente sino de la cadena,
y va de +0,62 («Guardar el informe») a +3,82 («ppp») según lleve descendente.

## Las cesiones, con su razón

Lo que este árbol dibuja distinto del prototipo, y por qué.

- **Los controles miden 36 y no 32 ni 38.** 36 px es el objetivo más pequeño que WCAG 2.2 acepta en
  AA. Una escala con tres alturas de control es tres oportunidades de que una fila no cuadre.
- **El radio de píldora es 999 y no la mitad del alto.** El dibujo recorta a la mitad del lado corto,
  así que un objetivo cuadrado sale círculo y uno ancho sale píldora con un solo número.
- **El acento del borde de foco se aparta un paso del acento del tema** cuando coinciden. Está escrito
  en la prueba y no afirmado a la baja.
- **En los dos altos contrastes el relleno no dice nada** y lo dice el segundo signo: el glifo de la
  píldora, la barra del rail, el borde de la fila de menú.
- **El aviso de acceso denegado no lleva el botón «Permisos» que el prototipo dibuja.** Ese botón
  abre los ajustes de Windows para ese recurso, y eso es arrancar un proceso del sistema: vive en la
  capa del anfitrión, que es el único sitio de este árbol con `Process.Start`, y traería su propia
  superficie de ataque a una decisión de forma. Decidido por el propietario el 2026-09-05 con la
  comparación de las sesenta pantallas por delante, para que la ausencia no se cuente como defecto
  en cada vuelta. El aviso ya dice qué pasa y que la aplicación nunca cambia permisos por su cuenta,
  que es la mitad que importa. Si se reabre, se reabre como alcance nuevo y con su decisión escrita
  delante del código.
