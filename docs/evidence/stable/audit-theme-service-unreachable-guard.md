# La guarda que hizo caer un suelo / The guard that dropped a floor

Un archivo cuya cobertura de ramas bajó sin que una línea suya cambiase, y por qué copiar el
artefacto no lo habría cerrado. / A file whose branch coverage fell with not one of its lines
changed, and why copying the artefact would not have closed it.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## La deuda que no se cerraba copiando el artefacto / The debt an artefact would not have closed

CI del commit `0eafd5c` falló con dos cosas y sólo una era mecánica:

- `App.axaml.cs` **subió** a 40/11 y su suelo dice 34/11 — eso se cierra copiando `coverage-debt`.
- `FluentThemeService.cs` **bajó** a 90/66 desde el 90/69 que tenía, **sin que una sola línea suya
  cambiase** en ese commit. / with not one of its lines changed.

Reproducido en local sobre los diez `coverage.cobertura.xml` del artefacto `test-results` de ese run:
**46/51 líneas y 20/30 ramas**, exactamente el 90/66 que CI anunció. Las ramas que faltan, por línea:

| Línea | Qué es | Veredicto |
| --- | --- | --- |
| 36–40 | los cinco `?? throw new ArgumentNullException` | alcanzables desde fuera: **faltaba la prueba** |
| 43 | una preferencia guardada que no es ninguna de las tres | alcanzable: un archivo de ajustes editado a mano |
| 61 | `Apply` con un valor fuera del enum | alcanzable: es público y está en `IThemeService` |
| **91** | `ApplyToApplication` validando **otra vez** | **nadie puede tomarla** |
| 121 | el respaldo de la duración sin tema montado | alcanzable |

La 91 es la que importa: sus dos únicos llamantes son `Apply`, que ya lanzó antes de llegar, y el
constructor, que pasa un valor que acaba de normalizar. Es la misma forma que `RouteStateConverter`
perdió tres de el mismo día. **Una guarda que ningún llamante puede alcanzar no es prudencia: son dos
ramas que ninguna prueba puede cubrir**, y quien contesta de verdad a un valor imposible es el brazo
`default` del `switch` de debajo, que devuelve el tema del sistema. / A guard no caller can reach is
not caution.

Lo que queda, en `ThemeServiceContractTests`, y **en C# con tipos directos y no por reflexión**: el
harness de `ThemeTests` invoca por reflexión, y eso envuelve la excepción que se quiere afirmar en una
`TargetInvocationException`. Tres pruebas: las cinco dependencias ausentes, la preferencia imposible
—refusada como argumento y perdonada como ajuste guardado, que es el sentido correcto—, y
`TryApplyBackdrop`, **al que no llegaba nada en absoluto**: sus dos líneas eran dos de las cinco sin
cubrir. / Three tests, and the backdrop was reached by nothing at all.

Los números finales los mide CI, no esta máquina, y por eso el suelo de este archivo se deja como
estaba: la vuelta siguiente copia el artefacto entero. Lo que aquí se afirma no es un porcentaje, es
que **la cobertura de este archivo dejó de ser un efecto secundario de las pruebas de otro**. / What is
asserted here is not a percentage: it is that this file's coverage stopped being a side effect of
somebody else's tests.
