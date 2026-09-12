# Hoja de ruta

Qué hace AP Reelume hoy, qué hará después y qué ha decidido no hacer. La versión inglesa está en
[README.en.md](README.en.md). El registro canónico del alcance es
[FEATURES.md](../FEATURES.md); esto es su lectura en prosa.

## La regla de publicación

**No se publica nada hasta que todo lo comprometido esté verificado.** Decisión del propietario del
2026-08-31, y manda sobre la lectura habitual de las tres versiones de abajo: no se corta una
primera publicación parcial para ir mejorándola después. Las tres versiones siguen ordenando **en
qué orden se construye**; ya no autorizan **publicar** al terminar la primera.

Qué cuenta como «todo», para que la regla sea comprobable y no una intención:

- **Cuenta** cada fila de [FEATURES.md](../FEATURES.md) que la matriz reconoce como compromiso
  —`DESIGN_APPROVED`, `PLANNED`, `IN_PROGRESS`, `IMPLEMENTED`, `BLOCKED`— y también las `DEFERRED`,
  que son compromisos aplazados y no rechazados. Todas tienen que llegar a `VERIFIED`.
- **No cuenta** lo que está `OUT_OF_SCOPE`, porque no es una funcionalidad pendiente sino una
  decisión escrita de no hacerla —hoy `UX-008` y `PLY-015`—. Meterlas exige una decisión nueva, no
  esta regla.

`pwsh -NoProfile -File eng/list-pending.ps1` contesta en cualquier momento cuánto falta, y separa
las dos categorías por su cuenta.

**Y el 2026-09-11 el propietario la endureció: «todas las mejoras deben ser aplicadas cuanto antes o
no habrá release».** Una mejora que se descubre o se registra va al orden inmediato, no a una lista
sin plazo. Lo dijo al ampliar `PLY-016`, la mejora de imagen para vídeos de poca resolución: cubre
cualquier vídeo por debajo de la resolución de la pantalla —720p o 1080p en una 4K— y **completa**,
con la superresolución del propio fabricante en NVIDIA, Intel y AMD y un reescalador portátil en el
resto. **No espera a VLC 4**: ese día VideoLAN publicaba como estable la 3.0.23 y la 4 sólo como
compilación nocturna «inestable» y sin soporte, y el canal de paquetes no tenía ninguna 4. **Y no
hace falta**: la 3.0.23 que se instala ya trae la superresolución de los tres fabricantes en su salida
D3D11 —NVIDIA e Intel desde la 3.0.19 y AMD desde la 3.0.21, comprobado en el binario—, aunque sólo
actúa en la ventana propia de VLC y no en la composición de esta aplicación, que dibuja sus controles
encima del vídeo. Llevarla a esa composición, y el reescalador portátil para las demás tarjetas, es
el trabajo de `PLY-016`; la afirmación de agosto de que exigía VLC 4 era falsa y está corregida en su
evidencia.

**Lo que esta regla convierte en bloqueo de publicación, y conviene saberlo pronto:** `PRD-002` no
puede llegar a `VERIFIED` sin el **certificado comercial de firma**, porque su ciclo se verificó
sobre una copia resellada y el artefacto sin firmar no puede repetirlo — lo que lo encadena a
`REL-001`.

**Y desde el 2026-09-12 hay un segundo, y es de gasto: la mitad de AMD de `PLY-016`.** Puesta la
tarea al propietario con su recomendación —encender la Súper resolución RTX en NVIDIA App, activar la
gráfica integrada del i7 y autorizar una máquina con tarjeta AMD en la nube, unos 0,11 $/h en spot o
0,62 en normal—, autorizó las dos primeras y **no la tercera**. Las dos primeras están medidas:
Windows ve la RTX 5070 y la UHD 770. Sin máquina AMD, su superresolución se puede escribir y **no se
puede verificar**, y un indicador que dice «encendido» sin haber comparado píxeles es exactamente lo
que esa fila prohíbe. Así que `PLY-016` sólo llegará a `VERIFIED` en dos terceras partes hasta que esa
decisión de gasto se tome: es del propietario, se le vuelve a plantear cuando la cadena de AMD esté
escrita, y la recomendación sigue siendo sí — son horas sueltas de una máquina, no una compra.

