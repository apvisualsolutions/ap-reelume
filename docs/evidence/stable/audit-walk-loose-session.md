# El archivo que llega del Explorador / The file that arrives from Explorer

Un vídeo abierto desde fuera de la biblioteca **se reproducía y no se veía**. Corregido, y con ello
los tres botones del aviso que ofrece añadir su carpeta, pulsados con el ratón. / A video opened from
outside the library **played and could not be seen**. Corrected, and with it the three buttons of the
notice that offers to add its folder, pressed with the mouse.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 122 | **125** |
| Pendientes / Pending | 6 | **3** |

```
The walk: 129 declared command controls in 128 identities; 125 pressed, 3 pending.
```

## El defecto / The defect

```
singleton.IsLooseSession=True  name='Arrival.2016.mp4'  engine=Playing  pos=00:00:00.15
player=False  playerVisible=False  stages=0  surfaces=0
```

La activación hacía **su parte entera**: validaba el archivo, arrancaba el motor y le daba su sesión
al aviso. Lo que no hacía nadie era construir **las superficies del reproductor**, y el aviso cuelga
de `Player?.LooseFile`. Resultado: hacías doble clic en un vídeo desde el Explorador, la aplicación lo
reproducía, y te quedabas en la pantalla de inicio — sin imagen, sin transporte y sin forma de
pararlo, con el mensaje de «esto no está en tu biblioteca» y sus tres botones dentro de una vista que
nunca se montaba. / The activation did its whole part; nobody built the player surfaces, and the
notice hangs off them.

**La causa de raíz no es el aviso: son dos caminos que abren medios donde sólo uno tiene pantalla.**
`OpenLooseFile` arrancaba el coordinador por su cuenta; `PlayerViewModel.OpenAsync` arranca y además
tiene superficie. Mientras hubiera dos, esto volvía. / The root cause is two paths opening media where
only one has a screen.

## La corrección / The correction

**`OpenLooseFile` valida y describe, y deja de abrir.** Se queda con el juicio que tiene que ocurrir
**antes** de tocar nada —un contenedor aprobado y un archivo que está de verdad— y abrir pasa a ser
del reproductor, siempre, por una vía única que los **dos** llamantes usan: la activación desde el
Explorador y el tráiler local. / It keeps the judgement that has to happen before anything is opened,
and opening becomes the player's, through one path both callers use.

- `ShellSurfaces.OpenLoosePlayer` y `ShellViewModel.OpenLoosePlayerAsync`, al lado de `OpenPlayer`.
- La sesión suelta recibe **reproductor, transporte, el diagnóstico de la imagen y el aviso**, y nada
  más: **sin seguimiento de progreso, sin marcadores, sin versiones y sin oferta de reanudar**. Eso es
  lo que conserva la promesa que `FileActivationTests` vigila — el censo de más de veinte tablas es
  idéntico antes y después de una activación.
- **No se reutiliza `OpenPlayerAsync`**, y no por gusto: empieza leyendo el archivo del catálogo —un
  archivo suelto no está en ninguno— y su camino engancha el seguimiento, lee la decisión de reanudar,
  compone los marcadores y busca el grupo de versiones. Cada una de esas cosas escribe o lee algo que
  una sesión suelta prometió no tocar.

**`OpenLooseFile` pasa a ser estático, y lo decidió el compilador**: sin la apertura no le queda
estado, y `CA1822` lo dijo. Sale del contenedor en el mismo cambio, porque un registro que nadie
resuelve es el defecto característico de esta casa. / It became static because the compiler said so,
and left the container in the same change.

**Lo que se gana además:** hoy un archivo suelto que no se puede decodificar hacía que el `catch`
limpiara el aviso y no quedara nada en pantalla. Abriendo por el reproductor, el fallo llega a
`Report` y **aparece la pantalla de recuperación** que la tanda anterior dejó probada. / A loose file
that cannot be decoded now reaches the recovery screen instead of leaving an empty one.

## Lo que la escena prueba / What the scene proves

| Control | Efecto afirmado / Asserted effect |
|---|---|
| Añadir la carpeta que lo contiene / Add containing folder | aparece la confirmación: añadir una raíz no es un encogimiento de hombros / the confirmation appears |
| Cancelar / Cancel | la confirmación se retira y **no se añade nada** / it withdraws and nothing is added |
| Añadir la carpeta, otra vez / Add again | la pregunta se puede volver a levantar tras refusarla / it can be raised again |
| Añadir a la biblioteca / Confirm | **una fila nueva en `library_roots`**, leída del catálogo / a new row in `library_roots` |

Cancelar va **en medio** y no al final: confirmar añade la raíz, y después no queda nada que refusar.

## El tercer superpuesto que se estiraba / The third overlay that stretched

```
stage=0, 0, 1280, 1400
surface=0, 0, 1280, 1400     ← antes / before
```

Sin alineación, el aviso se estiraba sobre **todo el escenario del reproductor** — y a diferencia de
la oferta de reanudar, **lleva fondo**, así que además se comía cada clic destinado al vídeo de
detrás. Recibe alineación, margen y **las dos dimensiones acotadas**, que es la mitad de este defecto
que se olvida: un `MaxWidth` solo deja la altura estirándose. La escena lo afirma en vez de mirarlo. /
Without alignment it stretched over the whole stage and, carrying a background, swallowed every click
meant for the video behind it. Both dimensions are now bounded, and the scene asserts it.

## Dos pruebas que el cambio destapó / Two tests the change exposed

- **`ResumeWiringTests` se rompió por leer la composición como texto**, cuarta aparición de esa forma:
  buscaba el primer `player.OpenAsync(` del archivo y la apertura suelta quedó antes, así que comparó
  una decisión de reanudar contra una sesión que **deliberadamente no tiene ninguna**. Se acota a la
  **declaración** de `OpenPlayerAsync`, que es el método del que habla.
- **`RepositoryPrivacyTests` señaló `design/`**, y estaba en lo cierto: enumera las carpetas de la
  raíz que no reconoce y las trata como carpetas personales del propietario. Una carpeta nueva
  versionada se declara en su lista; eso es la comprobación funcionando, no un falso positivo.

## Las puertas / The gates

```
dotnet format --verify-no-changes --severity warn                      # limpio / clean
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1   # 0 avisos / 0 warnings
dotnet test …Application.Tests                                         # 229 / 229
dotnet test …UiTests                                                   # 448 / 448
dotnet test …IntegrationTests                                          # 451 / 452, 1 omitida / skipped
dotnet test …AccessibilityTests                                        # 115 / 115
eng/check-walk-coverage.ps1                                            # 125 pulsados, 3 pendientes / pressed, pending
```

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
./eng/check-walk-coverage.ps1
```
