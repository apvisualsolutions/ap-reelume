# T34 — Bandeja e inicio con Windows opt-in / Opt-in tray and Windows startup

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `83484c3`
- Commit de tarea / Task commit: `feat: add opt-in tray and Windows startup`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1,
  NVIDIA GeForce RTX 5070, dos ASUS ProArt PA279CRV a 2560×1440 con escala 150 %
- IDs: `SYS-001=IMPLEMENTED` con el bloqueo declarado / with the declared block;
  `PRI-001=IN_PROGRESS`, que suma evidencia y cierra con la auditoría completa / gains evidence and
  closes with the full audit

## RED y GREEN / RED and GREEN

`AppLifecyclePolicyTests`, `TrayLifecycleTests`, `WindowsStartupTests` y `LifecycleSettingsTests` se
escribieron antes que el dominio, los puertos, los adaptadores y la superficie. RED falló en
compilación porque no existían `LifecyclePreferences`, `CloseBehavior`, `CloseDecision`,
`AppLifecyclePolicy`, `ITrayService`, `IStartupService`, `StartupEntryState`, `ILifecycleSettings`,
`CloseApplication`, `WindowsTrayService`, `WindowsStartupService`, `StoredLifecycleSettings`,
`LifecycleSettingsViewModel` ni `LifecycleSettingsView`. La salida está en
`artifacts/test-results/T34/red/build.log`. / The four suites were written first and RED failed on
every missing type.

El ViewModel que crea esta tarea tiene prueba desde el ciclo RED. / The one view model this task
creates is covered from RED.

GREEN ejecuta **807 pruebas con 0 fallos y 0 omitidas** en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T34/green/`. `dotnet format --verify-no-changes` no informa cambios y
ambas compilaciones terminan con 0 advertencias. La suite pasó de **768** a **807**. / GREEN runs 807
tests with no failures and no skips; the suite grew by 39.

Revisando el cableado del host apareció un defecto real que las pruebas no cubrían todavía: **salir
desde el menú de la bandeja no escribía el progreso**, porque llamaba a apagar la aplicación
directamente. Ahora toma exactamente el mismo camino que cerrar la ventana, con su prueba. / A real
defect surfaced while reviewing the host wiring — exiting from the tray menu skipped the progress
write — and it now takes the same path as closing the window.

## Todo desactivado hasta que alguien lo pida / Everything off until someone asks

| Preferencia / Preference | Valor inicial / Initial value |
|---|---|
| `TrayEnabled` | `false` |
| `StartWithWindows` | `false` |
| `CloseBehavior` | `Exit` |

Las reglas son puras y viven en `AppLifecyclePolicy`:

- Cerrar a la bandeja **sólo** es posible mientras la bandeja existe; pedirlo sin bandeja no cambia
  nada.
- Apagar la bandeja devuelve el botón de cerrar a su significado normal, en el mismo acto.
- Activar el inicio con Windows exige consentimiento **dado**; retirarlo nunca lo exige, porque
  quitar un permiso no puede ser más difícil que darlo.
- Activar o desactivar dos veces deja exactamente lo mismo que hacerlo una.
- Un estado guardado que se contradice —cerrar a una bandeja que no existe— se **repara** al leerlo,
  no se obedece.

/ Every rule is a pure decision and the adapters only carry it out.

## El orden de cierre no es negociable / The closing order is fixed

`CloseApplication` escribe la posición **antes que nada**. Sólo después detiene la sesión, y sólo
después oculta la ventana o sale. Lo comprueba una prueba que registra la secuencia real:

| Situación / Situation | Secuencia observada / Observed sequence |
|---|---|
| Sin bandeja, reproduciendo | `progress` → `stop-playback` → `exit` |
| A la bandeja, reproduciendo | `progress` → `hide-to-tray` (la sesión sigue) |
| Sin bandeja, sin reproducir | `progress` → `exit` |
| Salir desde el menú de la bandeja | `progress` → `stop-playback` → `exit`, y el icono desaparece |

En el host, el cierre de la ventana se **cancela primero** y se completa después de que la escritura
termine: un manejador de cierre síncrono no puede esperar a un disco. / The window close is cancelled
first and completed after the write, because a synchronous close handler cannot wait for a disk.

## El registro real, comprobado a mano / The real registry, checked by hand

Sobre `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, con el servicio real y no con una copia.
La ruta del ejecutable se omite aquí a propósito; lo que importa es la forma del valor.

| Comprobación / Check | Resultado / Result |
|---|---|
| La clave real está intacta antes de empezar | sí / yes |
| Inspeccionar no crea nada | `Absent`, y sigue ausente |
| Activar escribe la ruta del ejecutable **entrecomillada** | coincide con `ExpectedCommand`, empieza y acaba en `"` |
| Activar dos veces | un solo valor, idéntico |
| **El comando registrado arranca la aplicación** | sí: el proceso aparece con la ventana `AP Reelume` |
| Una entrada que apunta a otro sitio | se informa `Invalid` |
| Reparar | la reescribe a `Present`; repetir la reparación no hace nada |
| Desactivar dos veces | el valor desaparece y no vuelve |
| Daño colateral | las **8** entradas ajenas de la clave siguen exactamente igual |
| Estado final de la clave real | sin ninguna entrada de AP Reelume |

