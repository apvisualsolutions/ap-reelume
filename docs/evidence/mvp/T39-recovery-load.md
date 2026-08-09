# T39 — Recuperación y carga simultánea / Recovery and Concurrent Load

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `5a6e0a5`
- Commit de tarea / Task commit: `fix: pass recovery and concurrent workload matrix`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1,
  SQLite en WAL, Intel Core i7-14700K, NVIDIA GeForce RTX 5070
- IDs: `SYS-001` → `VERIFIED`; `LIB-002`, `LIB-010`, `PLY-008` y `DAT-001` suman evidencia;
  `PLY-001` sigue `IN_PROGRESS` por los bloqueos de hardware de C4

## RED y GREEN / RED and GREEN

El ciclo rojo de esta tarea no es una prueba que falla contra código ausente: es que **el arnés entero
no existía**. `artifacts/test-results/T39/red/missing-harness.log`, tomado contra `b4aa28e`, registra
que no había `eng/run-recovery.ps1`, ni ninguna de las cinco suites de recuperación, ni las tres de
carga, ni la matriz, y que el proyecto de rendimiento ni siquiera podía referenciar la bandeja que
tiene que medir. / The red cycle here is the absence of the harness itself, recorded against the tree
this task starts from.

Escribir las suites primero encontró tres cosas que la prueba había supuesto mal y el producto hacía
bien, lo cual es exactamente para lo que sirve escribirlas primero:

1. Una raíz inalcanzable **no** produce cero elementos enumerados: produce **un elemento fallido**
   cuya ruta es la raíz, y con él se marca la raíz como no disponible.
2. Esa marca cae sobre las **filas de archivos**, no sobre la fila de la raíz: el catálogo conserva su
   entrada y la señala inalcanzable, que es lo que permite recuperarla al reconectar.
3. Dos suites que lanzan hosts de prueba hijos **no pueden correr a la vez**: cada hijo es un host
   completo y el segundo agota tiempos que sobran para el primero. Se resolvió poniéndolas en una
   colección no paralela, no relajando el tiempo límite.

## La matriz / The matrix

`pwsh ./eng/run-recovery.ps1 -Mode Verify -Passes 2` ejecuta los nueve fallos que la especificación
nombra y compone [`recovery-matrix.md`](recovery-matrix.md) a partir de lo que la ejecución produjo,
no de una tabla escrita a mano. Cada fila declara uno de cuatro resultados —`Continued`, `Degraded`,
`Recoverable`, `AbortedSafely`— y **no existe una palabra para «funcionó»**: un caso de recuperación
que informa éxito es un caso que no llegó a fallar, y eso es un defecto del caso.

## El arnés se demuestra antes de creerle / The harness is proved before it is trusted

Una comprobación que siempre dice «ok» pasaría esta suite eternamente sin haber mirado nunca. Por eso
la corrupción sembrada se demuestra **detectable** antes de usarla: la misma comprobación dice `ok`
sobre la base sana y falla sobre la base dañada, en la misma prueba.

## Carga simultánea / Concurrent load

`pwsh ./eng/run-performance.ps1` mide **doce** métricas, las nueve de C6 y tres nuevas. Todas dentro
de presupuesto:

| Métrica / Metric | p95 | Presupuesto / Budget | |
|---|---:|---:|---|
| `playback-beat-during-scan` | 31,16 ms | 250 ms | ✔ |
| `scan-ui-block` | 3,45 ms | 50 ms | ✔ |
| `slow-nas-ui-block` | 0,01 ms | 50 ms | ✔ |
| `tray-idle-handles` | 8,98 handles/min | 30 handles/min | ✔ |
| `useful-window` | 4,61 ms | 3000 ms | ✔ |
| `first-search-page` | 3,66 ms | 150 ms | ✔ |
| `concurrent-search` | 12,53 ms | 150 ms | ✔ |
| `frame-p95` | 0,83 ms | 16,7 ms | ✔ |
| `recommendation-rank-10k` | 21,60 ms | 200 ms | ✔ |
| `recommendation-use-case-10k` | 23,63 ms | 200 ms | ✔ |
| `recommendation-disabled` | 0,10 ms | 1 ms | ✔ |
| `unchanged-probes` | 0 | 0 | ✔ |

Además, el escaneo de un recurso lento mantuvo **una sola enumeración por raíz**: un recurso que
responde despacio se castiga a sí mismo si se le piden varias cosas a la vez.

El «latido» es un lazo periódico en su propio hilo, porque eso es exactamente lo que es la devolución
de posición de un decodificador: algo que hay que atender a tiempo. Un hueco por encima del
presupuesto es lo que un espectador vería como un salto. / The beat is what a decoder's position
callback actually is.

