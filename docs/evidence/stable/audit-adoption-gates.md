# Las puertas de la adopción, auditadas con mutantes

**Fecha:** 2026-09-19. **Plugin:** `ap-smart-tech` 0.8.1, comprobado por efecto con
`claude -p "/ap-smart-tech:ping"` (`pong — ap-smart-tech 0.8.1`) y `adopt-doctor.ps1` en 0, «sin
deriva». **Tareas:** `ENG-038`, y `ENG-039` abierta.

El agente `gate-auditor` revisó en una copia aislada las tres piezas nuevas de la adopción del
sistema común (`audit-adopt-common-system.md`): `HandoffLimitsTests`, la batería del acta y
`gate-probe.ps1`. Midió con mutantes la primera; en las otras dos su aislamiento no le dejó ejecutar
`pwsh`, así que sus hallazgos eran de lectura. **Aquí se midieron todos**: cada mutante se pasó
contra la batería de `HEAD` (tiene que sobrevivir, o el hallazgo no era real) y contra la nueva
(tiene que morir).

## ENG-038: el recibo del acta, y un segundo defecto que la tarea no nombraba

`Escribir-Recibo` formaba la lista de saltos con un `if` usado como valor. En PowerShell eso
desenrolla la salida: una lista vacía sale como `null` y **una lista de uno sale como el elemento
suelto**. Lo primero es `ENG-038`; lo segundo apareció al escribir la prueba, con el caso de dos
saltos como control —ése sí sobrevivía—. Rojo antes del arreglo: 26 de 27 casos.

**La batería tenía la misma trampa al leer el recibo**, y dio rojo con el guion ya arreglado. Se
corrigió leyendo fuera del `if`. Un recibo viejo que ya trae `null`, o un `null` delante de un
salto, queda limpio.

| Mutante sobre `cierre-acta.ps1` | Batería de `HEAD` | Batería nueva |
| --- | --- | --- |
| la línea original (`ENG-038`) | verde | 28 rojos |
| `if` como valor sin `@()` | verde | 28 rojos |
| sin descartar los nulos | — | 2 rojos |
| perder los saltos previos | — | 3 rojos |
| `[object[]]@()` en vez de `@()` | — | verde: **equivalente**, el forzado sobraba y se quitó |

## Los hallazgos del auditor sobre el acta: diez mutantes

Todos **sobreviven** a la batería de `HEAD` y **mueren** con la nueva, medido con
`test-cierre-acta.ps1 -Script <copia>`:

| Hallazgo | Mutante | Lo que lo mata |
| --- | --- | --- |
| A1 | el contador de bloqueos a 0 | el recibo conserva `blocks` del anterior |
| A1 | la fase del recibo dice «completa» con pasos en falta | la fase es la primera fila marcada |
| A2 | dos filas de efectos con las etiquetas cruzadas | lo esperado se busca en la fila `!!`, no en cualquier línea |
| A3 | formato siempre en verde | caso sin `dotnet format` |
| A3 | `build` sin `-warnaserror` cuenta | caso con un build no estricto |
| A3 | pendientes siempre en verde | caso sin `list-pending.ps1` |
| A4 | no quitar lo que va tras `#` | lo citado en un comentario tras otro `-File` |
| A5 | sin mirar `is_error` | fallo marcado solo con `is_error` |
| A5 | sin mirar el texto del resultado | fallo solo en el texto, y en una lista de bloques |
| menor | sin `diff --cached` | el relevo preparado y sin commitear cuenta |

La batería pasa de 25 a 37 casos, todos en verde con el guion arreglado.

## El tope del relevo: dos mutantes medidos por el auditor

| Hallazgo | Mutante en `eng/check-handoff.ps1` | Prueba nueva que lo mata |
| --- | --- | --- |
| H1 | no comparar las fechas de los dos idiomas | `A_handover_updated_in_one_language_only_sounds_by_its_date` |
| H2 | contar caracteres en vez de bytes | `The_size_limit_counts_bytes_not_characters`: 3.100 letras acentuadas son 6.200 bytes |

`HandoffLimitsTests`: 9 de 9 en verde; con cada mutante, 1 rojo, el suyo.

## La privacidad: la sonda no ejecutaba el guion del cierre

G1: la puerta «privacidad» del manifiesto llamaba al filtro común directamente, y **ninguna prueba
ejecutaba `cierre-privacidad.ps1`**. Vaciar el relevo que ese guion pasa al filtro —el camino del
incidente de `ENG-032`— no lo veía nadie. Ahora la sonda monta un repositorio con el relevo sucio y
corre el guion real: suena 1 y calla 0, y con ese mutante sale **0 donde debía salir 1**.

Y el guion tiene su propia batería, `test-cierre-privacidad.ps1`: una fuga plantada en **uno** de los
seis puntos de paso por caso, y la prueba exige que suene ese punto y ningún otro. Once casos, y
nueve mutantes que mueren cada uno por el suyo: relevo vacío, mensaje vacío, sin el stage, sin
detectar copias, árbol vacío, prompt ignorado, nota ignorada, relevo ausente leído como limpio, y el
filtro fuera del modo público (siete rojos).

Las doce puertas del manifiesto, provocadas con `gate-probe.ps1` en sus dos casos: suenan 1 y callan
0 las doce. El manifiesto no se tocó, así que su huella sigue siendo la instalada.

## Lo que queda abierto

**`ENG-039`**: nada corre las dos baterías de los guiones del cierre por su cuenta. La orden de
baterías del manifiesto no la ejecuta ninguna herramienta del sistema común, y en CI no está la
carpeta de herramientas compartidas.
