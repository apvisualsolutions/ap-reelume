# C6 — Puerta de experiencia / Experience gate

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Tareas cubiertas / Tasks covered: T30–T35
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, PowerShell 7,
  Avalonia 12.1.1, LibVLCSharp 3.10.0, LibVLC 3.0.23.1, FlaUI UIA3 5.0.0, SQLite en WAL,
  Intel Core i7-14700K con 28 hilos, NVIDIA GeForce RTX 5070, dos ASUS ProArt PA279CRV a 2560×1440
  con escala 150 % y HDR activo

## Resultado por tarea / Per-task result

| Tarea / Task | Commit | Evidencia / Evidence | Estado / Status |
|---|---|---|---|
| T30 | `b6123ac` `feat: complete the hybrid home and title details` | [T30](T30-home-details.md) | superada / passed |
| T31 | `420d7a4` `feat: save local favorites watch later and ratings` | [T31](T31-personal-state.md) | superada / passed |
| T32 | `7c3bf9a` `feat: recommend titles locally with explanations` | [T32](T32-recommendations.md) | superada / passed |
| Marcas en la ficha de serie / Series-card marks | `10bd36f` `fix: put the same personal marks on the series card` | [T31](T31-personal-state.md) | corrección de alcance / scope fix |
| T33 | `83484c3` `fix: close MVP accessibility audit findings` | [T33](T33-accessibility.md), [informe firmado / signed report](accessibility-report.md) | superada / passed |
| T34 | `2162921` `feat: add opt-in tray and Windows startup` | [T34](T34-tray-startup.md) | superada con bloqueo declarado / passed with a declared block |
| T35 | `4a96aeb` `feat: open loose media without catalog import` | [T35](T35-open-with.md) | superada con bloqueo declarado / passed with a declared block |

## Condición 1 — La regresión visual está aprobada / Visual regression is approved

La baseline estructural de Inicio sigue siendo la aprobada en T30 y re-aprobada en T32: **36
combinaciones** —1366×768 y 4K, al 100/150/200 %, en claro, oscuro y alto contraste, en español e
inglés—. T33 cambió superficies de Inicio a propósito y la baseline **no necesitó regenerarse**: el
primer foco sigue siendo Continuar, el acceso a Biblioteca sigue dentro del primer viewport en las 36
y el orden de foco no varía. `HomeLayoutTests` pasa sin tocarla. / Home's approved structural
baseline is unchanged and still passes across all thirty-six combinations.

## Condición 2 — Cero defectos críticos y mayores de accesibilidad / Zero critical or major defects

La primera pasada de auditoría encontró **61 hallazgos: 14 críticos, 30 mayores y 17 menores**, en
nueve defectos con archivo propietario. Todos están cerrados.

```powershell
pwsh ./eng/run-accessibility.ps1 -Mode Verify -Passes 2
```

| Pasada / Pass | Pruebas / Tests | Critical | Major | Minor |
|---|---:|---:|---:|---:|
| 1 | 44 | 0 | 0 | 0 |
| 2 | 44 | 0 | 0 | 0 |

Ninguna severidad se rebajó y ningún chequeo se suprimió. La automatización cubre las **diecisiete
superficies** del recorrido canónico en los dos idiomas: árbol UIA con nombre, rol y estado; orden y
alcance del foco; ausencia de trampas; tokens de foco por tipo de control; contraste y ausencia de
color literal; escalado al 100, 150 y 200 %; reducción de movimiento; regiones activas; y los seis
controles de subtítulos. Sobre la aplicación real, el árbol que Windows publica tiene 45 elementos y
sus siete controles propios toman el foco cuando se les pide. / Two consecutive clean passes over
seventeen surfaces in both languages, plus the real UIA tree.

## Condición 3 — Cero tráfico en recomendaciones y datos personales / Zero traffic

| Prueba / Probe | Resultado / Result |
|---|---|
| Servidor señuelo mientras se calculan 200 recomendaciones (T32) | **0 solicitudes** |
| Muestreo de conexiones TCP durante la suite completa (C6) | **0 extremos remotos** en 25 muestreos |
| Diez minutos de bandeja activa e inactiva (T34) | **0 conexiones remotas** |
| Pila HTTP en `Domain` y `Application` | ninguna referencia a `HttpClient`, sockets, `WebRequest` ni resolución de nombres |
| Tipos de telemetría | ninguno en el código de producto |

`artifacts/test-results/C6/traffic.json` conserva el muestreo. / The canary, the sampled suite run,
and the ten idle minutes all come back at zero.

