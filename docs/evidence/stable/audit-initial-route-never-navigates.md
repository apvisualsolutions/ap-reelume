# La ruta con la que nace el shell no navega / The route the shell is born on never navigates

La Home sabía leerse y nadie se lo pedía al arrancar: `CurrentRoute` nace puesto en `Home`, el
evento `Navigated` sólo se dispara al navegar, y toda alimentación de superficies vive en el
manejador de ese evento. / Home knew how to read itself and nobody asked at startup: `CurrentRoute`
is born set to `Home`, `Navigated` only fires on a navigation, and every surface feed lives in that
event's handler.

Fecha / Date: 2026-08-24. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El síntoma y la forma / The symptom and the shape

La captura de la Home para la matriz de evidencia salía sin «En curso» sobre un data root sembrado
cuyo `watch_state` los repositorios sí leen. La sospecha anotada la sesión anterior —«¿la ruta
inicial no dispara `NavigatedAsync`?»— era la causa exacta, leída en tres archivos: / The Home
capture for the evidence matrix came out without its in-progress rail over a seeded data root whose
`watch_state` the repositories do read. The suspicion noted the session before — "does the initial
route ever fire `NavigatedAsync`?" — was the exact cause, read in three files:

- `NavigationService.CurrentRoute` se inicializa en `AppRoute.Home` **sin pasar por `Navigate`**,
  así que `Navigated` nunca se dispara para la ruta inicial. / initialized without going through
  `Navigate`, so `Navigated` never fires for the initial route.
- `ShellViewModel` sólo carga superficies dentro de `NavigatedAsync` — Home, Biblioteca vacía,
  bandeja de revisión, duplicados—. / only feeds surfaces inside `NavigatedAsync`.
- El anfitrión (`CompositionRoot.FinishShell`) arranca vigilancia, actualizador, LIB-016 y estilo
  de subtítulos, pero ninguna carga de la ruta inicial. / the host starts watching, updater,
  LIB-016 and subtitle style, but no initial-route load.

Es la decimocuarta forma del defecto de la casa: una superficie construida, registrada, con su
lector escrito, y sin nadie que la alimente en el único momento en que todos los usuarios la miran
—el arranque—. Salir de la Home y volver la cargaba; el primer vistazo, jamás. / The house defect's
fourteenth shape: a surface built, registered, its reader written, and nobody feeding it at the one
moment every user looks at it — startup. Leaving Home and coming back loaded it; the first look,
never.

## El rojo / The red

`The_route_the_shell_is_born_on_feeds_its_surface_without_a_navigation` en `ShellAssemblyTests`:
un `HomeViewModel` con una película a medias en su read model, entregado a un shell recién
construido con un `NavigationService` recién nacido, sin navegar. / handed to a freshly built shell
over a newborn `NavigationService`, with no navigation.

```text
[xUnit.net] ShellAssemblyTests.The_route_the_shell_is_born_on_feeds_its_surface_without_a_navigation [FAIL]
  Assert.True() Failure
  Expected: True
  Actual:   False   (home.HasInProgress)
Con error! - Con error: 1, Superado: 0, Total: 1
```

## La corrección mínima / The minimal fix

El constructor del shell, tras suscribirse, **reproduce la ruta inicial por el mismo camino que una
navegación**: `GuardedEvent.Run(() => NavigatedAsync(_navigationService.CurrentRoute))`. No hay una
segunda ruta de carga que mantener: la inicial y las navegadas comparten la única que existe. Una
ruta inicial sin superficie entregada sigue sin hacer nada, que es lo que las pruebas de un shell
vacío afirman. / The shell's constructor, after subscribing, replays the initial route through the
same path a navigation takes. There is no second load path to maintain: the initial route and the
navigated ones share the only one there is. An initial route with no surface handed over still does
nothing, which is what the empty-shell tests assert.

## Verificación / Verification

| Suite | Resultado / Result |
| --- | --- |
| UiTests | 778/778 |
| AccessibilityTests | 146/146 |
| ArchitectureTests | 30/30 |
| IntegrationTests | 461 verdes, 1 omitida documentada / 461 green, 1 documented skip |

La observación pendiente de la sesión anterior —«la home vacía con click en su propio botón de riel
ya activo»— queda explicada sin defecto segundo: `Navigate` no filtra la ruta repetida y el botón
del riel siempre ejecuta su comando, así que ese click sí cargaba; las capturas donde se observó se
hicieron con el data root pisado por la herramienta, defecto ya corregido y documentado el
2026-08-23. / The pending observation — "Home empty after clicking its own already-active rail
button" — is explained with no second defect: `Navigate` does not filter a repeated route and the
rail button always executes its command, so that click did load; the captures where it was observed
were taken with the data root overridden by the tool, fixed and documented on 2026-08-23.
