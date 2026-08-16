# La biblioteca sacada y devuelta con el ratón / The library copied out and brought back with the mouse

Cuatro controles que **ningún arnés podía pulsar** hasta hoy, porque los dos que preguntan por una
ruta se la preguntan a un diálogo modal de Windows. La regla de aislamiento llega por fin al sitio
para el que se escribió. / Four controls no harness could press, because the two that ask for a path
ask a modal Windows dialog. The isolation rule finally reaches what it was written for.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 84 | **88** |
| Pendientes / Pending | 44 | **40** |
| Pruebas de accesibilidad / Accessibility tests | 98 | **99** |

```
The walk: 129 declared command controls in 128 identities; 88 pressed, 40 pending.
Accessibility Verify over 2 pass(es): 0 critical, 0 major, 0 minor.
```

## Por qué estos cuatro estaban bloqueados / Why these four were blocked

`ChooseArchiveDestinationAsync` y `ChooseArchiveSourceAsync` empiezan igual: si no hay `MainWindow`,
devuelven `null`. Bajo un arnés **nunca** hay `MainWindow`, y `null` significa «cancelado» —así que
pulsar «Exportar» no hacía nada, y no hacer nada no deja nada que sondear. No es que la prueba fuera
difícil: es que el control no tenía efecto que medir. / Under a harness there is never a main window,
so both pickers answer null, null means cancelled, and a control that does nothing has no effect to
probe.

**La corrección no necesitó interfaz nueva.** Los dos modelos ya reciben un
`Func<CancellationToken, Task<string?>>`; lo que cambia es **quién lo cumple**, decidido una vez en la
composición por `IAppDataPaths.SystemHandoffDirectory`, exactamente como se decidió el lanzador de
enlaces. / No new interface: both view models already take a delegate, and which one fulfils it is
decided once, by the resolved data root.

- Quien es dueño del perfil: el diálogo de Windows, sin un solo cambio. / The person whose profile
  this is: the Windows dialog, unchanged.
- Una ejecución con raíz propia: `HandoffArchivePicker`, que responde **dentro de la carpeta de
  traspaso** —exportar escribe allí, restaurar lee lo que haya— y responde `null` si no hay nada, que
  es lo mismo que contesta un diálogo cancelado. / An isolated run answers inside its handover folder,
  and answers null when it holds nothing — which is what a cancelled dialog answers.

Componer la ruta en vez de recibirla es la parte que merece nombrarse: lo que normalmente impide que
la aplicación vea una carpeta que nadie le ofreció es el diálogo, y aquí ese trabajo lo hace el
confinamiento a la raíz. / Composing the path rather than being handed one is the part worth naming;
the confinement to that root does the job the dialog normally does.

## Lo que encadena la escena / What chains the scene

Nada en la escena compone una ruta. La exportación va donde **la aplicación** dice, y la restauración
toma lo que **la aplicación** encuentra allí: lo que salió es lo que vuelve. / Nothing composes a
path: the export goes where the application says, and the restore takes what the application finds
there.

Las cuatro sondas leen el **disco**, no la pantalla:

| Control | Sonda / Probe |
|---|---|
| Crear una copia ahora | Carpetas publicadas en `BackupsDirectory` / Folders published in the backups directory |
| Exportar a un archivo ZIP | ZIP en la carpeta de traspaso / Archives in the handover folder |
| Elegir un archivo y ver qué haría | El plan en pantalla / The plan on screen |
| Restaurar ahora | `library.db.pre-restore-*.bak`, la base apartada / The database moved aside |

La tercera es la única que lee la pantalla, y con razón: su efecto **es** el plan. La cuarta no lee
«restaurado», que una pantalla puede decir sin haber hecho nada: lee que la base que estaba viva se
guardó al lado de la nueva, que es la única prueba de que hubo intercambio. / Only the dry run reads
the screen, because the plan is its effect; the restore reads the preserved database rather than the
word "restored", which a screen can say without having done anything.

## La carrera que salió a la primera ejecución / The race the first run found

```
Expected: "BackupStatusDone"
Actual:   "BackupStatusRunning"
```