El nombre del valor es `APSolutions.LocalMedia`, la identidad de paquete estable, nunca el nombre
público. La suite automatizada escribe en una clave propia, `HKCU\Software\APSolutions\LocalMedia\Tests\Run`,
porque una batería de pruebas no debe dejar nada en la clave que Windows lee al iniciar sesión. /
The automated suite uses a key of its own; the real key was exercised by hand and left clean.

## La bandeja inactiva no hace nada / An idle tray does nothing

**603,7 segundos** con la bandeja activada, midiendo el proceso real en un equipo de 28 hilos y
muestreando cada minuto:

| Medida / Measurement | Resultado / Result | Presupuesto / Budget |
|---|---:|---:|
| CPU media / Average CPU | **0,0045 %** | <1 % |
| CPU acumulada / Accumulated CPU | 0,766 s en 603,7 s | — |
| Conexiones TCP remotas / Remote TCP connections | **0** | 0 |

Los diez muestreos por minuto están en `artifacts/test-results/T34/green/tray-idle.json`. La media
más alta de cualquier minuto fue **0,0045 %**, tres órdenes de magnitud por debajo del presupuesto, y
ninguna muestra encontró una sola conexión fuera de `127.0.0.1`. / Ten per-minute samples, a peak
average three orders of magnitude under budget, and not one connection outside loopback.

**Observación registrada, no un fallo de la puerta:** los handles del proceso pasaron de **892 a 966**
en esos diez minutos, unos siete por minuto sin que nadie tocara nada. La memoria de trabajo no creció
—bajó de 184 a 174 MB—. El criterio de esta puerta es CPU y tráfico, y los dos se cumplen con
holgura; el crecimiento de handles se anota aquí con su cifra exacta en lugar de omitirse, y se
traslada a la puerta como observación. / A recorded observation rather than a gate failure: handles
grew from 892 to 966 while the working set fell, and the number is stated rather than dropped.

El adaptador no programa nada: crea el icono una vez, lo hace visible cuando se le pide y sólo emite
un evento si alguien activa una de sus dos entradas. No hay temporizador, ni sondeo, ni trabajo de
fondo. / The adapter schedules nothing.

## Cobertura / Coverage

| Archivo / File | Líneas / Lines |
|---|---:|
| `Domain/Lifecycle/AppLifecyclePolicy.cs` | 29/29 — 100 % |
| `Application/Lifecycle/CloseApplication.cs` | 29/29 — 100 % |
| `Windows/Startup/WindowsStartupService.cs` | 26/26 — 100 % |
| `Windows/Tray/WindowsTrayService.cs` | 41/42 — 97,62 % |
| `Infrastructure/Settings/StoredLifecycleSettings.cs` | 14/14 — 100 % |
| `Presentation/Settings/LifecycleSettingsViewModel.cs` | 66/66 — 100 % |
| **Total del código nuevo / New code total** | **205/206 — 99,51 %** |

La única línea sin cubrir es la guardia de `CanExecute` del comando privado del menú de bandeja. La
política de dominio queda al 100 % de líneas. / The one uncovered line is a private command guard.

## Privacidad y límites / Privacy and boundaries

- **Sin telemetría ni red**: ningún archivo de esta tarea abre un socket, resuelve un nombre ni emite
  un evento remoto. La bandeja sólo dibuja un icono.
- **Sin credenciales**: no se guarda ninguna, ni se pide ninguna. El inicio con Windows es una
  entrada del usuario actual y **no requiere permisos de administrador**.
- **Sin rutas privadas en la evidencia**: el valor del registro contiene la ruta absoluta del
  ejecutable, así que se describe su forma y no su contenido.
- **Sin operaciones destructivas**: desactivar borra un valor propio; la clave y las demás entradas
  quedan intactas.
- **Artefactos ignorados**: `artifacts/` no aparece en `git status`.
- **Sin datos personales versionados**: ningún archivo tocado contiene nombre de usuario, nombre de
  equipo ni ruta absoluta local.

## Salvedades declaradas / Declared caveats

1. **Sin VM limpia de Windows.** Este equipo no tiene hipervisor con Windows —no existe `Get-VM` y no
   hay VirtualBox, VMware ni QEMU— y cerrar la sesión mataría la sesión de trabajo. El reinicio de
   sesión que pide el plan se sustituye por reiniciar el proceso y **ejecutar de verdad el comando
   registrado**, que es la parte que el reinicio de sesión probaría. Por eso `SYS-001` queda
   `IMPLEMENTED` y no `VERIFIED`.
2. **La desinstalación se comprueba como limpieza de la entrada**, no como desinstalación de paquete:
   el MSIX llega en T40. Lo que sí está demostrado es que desactivar no deja huérfanos.
3. **El proyecto de integración pasa a `net10.0-windows10.0.22621.0`** para poder ejercitar los
   adaptadores de Windows que el plan sitúa en `IntegrationTests/Windows/`. Su archivo de bloqueo se
   regeneró en el mismo commit.

/ Three caveats, declared rather than papered over.

`SYS-001` pasa a `IMPLEMENTED`: la bandeja y el inicio existen, están desactivados por defecto, sólo
se activan tras consentimiento explícito, son reversibles, el cierre escribe el progreso antes que
nada y la entrada del registro es exacta, idempotente, reparable y se limpia sin dejar rastro. /
The lifecycle identifier is implemented, with clean-VM verification blocked and declared.
