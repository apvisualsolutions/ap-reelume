# C7 — Puerta de recuperación / Recovery gate

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Tareas cubiertas / Tasks covered: T36–T39
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, PowerShell 7,
  Avalonia 12.1.1, LibVLCSharp 3.10.0, LibVLC 3.0.23.1, FlaUI UIA3 5.0.0, SQLite en WAL,
  Intel Core i7-14700K con 28 hilos, NVIDIA GeForce RTX 5070, dos ASUS ProArt PA279CRV a 2560×1440
  con escala 150 % y HDR activo

## Resultado por tarea / Per-task result

| Tarea / Task | Commit | Evidencia / Evidence | Estado / Status |
|---|---|---|---|
| T36 | `8dea753` `feat: create rotating backups and safe exports` | [T36](T36-backup-export.md) | superada / passed |
| T37 | `b4aa28e` `feat: validate stage and remap library restores` | [T37](T37-disaster-recovery.md) | superada con bloqueo declarado / passed with a declared block |
| T38 | `5a6e0a5` `feat: enforce offline privacy and inspectable diagnostics` | [T38](T38-privacy.md) | superada / passed |
| T39 | `d5cdf79` `fix: pass recovery and concurrent workload matrix` | [T39](T39-recovery-load.md), [matriz / matrix](recovery-matrix.md) | superada con bloqueo declarado / passed with a declared block |

## Condición 1 — Export/import recupera los datos personales en rutas nuevas / Full restore into new paths

Una copia se exporta, se abre con herramientas ajenas al código —`Expand-Archive` y `Get-FileHash`—,
y se restaura en **tres clases de ruta distinta del mismo equipo**. En las tres vuelve todo y el
reescaneo no produce un solo duplicado:

| Destino / Destination | Marcas / Marks | Progreso / Progress | Marcadores | Preferencias y arte | Duplicados |
|---|---|---|---|---|---:|
| Carpeta nueva / New folder | favorito y 8 | 1260 s | 1 | sí | **0** |
| Unidad sustituida con `subst` | favorito y 8 | 1260 s | 1 | sí | **0** |
| UNC local `\\localhost\<recurso>\…` | favorito y 8 | 1260 s | 1 | sí | **0** |

El ZIP lleva exactamente cuatro cosas —base consistente, preferencias, arte personal y manifiesto— y
todos sus hashes cuadran. No lleva vídeos, caché descargada, diagnósticos ni credenciales.

## Condición 2 — Toda corrupción deja intacta la última base válida / Every corruption preserves the last valid database

| Prueba / Probe | Resultado / Result |
|---|---|
| Diez fallos inyectados en una restauración, uno por fase (T37) | la base activa idéntica **byte a byte** en los diez |
| Rotación con cinco copias dañadas más recientes (T36) | la única restaurable **sobrevive** |
| Base dañada (T39) | `AbortedSafely`; el archivo y las catorce copias previas, intactos |
| Migración fallida (T39) | `AbortedSafely`; esquema sin cambios y copia previa válida |
| Conflicto de renombrado (T39) | `AbortedSafely`; ningún archivo movido, ninguna fila de auditoría |

## Condición 3 — La captura de privacidad sólo contiene lo solicitado / The capture holds only what was asked for

Recorrido de **treinta minutos** sobre la aplicación real, con la captura que fija
[ADR-0002](../../adr/0002-publication-history-and-privacy-capture.md): sin proxy, sin certificado y
sin elevación.

| Prueba / Probe | Resultado / Result |
|---|---|
| Muestras de conexiones TCP por proceso / Per-process TCP samples | **177** |
| Muestras con algún extremo remoto / Samples with a remote endpoint | **0** |
| Canarios en el informe exportado / Canaries in the exported report | **0** de diez categorías |
| Claves del informe / Report keys | las nueve permitidas y ninguna más |
| Archivos de registro en la carpeta de datos / Log files in the data folder | **0** |
| Clientes HTTP sin propósito declarado / HTTP clients without a declared purpose | **0** |
| Almacén propio de credenciales / Application-owned credential store | ninguno / none |

