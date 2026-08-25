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

Los tamaños que el prototipo gasta, y los únicos que hay: 14 para un galón, 16 para una fila de menú,
18 para un aviso, 20 para un destino del rail, 22 para el conmutador de reproducción.

Dos formas son de esta aplicación y lo dicen: `IconStop`, porque su transporte tiene una parada donde
el prototipo tiene un solo conmutador, y `IconChevronUp`, que es `IconChevronDown` del revés.

## La alineación vertical

El problema que vuelve, y por qué vuelve.

Una tipografía no tiene ascendente y descendente simétricos: una etiqueta centrada al píxel dibuja su
tinta —de la cima de una mayúscula al pie de un descendente— **2,43 px por debajo** del centro de su
caja. Medido con las métricas de la fuente, no con una captura.

La compensación son **5 px**, que es el doble de la medida porque un margen a un lado mueve una caja
centrada la mitad de él. Y va **en la etiqueta**, nunca en el relleno del botón:

- Un relleno inferior en el botón mueve **todo** el contenido —el icono también—, así que un icono y
  la palabra a su lado siguen desalineados exactamente lo mismo que antes. Cambia dónde está la fila
  entera, no cómo se relacionan sus dos piezas.
- Un margen inferior en el `TextBlock` mueve **sólo la palabra**, que es lo único que tiene línea base
  que responder. El icono se queda centrado por su geometría, y los dos coinciden.

Por eso `Path.icon` no lleva compensación y `TextBlock` dentro de un botón sí. `ButtonOpticalCentreTests`
sostiene las dos afirmaciones a un píxel: la tinta centrada en su botón, y la tinta centrada respecto
del icono que tiene al lado.

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