El «latido» es un lazo periódico en su propio hilo, porque eso es exactamente lo que es la devolución
de posición de un decodificador: algo que hay que atender a tiempo. Un hueco en ese lazo por encima
del presupuesto es lo que un espectador vería como un salto. / The beat is what a decoder's position
callback actually is: something that must be serviced on time.

## Bandeja inactiva / Idle tray

C6 dejó una observación abierta: con la bandeja activa e inactiva los handles del proceso crecían unos
siete por minuto y no se pudo atribuir. `TrayIdleTests` mide **la bandeja sola**, en un proceso hijo,
con la bandeja visible y con la bandeja oculta, y comprueba dos cosas: cuántos handles gana por minuto
y si el proceso sigue vivo al final de la ventana.

| Fase / Phase | Handles por minuto / Handles per minute | ¿Sobrevivió? / Survived? |
|---|---:|---|
| Bandeja visible / Tray visible | **8,98** | sí / yes |
| Bandeja oculta / Tray hidden | dentro del mismo presupuesto / within the same budget | sí / yes |

La observación queda **acotada, no cerrada**: el crecimiento existe, es del mismo orden que el que C6
midió, y está por debajo de un presupuesto que una fuga real superaría con holgura. Lo que la
observación no puede seguir diciendo es que nadie la esté mirando: ahora falla una prueba si crece. /
The observation is bounded rather than closed, and a test now fails if it grows.

## Un episodio que no se reprodujo / An episode that did not reproduce

Durante la verificación física de T38 la aplicación se cerró sola tres veces —a los 144, 182 y 255
segundos— siempre con **código de salida 0**, es decir, un apagado ordenado y no un fallo. Se bisecó
por commits, se instrumentó el arranque y se observó en reposo repetidamente. Con el binario final no
volvió a ocurrir: **600 segundos y después treinta minutos completos** sin cerrarse, esta última vez
durante el recorrido de privacidad de T38.

No se declara resuelto porque no se encontró la causa. Se declara **no reproducible con el binario que
se entrega**, y se deja cubierto por `TrayIdleTests`, que ahora falla si el proceso no llega vivo al
final de su ventana. / It is recorded as not reproducible rather than as fixed, and a test now watches
for it.

## Verificación transversal / Cross-cutting verification

| Comprobación / Check | Resultado / Result |
|---|---|
| Suite completa `Release` / Full Release suite | **1064 pruebas, 0 fallos, 0 omitidas** |
| `dotnet format --verify-no-changes` | sin cambios / no changes |
| Debug y Release con `-warnaserror` | 0 advertencias, 0 errores |
| `eng/run-recovery.ps1 -Mode Verify -Passes 2` | 9 filas por pasada, las dos idénticas |
| `eng/run-performance.ps1` | **12/12 métricas dentro de presupuesto** |
| `eng/run-accessibility.ps1 -Mode Verify -Passes 2` | 0 críticos, 0 mayores, 0 menores |
| `eng/verify-docs.ps1` | 59 Markdown, 8 localizados, 53 IDs, 46 MVP |

La suite pasó de **1047** a **1064**. / The suite grew from 1047 to 1064.

## Cobertura / Coverage

Esta tarea **no añade código de producción**: son ocho suites, un arnés y una matriz. El listón de
cobertura de código nuevo no aplica porque no hay código nuevo que cubrir; lo que aporta es cobertura
adicional sobre código que ya existía —el corredor de migraciones, el comprobador de integridad, el
coordinador de escaneo, el rastreador de progreso, el renombrador seguro y la bandeja— ejercitado
esta vez por su modo de fallo en lugar de por su camino feliz. / This task adds no production code; it
adds failure-path coverage over code that already existed.

## Salvedades declaradas / Declared caveats

1. **`PLY-001` sigue `IN_PROGRESS`.** Los dos bloqueos de hardware de C4 —no hay GPU integrada activa
   y ningún endpoint de audio acepta 5.1 o 7.1— no se resuelven aquí y no se convierten en PASS.
2. **El fallo de proveedor de metadatos se comprueba sin red.** Lo que la matriz afirma es que una
   caída del proveedor no cuesta nada de lo que la biblioteca posee, y la biblioteca es local.
3. **El proyecto de rendimiento pasó a `net10.0-windows10.0.22621.0`** y referencia el proyecto
   `Windows`, porque la bandeja que tiene que medir sólo existe allí. Es el mismo movimiento que la
   suite de integración hizo en I5, y su `packages.lock.json` está regenerado.
4. **El acceso denegado se simula bloqueando un archivo**, no retirando permisos NTFS: cambiar una ACL
   es una modificación de la configuración de seguridad del equipo, y una tarea de recuperación no
   debería introducirla. Lo que se demuestra es lo que importa: un elemento que no se puede abrir no
   detiene el escaneo.