El alcance se declara y no se infla: esto observa **el proceso .NET**, no el equipo.

## Condición 4 — La matriz pasa dos veces / The matrix passes twice

```powershell
pwsh ./eng/run-recovery.ps1 -Mode Verify -Passes 2
```

Nueve fallos, nueve filas, **dos pasadas idénticas**, cada una con uno de los cuatro resultados
permitidos y ninguna con un éxito falso. Antes de creerle al arnés se demuestra que **detecta** una
corrupción sembrada: la misma comprobación dice `ok` sobre la base sana y falla sobre la dañada.

| Fallo / Failure | Resultado / Outcome |
|---|---|
| Cierre inesperado / Unexpected shutdown | `Recoverable` |
| USB/NAS desconectado / USB or NAS disconnected | `Degraded` |
| Acceso denegado / Access denied | `Degraded` |
| TMDB caído o limitado / TMDB down or rate limited | `Degraded` |
| Archivo corrupto / Corrupt file | `Degraded` |
| Motor multimedia falla / Media engine fails | `Degraded` |
| Base dañada / Damaged database | `AbortedSafely` |
| Migración falla / Migration fails | `AbortedSafely` |
| Conflicto de renombrado / Rename conflict | `AbortedSafely` |

## Verificación transversal / Cross-cutting verification

| Comprobación / Check | Resultado / Result |
|---|---|
| `dotnet restore --locked-mode` | correcto / clean |
| `dotnet format --verify-no-changes` | sin cambios / no changes |
| `dotnet build -c Debug -warnaserror` | 0 advertencias, 0 errores |
| `dotnet build -c Release -warnaserror` | 0 advertencias, 0 errores |
| Suite completa `Release` / Full Release suite | **1064 pruebas, 0 fallos, 0 omitidas** |
| `eng/verify.ps1 -Configuration Release -Runtime win-x64` | superada / passed |
| `eng/run-accessibility.ps1 -Mode Verify -Passes 2` | 0 críticos, 0 mayores, 0 menores |
| `eng/run-performance.ps1` | **12/12 métricas dentro de presupuesto** |
| `eng/run-recovery.ps1 -Mode Verify -Passes 2` | 9 filas por pasada, las dos iguales |
| `eng/verify-docs.ps1` | 60 Markdown, 8 localizados, 53 IDs, 46 MVP |
| Auditoría de dependencias / Dependency audit | **0 paquetes vulnerables** en los trece proyectos |
| Medios y artefactos generados / Generated media and artifacts | siguen ignorados / still ignored |
| Datos personales en el árbol versionado / Personal data in the tracked tree | **ninguno / none** |

La suite pasó de **828** al cerrar I5 a **1064**. / The suite grew from 828 at the end of I5 to 1064.

## Cobertura del código nuevo / New-code coverage

| Tarea / Task | Total | Archivo más bajo / Lowest file |
|---|---:|---|
| T36 | 510/512 — **99,61 %** | `SqliteBackupService.cs` — 96,00 % |
| T37 | 582/591 — **98,48 %** | `RootRemapRowViewModel.cs` — 96,67 % |
| T38 | 245/246 — **99,59 %** | `DiagnosticsContracts.cs` — 97,22 % |
| T39 | sin código de producción / no production code | — |

Ningún archivo nuevo baja del 96 %, por encima del listón del 91 % que fijó I5.

## Lo que la verificación física encontró / What physical verification found

Tres defectos que las pruebas sin cabeza no podían ver, los tres corregidos y fijados por pruebas:

1. **Ningún comando avisaba a su superficie** cuando su respuesta cambiaba, así que «Restaurar ahora»
   no podía pulsarse nunca y «Cancelar» no llegaba a habilitarse. Tres pruebas cuentan ahora las
   notificaciones del evento, no sólo el valor devuelto.
2. **Reunir los datos de la máquina podía tumbar la pantalla** si un proveedor no respondía.
3. **Una exportación que falla parecía un éxito**: no decía nada.