**Y `PRD-003` dejó de ser lo que esta línea decía, el 2026-09-04.** Decía que dependía de «una
máquina Windows 11 ARM64 que no hay». La hay y es gratis: GitHub ofrece runners hospedados de
Windows 11 ARM64 —`windows-11-arm`—, **gratis e ilimitados en repositorios públicos**, y éste lo es
desde el 2026-08-10.

**Y las seis fases se pueden intentar, porque ninguna necesita hardware.** Eso costó dos
suposiciones falsas antes de leer las pruebas que cada fase ejecuta: la de audio corre el motor en
modo mudo y comprueba **lo que el vídeo trae**, no lo que sale por los altavoces; la de HDR
**inyecta** una pantalla fingida para los dos casos y decodifica por software a propósito. La matriz
lo decía desde el principio: las seis llevan la **misma** razón de bloqueo —«esto se ejecutó en un
anfitrión x64»—, y ninguna menciona sonido ni pantalla. `VideoLAN.LibVLC.Windows` trae binarios
ARM64 nativos con sus complementos, comprobado en el paquete descargado.

**Lo que falta por saber es si esa imagen —que la mantiene Arm, LLC y no es la misma que la de
x64— trae las herramientas que el flujo espera**, empezando por `ffmpeg`. Eso sólo se sabe
corriéndolo, y es la tanda prioritaria de la sesión siguiente. Hasta medirlo, `PRD-003` sigue
`BLOCKED`: lo que cambia es que el desbloqueo ya no exige comprar nada.

**Y un tercero que ya está resuelto, el mismo 2026-09-01:** `PLY-004` estaba bloqueado porque los
cuatro endpoints físicos de este equipo declaran mezcla de dos canales. El propietario decidió que un
endpoint **virtual** de ocho canales lo verifica, anotándolo en la evidencia; se instaló VoiceMeeter
Banana —VB-CABLE quedó descartado porque su propio foro documenta que entrega los ocho canales por
Kernel Streaming y no siempre por WASAPI compartido, que es la vía que usa la aplicación—, y sobre
ese endpoint se **grabó la salida y se contaron los ocho canales**: cada uno lleva su propio tono con
un contraste mínimo de 86 dB. `PLY-004` pasa a `VERIFIED` y **de los tres bloqueos de publicación
quedan dos**.

**Ese párrafo se escribió el 2026-09-01 y decía «los dos que exigen comprar: la máquina ARM64 y el
certificado de firma». Ya no son dos compras, sino una**, y lo refuta el bloque de arriba fechado el
2026-09-04: los runners `windows-11-arm` de GitHub son gratis e ilimitados en repositorios públicos.
Sigue habiendo dos bloqueos y `PRD-003` sigue `BLOCKED` hasta que se mida qué contestan sus seis
fases; lo que ya no es cierto es el motivo por el que lo estaba. Se corrige aquí porque **ninguna
prueba cruza las dos afirmaciones**: `ScopeBoundaryTests` sólo exige que `PRD-003` aparezca nombrado
en los dos idiomas, no que lo que se diga de él concuerde consigo mismo.

### Lo decidido el 2026-09-05 y todavía sin construir

**Las portadas tienen tres orígenes y un orden, y está escrito en
[ADR-0009](../adr/0009-a-cover-has-three-origins-and-an-order.md).** La elegida a mano gana, luego la
del proveedor, y si no hay ninguna se saca un fotograma del vídeo — para películas y series también,
no sólo para cursos. El orden se cambia en un ajuste general y se puede saltar en un título concreto,
con la galería que el prototipo ya dibuja. Cierra un defecto medido: hoy un solo campo guarda dos
cosas, y refrescar contra el proveedor deja la portada de alguien huérfana dentro de cada copia de
seguridad.