**La carpeta aparece en el disco antes de que la pantalla lo diga.** La copia se publica y sólo
después corre la continuación que fija el estado, así que una sonda que lee el disco queda satisfecha
mientras la pantalla sigue en «copiando». Leer el resultado justo después de la pulsación es afirmar
sobre una carrera. Ahora se espera al reposo y **luego** se dice qué salió. / The folder lands before
the screen says so, so the outcome is waited for rather than read straight after the press.

Es la misma carrera que la escena de privacidad encontró desde el otro lado, y ya van dos: **una
sonda de disco que pasa no significa que la pantalla haya terminado**. / Second time this shape
appears: a disk probe passing does not mean the screen has finished.

## Lo que la restauración demostró de paso / What the restore proved along the way

El intercambio ocurre **con la aplicación viva y la biblioteca cargada**, y funciona: `SwapAsync`
llama a `SqliteConnection.ClearAllPools()` antes de mover nada, y eso basta porque el catálogo abre y
cierra su conexión por operación. Hasta hoy eso no lo había comprobado nadie desde la aplicación
ensamblada. / The swap happens with the application running and the library loaded, and it works —
nobody had checked that from the assembled application before.

**Un hallazgo que resultó falso, y la razón de que lo fuera merece quedarse.** Esta evidencia afirmó
primero que el segundo constructor de `StagedRestoreService` —con `availableBytes` y un gancho
`beforeSwap`— no lo usaba nadie, y que el gancho era un punto de extensión imaginario en el único
momento destructivo del programa. **Es falso.** Lo destapó el compilador al retirarlo el 2026-08-17: /
This evidence first claimed the three-argument constructor was used by nobody. It is false, and the
compiler said so:

```
error CS1729: 'StagedRestoreService' no contiene un constructor que tome 3 argumentos
```

`DisasterRecoveryTests` lo usa, y lo usa **para lo que existe**: `onBeforeSwap: cancellation.Cancel`
prueba una cancelación justo antes del intercambio, y
`onBeforeSwap: () => throw new IOException(...)` prueba un intercambio interrumpido. Son los dos
caminos que deciden si un fallo a mitad pierde la biblioteca de alguien. / It is used to test a
cancellation and an interrupted swap — the two paths that decide whether a half-way failure loses
somebody's library.

**Por qué la búsqueda mintió:** la llamada es `new(Paths, _ => availableBytes, onBeforeSwap)`, con el
tipo inferido del método que la devuelve. Un `grep` de `new StagedRestoreService(` no la encuentra, y
el silencio se leyó como ausencia. / The call uses a target-typed `new`, which a grep for
`new StagedRestoreService(` does not find, and the silence read as absence.

**Regla, y es la que se lleva de aquí:** para saber quién construye un tipo se pregunta al
compilador, no al buscador — retirar el miembro y compilar cuesta un minuto y no puede equivocarse. /
Ask the compiler who constructs a type, not the search: removing the member and building takes a
minute and cannot be wrong.

## Lo que queda de la tanda / What is left of the batch

«Cancelar» (`BackupCancelLabel`) sigue pendiente y es la **6b**: lleva `IsEnabled="{Binding
IsRunning}"` **y** `CanExecute => IsRunning`, así que sólo existe mientras una copia corre, y con la
biblioteca de un arnés la copia termina en milisegundos. Hace falta sembrar una biblioteca que
**tarde** y medir cuánto antes de escribir la escena. / Cancel only exists while a copy is running,
and a harness-sized library finishes in milliseconds; it needs a slow library, measured first.

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet test tests/ApSolutions.LocalMedia.IntegrationTests/ApSolutions.LocalMedia.IntegrationTests.csproj `
  -c Release -m:1 --settings eng/test.runsettings --logger trx --filter "FullyQualifiedName~HandoffArchivePickerTests"
./eng/run-accessibility.ps1 -Mode Verify -Passes 2
./eng/check-walk-coverage.ps1
```

Las dos pasadas no son adorno: es como CI lo corre, y es lo único que ha reproducido las carreras que
`dotnet test` a secas deja pasar. / Two passes is how CI runs it, and the only thing that has
reproduced the races a single run lets through.
