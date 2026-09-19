# La adopción del sistema común de trabajo, medida

**Fecha:** 2026-09-19. **Plugin:** `ap-smart-tech` 0.7.4, comprobado por efecto con
`claude -p "/ap-smart-tech:ping"` en una sesión nueva. **Tareas:** `ENG-033`, `ENG-034`, `ENG-035`,
`ENG-036` y `ENG-037`.

Este repositorio es el piloto real del sistema común de la casa. Adoptarlo no es instalarlo: se
midió lo que el proyecto ya hacía, se escribió el manifiesto con lo medido y se probó cada puerta
provocándola. Todo se hizo en una rama local y sin tocar ningún remoto.

## La secuencia, con lo que salió

| Fase | Salida | Lo que dijo |
| --- | --- | --- |
| `measure` | 0 | 1.583 ficheros seguidos, cinco hooks declarados, «vivo sin comprobar». Cuenta **una** batería porque sólo reconoce ficheros `test-*.ps1`: las diez suites de .NET no las ve. |
| `map` | 0 | Borrador válido con valores de plantilla (`T-001`, `HECHA`, `docs/RELEVO.md`), corregido a mano con lo medido. |
| `learn` en seco | 1 | Se rompe en el extractor de fugas: un fichero vacío le da un texto nulo. Informado a la sesión de IT; no se rodeó. |
| `gap` | 0 | Nueve de nueve fases cubiertas, después de dar a `stop` el comando que su paso ejecuta de verdad: `gap` sólo mira `command` y no `waitFor`, que el esquema admite. |
| `apply` | 0 | Con una sola llave —la bandera o la variable— imprime lo que haría y `git status` queda vacío; con las dos escribe `.claude/ap-smart-tech-install.json`, sin ninguna ruta de la máquina. |
| `prove` | 0 | Doce puertas, doce como deben; recibo con huella `24971ecf81ac7dfe…`. |
| `prove`, «no mide» | 2 | Una puerta cuyo guion ya no existe, otra sin la variable de las herramientas y otra sin comando: las tres «no se pudo medir», ninguna en verde. |

## Lo que se midió del backlog para el manifiesto

- **Identificadores**: 37 filas `ENG-NNN` en `TAREAS.md` y 73 en la matriz con diez prefijos (`CRS`,
  `DAT`, `DOC`, `LIB`, `PLY`, `PRD`, `PRI`, `REL`, `SYS`, `UX`), todos de tres cifras. El patrón no
  casa con `ADR-0012` ni con los identificadores de la sesión de IT, y el validador lo comprueba con
  muestras que deben casar y otras que no.
- **Estados**, tal como se escriben: los seis de `TAREAS.md` y los nueve de la matriz. En `TAREAS.md`
  una tarea hecha no lleva estado sino que cambia de tabla, así que lo pendiente se lista con
  `eng/list-pending.ps1`, que ya cuenta por dos caminos.
- **La sonda del cajón**: «registro de tareas la más vieja arriba» devuelve primero la nota real del
  registro.

## Las puertas, y sus dos casos

Cada una se provoca con `.claude/skills/cierre/scripts/gate-probe.ps1` contra un árbol de mentira
bajo el temporal, nunca contra este repositorio: la de commits y pushes del plugin, la del final de
turno, el acta, el tope del relevo, el filtro de privacidad en modo público y el `.gitignore`. Cada
una tiene un caso que debe sonar y otro que debe callar. Sin el segundo, un instrumento que grita
siempre pasaría por bueno; sin el primero, uno muerto pasaría por limpio. Las dos del acta salieron
primero «no se pudo medir» por un error del propio guion, que es el tercer resultado funcionando.

## El guardián que se retiró

`pre-push-closing.sh` no tenía batería guardada. Se rehízo con diez casos en un clon desechable, y
pasaron los diez: dos que deniegan (código y `eng/` sin subir) y ocho que dejan pasar, incluidos el
fast-forward de `main`, un heredoc que cita un push y un fichero nuevo sin añadir. Ese último era el
agujero que el plan de IT pedía mirar: el hook sólo veía commits. Un mutante que no deniega nunca
rompe dos casos. Se retiró en el mismo commit que activa el sistema común.

## El acta, por efecto

La batería de `cierre-acta.ps1` pasó de 16 a 25 casos. Los nuevos cubren tres cosas:

- **Las comprobaciones de IT por su efecto.** Una rama no tomada, una que no pudo medir y una
  anterior a la marca, las tres en rojo. Una que sólo señala, en verde.
- **La regla del paso 0.** Código, `eng/` o un fichero nuevo sin añadir hacen fallar el acta; sólo
  documentación la deja pasar.
- **El recibo.** Lleva siempre el mismo código que la salida.

Tres mutantes, y los tres mueren:

- dar siempre por bueno el efecto rompe 3 casos;
- dejar de mirar lo que no es documentación rompe 3;
- escribir siempre 0 en el recibo rompe 20.

## El ajuste local y el relevo

- `git -c core.excludesFile=/dev/null check-ignore -v .claude/settings.local.json` salía 1 y ahora
  responde `.gitignore:55`. `.claude/settings.json` sigue sin ignorarse.
- `eng/check-handoff.ps1` contra el relevo de 9.183 líneas dio siete hallazgos. Contra el nuevo
  sale 0. `HandoffLimitsTests` lleva siete pruebas: la real y seis escenas.

## Lo que se informa a la sesión de IT, sin rodearlo

1. **El filtro de datos internos da falsos positivos con el propio manifiesto**: toma la doble
   barra invertida con la que el JSON escribe una expresión regular por el comienzo de una ruta de
   red, así que el filtro del cierre suena en cada cierre que toque el manifiesto.
2. **`learn` se rompe con un fichero vacío** en el extractor de fugas y sale 1 en vez de 2. Además
   recorre carpetas que git no sigue.
3. **`gap` ignora `waitFor`**, que el esquema admite para la fase `stop`.
4. **Ningún guion escribe la espera declarada** que la puerta del final de turno sabe leer.
5. **El barrido común de memorias** sólo busca la carpeta en los ajustes versionados, y en un
   repositorio público ahí no puede ir una ruta de la máquina.
6. **`measure` no reconoce las baterías de .NET**.
7. **El recibo del acta no va atado al estado del árbol**: después de un acta en verde, un commit de
   código pasaría la puerta hasta que el acta se vuelva a calcular.