**Y quedan doce cosas construidas que ninguna pantalla enseña**, de las dieciocho que encontró
[la auditoría del 2026-09-04](../evidence/stable/audit-built-and-not-drawn.md). **La cuenta se midió
una a una el 2026-09-06** y las seis cerradas son la biblioteca que se cortaba en cincuenta títulos,
la ficha de la cuenta atrás que prometía ser configurable sin serlo —**cerrada de verdad el
2026-09-05**, con la sección «Reproducción» de Ajustes—, los tres nombres del mini reproductor, las
cadenas huérfanas, que además ganaron [una puerta](../evidence/stable/audit-orphaned-strings.md) para
que no vuelvan, **el escaneo que se podía cancelar por dentro y no por fuera**, cerrado esa misma
tarde con [la franja de avisos](../evidence/stable/audit-lib002-the-notices-strip.md), y **editar la
ficha de un episodio suelto, que se cierra porque su premisa era falsa** — la ficha de serie sí tiene
ruta al editor. Las doce restantes van en dos grupos: lo que sólo falta enseñar, y lo que el diseño
tiene y la aplicación no.

**Este párrafo contaba «las portadas en la rejilla» entre las seis, y eso era contar mal aunque el
total saliera bien**: esa portada fue el **detonante** de la auditoría, no uno de sus dieciocho, y a
cambio daba por abierto el que ya estaba cerrado. Dos cuentas que coinciden no son una cuenta
confirmada.

**Y apareció uno que la auditoría no tenía, porque sólo se ve en píxeles**: la pantalla de Cursos se
dibujaba **debajo** de la tarjeta de bienvenida, con los dos títulos y las dos descripciones
superpuestos e ilegibles. Salió de fotografiar la aplicación al lado del prototipo, en la primera
pareja que nadie había mirado. Está
[cerrado y con su puerta](../evidence/stable/audit-courses-under-the-welcome-card.md), que ahora
cubre **todos** los destinos y no uno.
**El botón «Permisos» del aviso de acceso denegado se deja fuera, y es una decisión del
propietario.** El prototipo lo dibuja: abre los ajustes de Windows para ese recurso compartido. Aquí
eso significa **arrancar un proceso del sistema**, que vive en la capa del anfitrión y tiene sus
propias reglas de aislamiento — no es «un botón más» en una vista. La recomendación es **no
construirlo por ahora**: el aviso ya dice qué pasa y que la aplicación nunca cambia permisos por su
cuenta, que es la parte que evita que alguien espere de ella algo que no hace. Queda como alcance
nuevo, esperando un sí o un no.


**La paridad visual se vuelve a pasar.** `PRD-006` estaba `VERIFIED` sobre «las 53 vistas» y el árbol
tiene **61**; y de esas 53 sólo se fotografiaron **ocho pantallas** junto al prototipo. Además las
fichas por vista contra las que se compararía llegaron seis días después de darla por buena. Bajó a
`IMPLEMENTED` y sube cuando cubra las sesenta.

**El 2026-09-06 su criterio se corrigió a 61 y su comparación llegó a diecinueve parejas de las
cuarenta y dos pantallas del prototipo**, con **43 defectos medidos** y otros 65 candidatos cerrados
con veredicto escrito, en
[la vuelta cuatro](../evidence/stable/audit-prototype-fidelity-round-four.md). **Eso es lo que la
regla de publicación cuenta**: `PRD-006` es un compromiso, así que esos 43 se corrigen antes de
publicar nada. Faltan por comparar los estados que hay que fabricar y el reproductor entero.

