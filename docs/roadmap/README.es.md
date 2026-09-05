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

**Lo que esta regla convierte en bloqueo de publicación, y conviene saberlo pronto:** `PRD-002` no
puede llegar a `VERIFIED` sin el **certificado comercial de firma**, porque su ciclo se verificó
sobre una copia resellada y el artefacto sin firmar no puede repetirlo — lo que lo encadena a
`REL-001`.

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
[la auditoría del 2026-09-04](../evidence/stable/audit-built-and-not-drawn.md). Las seis cerradas
son las portadas en la rejilla, la biblioteca que se cortaba en cincuenta títulos, la ficha de la
cuenta atrás que prometía ser configurable sin serlo —**cerrada de verdad el 2026-09-05**, con la
sección «Reproducción» de Ajustes—, los tres nombres del mini reproductor, las cadenas huérfanas,
que además ganaron [una puerta](../evidence/stable/audit-orphaned-strings.md) para que no vuelvan, y
**el escaneo que se podía cancelar por dentro y no por fuera**, cerrado esa misma tarde con
[la franja de avisos](../evidence/stable/audit-lib002-the-notices-strip.md). Las doce restantes van
en dos grupos: lo que sólo falta enseñar, y lo que el diseño tiene y la aplicación no.

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


**La paridad visual se vuelve a pasar.** `PRD-006` está `VERIFIED` sobre «las 53 vistas» y el árbol
tiene 60; y de esas 53 sólo se fotografiaron **ocho pantallas** junto al prototipo. Además las fichas
por vista contra las que se compararía llegaron seis días después de darla por buena. Baja a
`IMPLEMENTED` y sube cuando cubra las sesenta.

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