Y una fuga de privacidad en una prueba propia —el nombre de usuario y el del equipo escritos como
datos de ejemplo— que la auditoría previa al commit atrapó antes de confirmarla.

## Hallazgo que I6 no buscaba y hay que mirar antes de I7 / A finding I6 was not looking for

El recorrido de la aplicación real que exigía T38 enseñó algo que ninguna prueba había señalado: **la
aplicación expone bastante menos de lo que tiene construido**. Catorce superficies con pruebas y
evidencia propia **no son alcanzables** desde el shell —añadir una carpeta, la bandeja de revisión,
duplicados, el editor de metadatos, el renombrado, el reproductor, pistas, subtítulos, salida de
audio, marcadores, reanudar, atajos, ajustes de escaneo y los créditos de TMDB—, y la composición no
registra el proveedor de metadatos.

No es una regresión de I6: cada componente existe y funciona. Falta el ensamblaje, y ninguna prueba lo
comprobaba porque todas **construyen** la superficie que van a examinar en lugar de pedírsela a la
aplicación.

Está medido, decidido y planificado en
[ADR-0003](../../adr/0003-assemble-the-application-before-packaging-it.md): I7 empieza por una tarea
**T39B** de ensamblaje, con una prueba de alcanzabilidad que convierte una vista huérfana en un fallo
de la suite. Ningún identificador se degrada por ello; la diferencia se cobra en T41, donde la puerta
MVP exige que cada compromiso sea alcanzable en el artefacto publicado. / Measured, decided, and
planned in ADR-0003: I7 starts with an assembly task before packaging anything.

## Salvedades declaradas / Declared caveats

1. **No hay VM limpia de Windows**, comprobado en este equipo. La restauración se verificó en tres
   clases de ruta distinta del mismo equipo. El bloqueo se declara y **no se convierte en PASS**.
2. **No hay elevación de administrador**, así que la captura de paquetes con `pktmon` no se ejecuta.
   ADR-0002 decidió las cuatro piezas que la sustituyen y su alcance está declarado.
3. **`IMediaVersionGroupRepository` sigue siendo un puerto sin adaptador SQLite**, como se declaró en
   C6. No pertenece a T36–T39.
4. **La numeración de migraciones no cambió**: I6 no añadió ninguna. `0001`–`0014` siguen ocupadas y
   `0015` sigue libre.
5. **Los dos bloqueos de hardware de C4 siguen abiertos**: no hay GPU integrada activa y ningún
   endpoint de audio acepta 5.1 ni 7.1. `PLY-003` y `PLY-004` siguen `IN_PROGRESS` por eso.
   **`PLY-001` sigue `IN_PROGRESS` por una razón distinta y mejor documentada**: su criterio dice que
   el reproductor integrado es el predeterminado, y hoy el reproductor no se puede abrir desde la
   aplicación. Se cierra en T39B, no antes.
6. **`CompositionRoot` no cablea el proveedor TMDB**, así que el shell real no abre ninguna conexión.
   El tramo «TMDB consentido» de T38 se observa en el arnés que sí lo construye. Integrar la
   identificación en el shell no pertenece a I6.
7. **Un episodio no reproducible**: durante T38 la aplicación se cerró sola tres veces con código 0.
   Con el binario que se entrega no volvió a ocurrir en 600 segundos ni en los treinta minutos del
   recorrido. Se declara no reproducible, no resuelto, y `TrayIdleTests` falla ahora si el proceso no
   llega vivo al final de su ventana.

## Resultado / Result

**C7 superada.** Export/import recupera íntegramente los datos personales en rutas nuevas, toda
corrupción deja intacta la última base válida, la captura de privacidad no contiene una sola conexión
no solicitada, y la matriz de recuperación y concurrencia pasa dos veces sin discrepancias. La rama
queda publicada y **no se integra en `main`**: I6 se detiene aquí para revisión. / C7 passes; the
branch stays published and unmerged pending review.