**El propietario decidió el 2026-09-06 en qué orden se atacan: pantalla por pantalla**, empezando por
Biblioteca y escaneo, que concentra nueve y contenía el más serio de todos. El orden es Biblioteca y
escaneo, ficha de película, atajos de teclado, editor de metadatos, actualizaciones, índice de
Ajustes, subtítulos, copias, privacidad, cursos y detección de segmentos. **Su precio está medido**:
siete de los cuarenta y tres son rótulos compartidos repartidos por seis pantallas, y un solo barrido
los cerraría de golpe; yendo pantalla por pantalla se tocan al pasar por cada una, con la condición
de no dejar ninguno para una pasada de rótulos que ya no existe.

**El primero está cerrado**: la retirada de una carpeta borra su catálogo, tras una pregunta que
enumera lo que se pierde. Es la única de las cuarenta y tres que prometía algo falso sobre los datos
de quien usa el programa, y su decisión está en
[ADR-0011](../adr/0011-a-destructive-question-floats-and-blocks.md). Quedan **42**.

**El margen entre el riel y la primera tarjeta está cerrado desde el 2026-09-11, y no era una sola
cifra**, como decía este párrafo: el prototipo escribe un único relleno de página —28 bajo la barra,
32 a los lados, 48 abajo— para todos sus destinos, y la aplicación ponía 48 en cada uno; la portada
llevaba además los 8 px de su baldosa sin compensar, 56 en total. Ahora las páginas abren a 32 y la
portada en la línea del título, con la técnica del propio prototipo, en
[la evidencia del margen](../evidence/stable/audit-page-margin.md). **No era uno de los 42** —era el
defecto geométrico que dejó la vuelta cuatro—, así que la cuenta no se mueve.

**Medirlo a 1600 px destapó otro, cerrado el mismo día**: la rejilla no contaba el borde de 1 px de
cada tarjeta y a ese ancho metía una columna que no cabía, así que la última portada se comía 9 px del
margen derecho. **Y dejó registrado uno que faltaba: la rejilla fluida, cerrada también el
2026-09-11** en cuanto el propietario la puso la primera del orden de paridad. La aplicación usaba
una tarjeta fija de 148 que a 1600 cabía ocho veces, con unos 160 px libres a la derecha; ahora
cuenta las columnas con la regla del prototipo y reparte el ancho al píxel, como el navegador: nueve
portadas de 147 y 148 a 1600, ocho de 155 y 156 a 1500, y 33 px dentro de la página a cada lado,
contados en píxeles. Todo, en [la evidencia de la rejilla](../evidence/stable/audit-fluid-library-grid.md).
**No era uno de los 42**, así que la cuenta no se mueve.

**Y al medirla contra el prototipo quedaron siete con nombre, que según la regla del propietario del
mismo día —«todas las mejoras deben ser aplicadas cuanto antes o no habrá release»— van al orden
inmediato y no a «después»**: el ritmo vertical de la tarjeta, que se corrige de una vez —de la
última línea a la portada siguiente 18 contra 20, de la portada al título 8 contra 10, y las tres
líneas bajo la portada separadas 8 px donde el prototipo las apila—; cuatro diferencias de forma en la
tarjeta —la pista del progreso, el velo de «no disponible», el chip de tipo y la marca de visto—; la
densidad, que sólo coincide con el prototipo en la cómoda; los dos rieles de Inicio, que el prototipo
pinta como rejilla fluida de mínimo 132; el esqueleto de la primera carga; la decodificación de
portadas a 148, que ahora se dibujan hasta unos 187; y el desplazamiento que se mueve al
redimensionar. **Y uno que no es de paridad**: cambiar
de idioma borra la apariencia elegida hasta reiniciar, leído en el código y pendiente de reproducir.

**Y uno que tampoco estaba en la cuenta, señalado por el propietario el mismo día y cerrado**: las
veintisiete píldoras de opción dibujaban un botón de opción dentro, y en dieciocho era lo único que
decía cuál estaba elegida. El prototipo no lo dibuja en ninguna; ahora la elegida lo dice con su
borde, en [su evidencia](../evidence/stable/audit-option-pills-without-a-circle.md).