## Condición 4 — Bandeja e inicio desactivados por defecto / Tray and startup off by default

`LifecyclePreferences.Default` es `TrayEnabled=false`, `StartWithWindows=false`,
`CloseBehavior=Exit`, comprobado por prueba de dominio. El autoinicio sólo cambia tras un
consentimiento **dado**, y retirarlo nunca pide confirmación. Sobre la clave real
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`: la entrada no existe hasta que se activa, el
valor es la ruta del ejecutable entrecomillada, activar y desactivar dos veces es idempotente, una
entrada que apunta a otro sitio se informa `Invalid` y se repara, **el comando registrado arranca la
aplicación de verdad**, y al desactivar las ocho entradas ajenas de la clave quedan intactas. La clave
quedó limpia. / Everything is off until asked for, and the real Run key was exercised and left clean.

## Condición 5 — Una activación suelta deja la base sin cambios / Loose activation changes nothing

| Prueba / Probe | Resultado / Result |
|---|---|
| Integración: censo de **todas** las tablas antes y después | idéntico, incluidas una segunda activación y una fallida |
| Ruta con espacios, Unicode y más de 240 caracteres | abre, y el censo no cambia |
| Físico: cinco activaciones reales sobre la base del equipo, **32 tablas** censadas | **0 tablas cambiaron** |
| Físico: camino de «Abrir con…» resolviendo `"<ejecutable>" "%1"` desde el registro | arranca la aplicación; **0 tablas cambiaron**; el registro se retira |

La sesión suelta no llama a `PlaybackProgressTracker.BeginAsync`, que es lo único que autoriza al
rastreador a escribir, y su identificador se genera por activación, así que ni siquiera existe una
clave estable que pudiera convertirse en fila. / Nothing is written, and there is no stable key that
could become a row.

## Verificación transversal / Cross-cutting verification

| Comprobación / Check | Resultado / Result |
|---|---|
| `dotnet restore --locked-mode` | correcto / clean |
| `dotnet format --verify-no-changes` | sin cambios / no changes |
| `dotnet build -c Debug -warnaserror` | 0 advertencias, 0 errores |
| `dotnet build -c Release -warnaserror` | 0 advertencias, 0 errores |
| Suite completa `Release` / Full Release suite | **828 pruebas, 0 fallos, 0 omitidas** |
| `eng/verify.ps1 -Configuration Release -Runtime win-x64` | superada / passed |
| `eng/run-accessibility.ps1 -Mode Verify -Passes 2` | 0 críticos, 0 mayores, 0 menores |
| `eng/run-performance.ps1` | **9/9 métricas dentro de presupuesto** |
| `eng/verify-docs.ps1` | 51 Markdown, 6 localizados, 53 IDs, 46 MVP |
| `dotnet list package --vulnerable --include-transitive` | ningún paquete vulnerable / none |
| `dotnet list package --deprecated` | ningún paquete en desuso / none |
| Migraciones / Migrations | 14 aplicadas, 14 copias previas válidas, `integrity_check` en `ok` |

La suite pasó de **616** al cerrar I4 a **828** al cerrar I5. / The suite grew from 616 to 828.

### Presupuestos de rendimiento / Performance budgets

| Métrica / Metric | p95 | Presupuesto / Budget |
|---|---:|---:|
| Ventana útil / Useful window | 7,77 ms | 3000 ms |
| Primera página de búsqueda / First search page | 6,98 ms | 150 ms |
| Búsqueda concurrente / Concurrent search | 26,23 ms | 150 ms |
| Tiempo de fotograma / Frame time | 0,77 ms | 16,7 ms |
| Bloqueo de la UI por escaneo / Scan UI block | 9,57 ms | 50 ms |
| Sondeos de archivos sin cambios / Unchanged probes | 0 | 0 |
| Ordenar 10.000 recomendaciones / Ranking 10,000 | 30,38 ms | 200 ms |
| Caso de uso completo sobre 10.000 / Whole use case | 30,80 ms | 200 ms |
| Llamada desactivada / Disabled call | 0,15 ms | 1 ms |

## Cobertura por tarea / Per-task coverage

| Tarea / Task | Líneas del código nuevo / New-code lines |
|---|---:|
| T30 | 98,05 % |
| T31 | 98,47 % |
| T32 | 237/241 — 98,34 % |
| T33 | 17/17 — 100 % |
| T34 | 205/206 — 99,51 % |
| T35 | 95/97 — 97,94 % |

Ningún archivo nuevo de I5 baja del 91 % de líneas. / No new file in I5 falls below the bar.

## Estado de los identificadores de I5 / I5 identifier status

| ID | Estado / Status | Por qué / Why |
|---|---|---|
| `UX-001`, `UX-002`, `UX-003`, `UX-004` | `VERIFIED` | Inicio y fichas, con baseline aprobada y sin literales |
| `UX-005` | `IMPLEMENTED` | cierra con la exportación de T36 / closes with the T36 export |
| `UX-006` | `VERIFIED` | recomendaciones explicables, desactivables y sin red |
| `A11Y-001`, `A11Y-002` | `VERIFIED` | dos pasadas limpias y nueve defectos cerrados |
| `PLY-014` | `VERIFIED` | suma el recorrido completo sin ratón en diecisiete superficies |
| `SYS-001` | `IMPLEMENTED` | sin VM limpia para el reinicio de sesión |
| `SYS-002` | `IMPLEMENTED` | sin MSIX no hay asociación real de extensiones |
| `PLY-001`, `PRI-001`, `DAT-002` | `IN_PROGRESS` | cierran en I6 y I7 |
| `UX-007` | `DEFERRED` | listas personalizadas, `POST_STABLE`; no se toca |
| `UX-008` | `OUT_OF_SCOPE` | marcadores personales; no se toca |

## Hardware / Hardware

Los dos bloqueos declarados en C4 **siguen vigentes y sin cambios**: no hay GPU integrada activa
—sólo la NVIDIA GeForce RTX 5070— y ninguno de los cuatro endpoints de audio activos acepta 5.1 ni
7.1. Por eso `PLY-003` y `PLY-004` permanecen `IN_PROGRESS`. Nada se ha sustituido por una
simulación. I5 no introduce ninguna capacidad que dependa de hardware ausente. / The two C4 blocks
stand unchanged and nothing was simulated.

## Privacidad y límites / Privacy and boundaries

- **Sin telemetría**: ningún tipo de telemetría, análisis o seguimiento en el código de producto.
- **Sin tráfico no autorizado**: comprobado con servidor señuelo, muestreo de la suite completa y
  diez minutos de bandeja inactiva; los tres en cero.
- **Sin rutas privadas expuestas**: `git grep` sobre todo el árbol versionado no encuentra el nombre
  de usuario del sistema, el nombre del equipo ni ninguna ruta absoluta local.
- **Sin operaciones destructivas sobre medios**: los únicos `File.Move`/`File.Delete` del producto
  son el renombrado confirmado de T17, la escritura atómica de ajustes y la caché de arte
  regenerable. Ninguno toca un vídeo salvo el renombrado auditado.
- **Artefactos y medios ignorados**: `git ls-files` no devuelve ningún archivo bajo `artifacts/` ni
  ningún archivo multimedia; `eng/verify.ps1` falla si alguno apareciera, y no apareció.
- **Árbol limpio**: `git status` no informa nada pendiente.

## Saneamiento de privacidad / Privacy clean-up

El repositorio y su historial se publican, así que toda la deuda de privacidad conocida se cierra
aquí en lugar de esperar al cierre documental de T41. Los resultados no cambian; lo que desaparece es
el inventario personal.

| Archivo / File | Qué contenía / What it held | Qué contiene ahora / What it holds now |
|---|---|---|
| `C2-library-gate.md` | recuento exacto de archivos, bytes totales y dos términos de búsqueda reales | `N` para el recuento, usado igual en todas las filas; el volumen redactado; los términos descritos por su efecto |
| `T6-scan.md` | el mismo recuento en tres frases | «la biblioteca completa» |
| `Domain.Tests/Fixtures/media-name-cases.json` | el título de una serie real en cuatro casos | un título ficticio que conserva el patrón `Cap.NNN` que el caso ejercita |
| `IntegrationTests/Catalog/CatalogQueryTests.cs` | el mismo título en una ruta de ejemplo | el mismo título ficticio |

La coherencia que C2 demostraba sigue siendo comprobable: todo lo enumerado se sondea y todo lo
sondeado se indexa, con `N` en las tres filas. El tiempo medido, los presupuestos y los resultados de
la puerta quedan intactos. Las pruebas del analizador de nombres siguen ejercitando exactamente el
mismo patrón. / The consistency C2 proved is still checkable, and the parser cases still exercise the
same pattern.

Queda una constancia honesta: **el historial de Git anterior a este commit sigue conteniendo los
valores originales**, en tres commits de I1 e I2. La decisión está tomada y registrada en
[ADR-0002](../../adr/0002-publication-history-and-privacy-capture.md): **no se reescribe**. Reescribir
cambiaría el SHA de prácticamente los 46 commits de la rama y de `main`, y dejaría rotas las 28
referencias de commit que 26 archivos de documentación citan, a cambio de un beneficio que se obtiene
igual publicando un repositorio nuevo desde el árbol saneado. Ese es el mecanismo elegido y se ejecuta
en T41. / The decision is recorded in ADR-0002: the history is not rewritten, because the public
repository will be created fresh from the cleaned tree instead.

## Salvedades declaradas / Declared caveats

1. **Sin VM limpia de Windows para T34.** Verificado en este equipo que no hay hipervisor con
   Windows —no existe `Get-VM` y no hay VirtualBox, VMware ni QEMU— y no se cierra la sesión. El
   reinicio de sesión se sustituye por reiniciar el proceso y ejecutar de verdad el comando
   registrado. `SYS-001` queda `IMPLEMENTED`, no `VERIFIED`.
2. **Sin MSIX para T35.** Windows no ofrecerá la aplicación en «Abrir con…» hasta que el paquete de
   T40 declare `FileAssociations.xml`. Lo verificado es el comando que el shell ejecuta, con `%1`
   resuelto desde el registro. `SYS-002` queda `IMPLEMENTED`.
3. **`IMediaVersionGroupRepository` sigue siendo un puerto sin adaptador SQLite.** La ficha de
   película presenta un grupo completo y está probada con tres versiones, pero el host le pasa
   `null`. No pertenece a ninguna tarea de I5 y se declara en lugar de cerrarse a medias.
4. **Numeración de migraciones adaptada.** `personal_state` es la `0013` y `episode_media` la `0014`,
   porque los números que el plan citaba estaban ocupados. Nada se renumeró ni se sobrescribió; la
   siguiente libre es la `0015`.
5. **Los dos bloqueos de hardware de C4**, arriba: sin GPU integrada activa y sin endpoint de audio
   que acepte 5.1 o 7.1.
6. **Deuda de privacidad saneada por completo, antes de lo previsto.** La auditoría encontró, además
   de la ya conocida en `C2-library-gate.md` y `T6-scan.md`, el título de una serie real de la
   biblioteca del propietario en dos archivos de prueba versionados. Toda ella se ha saneado en este
   incremento en lugar de esperar a T41: el repositorio se publica con su historial y cada día que
   pasa el dato sigue ahí. Ver «Saneamiento de privacidad» más abajo. / All of it was cleaned here
   rather than deferred.
7. **Crecimiento de handles con la bandeja inactiva.** Durante los diez minutos de medición los
   handles del proceso pasaron de 892 a 966, unos siete por minuto, mientras la memoria de trabajo
   bajaba. No es criterio de esta puerta —que son CPU y tráfico, ambos con enorme holgura— y se
   registra con su cifra exacta en lugar de omitirse.
8. **El proyecto de integración pasa a `net10.0-windows10.0.22621.0`** para poder ejercitar los
   adaptadores de Windows que el plan sitúa en `IntegrationTests/Windows/`. Su archivo de bloqueo se
   regeneró en los mismos commits.

/ Eight caveats, all declared rather than papered over; three of them are new findings from this
session's audits.

## Resultado de la puerta / Gate result

**C6 se propone como superada.** Las cinco condiciones de la puerta I5 están demostradas con pruebas
reproducibles: la baseline estructural sigue aprobada en sus 36 combinaciones, la auditoría de
accesibilidad cierra con cero críticos y cero mayores en dos pasadas consecutivas, las
recomendaciones y los datos personales no producen una sola solicitud, la bandeja y el inicio con
Windows están desactivados por defecto y sólo se activan tras consentimiento reversible, y una
activación suelta deja las 32 tablas de la base exactamente como estaban.

La suite completa pasa sin fallos ni omisiones, los nueve presupuestos de rendimiento se cumplen, la
cobertura del código nuevo de las seis tareas supera el listón y ninguna capacidad ausente se ha
sustituido por una simulación ni se ha declarado como resultado superado.

La rama `codex/ap-reelume-mvp-x64` queda publicada, sin commits `wip:`, con `main` como antepasado
directo. **No se integra en `main`:** la aprobación corresponde al propietario antes de comenzar I6. /
C6 is proposed as passed with the eight declared caveats above; the branch stays published and
unmerged for the owner's review.