**Un aviso que describe un estado ocupa sitio; uno que narra un suceso flota**, y está escrito en
[ADR-0010](../adr/0010-a-state-takes-space-and-an-event-floats.md). Se decidió porque nadie lo había
decidido nunca: ni la franja de avisos ni el mensaje efímero estaban en el inventario de controles ni
en su lista de exclusiones, así que la misma pregunta podía contestarse de dos maneras. **Los avisos
del prototipo NO estaban rotos** —empujan 77 px a propósito—, y coincide con Microsoft, Material y
Carbon, y con lo que esta aplicación ya decidió en agosto para la banda de archivo suelto. De ahí
salen dos decisiones del propietario: **el aviso de disco desconectado va sólo en la Biblioteca**, no
persiguiendo por toda la aplicación; y **el escaneo se dibuja de dos maneras** — franja completa
cuando lo lanza una persona, marca discreta cuando arranca solo al abrir.

**Deshacer una decisión en la bandeja de revisión se aplaza, con su medición escrita.** Parecía
«añadir un botón» y no lo es: hoy hay **tres cerrojos** —el almacén rechaza devolver una ficha a
pendiente, la fila queda con el candado puesto, y no se guarda el estado anterior— y, además,
aceptar ya reescribió los metadatos del título sin copia de lo que había. El prototipo promete por
escrito «puedes cambiarla después», así que la promesa queda registrada y la decisión se toma con
ese número delante, no antes.

**Y dos defectos que dejó a la vista la retirada de carpetas, registrados el 2026-09-06 y sin
corregir.** El primero: tras retirar una carpeta, su vigilante sigue vivo hasta cerrar el programa,
porque el servicio que vigila sabe empezar con una carpeta y parar todas, pero no dejar de vigilar
una. No puede deshacer el borrado, y cada cambio en esa carpeta produce una excepción que se calla,
que es la clase de fallo que esta casa no deja pasar. El segundo: cada retirada hecha antes del
2026-09-06 dejó su catálogo en la base, marcado como disponible y sin carpeta que lo sostenga.
**Ese escombro sólo puede existir en bases de desarrollo**, porque no se ha publicado ninguna
versión —el repositorio no tiene releases ni etiquetas—, y eso es lo que decide si hace falta una
migración o basta con escribir por qué no.


## Las tres versiones

| Versión | Qué significa |
|---|---|
| `MVP` | Aplicación x64 instalable y útil para validar una colección real. Puerta aprobada el 2026-08-05. |
| `STABLE` | Primera publicación pública completa, incluida ARM64. Es donde estamos. |
| `POST_STABLE` | Mejoras que no bloquean la primera publicación estable. |

## Dónde estamos

El MVP cataloga, identifica, reproduce y recuerda dónde se quedó, en español y en inglés, sin cuenta
y sin enviar nada a ninguna parte. Se distribuye como MSIX x64 y como ZIP independiente, ambos con
hash publicado y compilación reproducible.

De los 46 compromisos del MVP: **44 verificados**, **1 fuera de alcance por decisión** y
**1 bloqueado** por hardware o entorno que este equipo no tiene. Ninguno queda informalmente
pendiente: cada bloqueo dice quién lo tiene y qué lo desbloquearía.
[release-readiness.md](../evidence/mvp/release-readiness.md) los detalla.

El Product Owner aprobó la puerta MVP el **2026-08-05** con ese bloqueo declarado. Aprobar la puerta
no lo resuelve: `PLY-004` sigue bloqueado con la misma condición, y los riesgos que el MVP deja
abiertos se heredan en `STABLE` en vez de cerrarse. Con la aprobación arranca la Parte B.

## Lo que viene: `STABLE`

| ID | Qué falta |
|---|---|
| `PRD-003` | Paridad ARM64. La compilación y el paquete nativo ya están hechos y verificados; falta correr las seis fases en una máquina ARM64. **Desde el 2026-09-04 ya no hace falta comprarla**: los runners `windows-11-arm` de GitHub son gratis en repositorios públicos, y ninguna de las seis fases pide hardware. Bloquea la publicación estable hasta medirlo. [T42](../evidence/stable/T42-arm64.md) |
| `REL-001` | Microsoft Store como distribución principal, con su certificación. Lleva dos deudas conocidas del MVP: justificar ante la Store la capacidad restringida `unvirtualizedResources` —sin ella el paquete borra la biblioteca al desinstalarse— y decidir cuándo firmar, porque el certificado comercial cambiará la identidad del paquete. |
| `REL-004` | Comprobación formal de marca, dominios y Store para el nombre público. |

De esa lista ya están hechos `REL-003` y `PLY-013`. El actualizador independiente comprueba, resume
en los dos idiomas, descarga a una carpeta aparte comprobando hash y tamaño, y no entrega nada a
Windows sin una confirmación que nombra la versión que estaba en pantalla; la Store mantiene su
propio canal. [T44](../evidence/stable/T44-updater.md) Y la detección automática de segmentos
compara localmente los episodios de cada serie, cumple cada umbral aprobado sobre un corpus
retenido y nunca pisa una marca manual ni una corrección humana.
[T43](../evidence/stable/T43-segment-detection.md)

## Lo que se hará después: `POST_STABLE`

| ID | Qué es |
|---|---|
| `UX-007` | Listas personalizadas. El modelo actual admite añadirlas sin migración destructiva. |
| `PLY-015` | Dolby Vision y passthrough Dolby/DTS. Requiere una evaluación técnica, legal y de demanda que no se ha hecho. |

## Lo que esta versión **no** hace

Esto no es una lista de tareas pendientes: son decisiones. Cambiarlas exige actualizar primero la
especificación y la matriz, en ambos idiomas.

- **No hay cuentas ni sesión remota.** Una persona, un PC. No hay registro, ni contraseña, ni perfil.
- **No hay sincronización entre equipos ni nube.** Lo que ve la aplicación está en su disco.
- **No reproduce varios vídeos a la vez.** Hay una sesión de reproducción, y una sola.
- **No es una plataforma de cursos**, y desde `ADR-0006` la frase está acotada, no borrada. Lo que
  sigue fuera es lo que la motivaba: no hay matrículas, ni certificados, ni cuestionarios, ni rachas,
  ni estadísticas de estudio, ni porcentaje de formación completada, ni nada que hable con una
  plataforma. Lo que entra (`CRS-001`…`CRS-005`) es lo que la aplicación ya hace con una serie:
  reconocer lo que hay en el disco, ordenarlo, reproducirlo en orden y recordar por dónde iba.
- **No gestiona vídeos más allá de catalogarlos.** No convierte, no recorta y no exporta vídeo. El
  renombrado seguro es la única operación que toca los archivos, y previsualiza antes de hacer nada.
- **No hay notas ni marcadores personales en la línea de tiempo** (`UX-008`). Las marcas de
  introducción y créditos existen (`PLY-012`), pero son de la serie, no un cuaderno personal.
- **No hay listas personalizadas todavía** (`UX-007`, pospuesto).
- **No hay Dolby Vision ni passthrough de audio** (`PLY-015`, fuera de alcance).
- **No hay macOS ni Linux.** El núcleo está desacoplado y no referencia APIs de Windows ni de
  Avalonia (`PRD-004`), de modo que portarlo sería posible; no está planificado.

## Cómo cambia esta hoja de ruta

Una funcionalidad sólo pasa a `VERIFIED` cuando su evidencia está enlazada en la matriz. Un cambio de
alcance —añadir algo de la lista de arriba, o quitar algo de la de abajo— se registra primero como
decisión en un [ADR](../adr) y después en la matriz, en español e inglés.

