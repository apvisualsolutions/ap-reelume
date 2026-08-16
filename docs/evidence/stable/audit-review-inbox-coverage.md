# La pantalla de revisión entra en la lista de vigilados / The review surface joins the watched list

La deuda que la tanda 5 tenía que pagar antes de cerrarse: `ReviewInboxViewModel.cs` pasa de
**92,13 % de líneas y 59,26 % de ramas, vigilado por nadie**, a **100/100 vigilado en cada ejecución**.
/ The debt batch 5 had to pay before closing: from 92.13 % lines and 59.26 % branches watched by
nobody, to 100/100 held at every run.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Los números / The numbers

| Momento / Moment | Líneas / Lines | Ramas / Branches |
|---|---|---|
| Antes de la tanda 5 / Before batch 5 | 92,13 % | 59,26 % |
| Con las dos reasignaciones pulsadas / With both reassignments pressed | 93,82 % | 59,26 % |
| Hoy / Today | **100,00 %** | **100,00 %** |

Nótese la fila del medio: **pulsar los dos botones no movió una sola rama**. Un paseo prueba que un
control hace su trabajo; no llega a los caminos que sólo se toman cuando algo va mal. / Pressing both
buttons moved no branch at all: a walk proves a control does its work and never reaches the paths
taken only when something goes wrong.

## Dos hallazgos que la medición destapó / Two things the measurement turned up

### Ramas que no se podían tomar / Branches that could not be taken

`(SearchManuallyCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged()`, en tres sitios. Las dos
propiedades **siempre** son `AsyncRelayCommand` —se asignan en el constructor y no tienen `set`—, así
que la mitad nula del `?.` era inalcanzable y la cobertura de ramas no podía llegar a 100 por
construcción. / The nullable half of that `?.` was unreachable, so branch coverage could not reach
100 by construction.

No se ha borrado la comprobación: se ha quitado la necesidad de comprobar. Los dos comandos viven en
campos con su tipo, como los de `ShellViewModel`. Y esto **no es cosmético**: si alguien cambiara el
tipo del comando, el `as` no fallaría, devolvería nulo, y el botón dejaría de anunciar que su
disponibilidad cambió — que es exactamente la avería de `ARQ-004` que esta misma clase sufrió y que se
corrigió el 2026-08-16. La conversión silenciosa era el camino de vuelta a ella. / The cast could not
fail, but it could stop matching, and a command that quietly stops announcing `CanExecuteChanged` is
the very defect this class carried until yesterday.

### Una rama que dos suites no pueden cubrir entre las dos / A branch two suites cannot share

`if (!_nextOffset.HasValue)` en `LoadMoreAsync`. El paseo recorre veintiséis candidatos y toma el lado
«hay más»; las pruebas de interfaz tenían uno solo y tomaban el lado «no hay más». Las dos mitades
estaban ejercidas y la rama **seguía leyéndose como media**: al fusionar informes Cobertura se conserva
**el mejor de los dos** para cada línea, no la unión. / Both halves were exercised and the branch still
read as half-covered: merging Cobertura keeps the better report for a line rather than the union.

Es una regla del proyecto a partir de hoy: **una rama se cubre entera dentro de una sola suite**, o no
se cubre. La prueba de paginación hace las dos cosas seguidas. / A branch is covered whole within one
suite, or it is not covered.

## Lo que se añadió, y por qué no es cobertura de adorno / What was added

Todo lo que faltaba era el camino de lo que sale mal, y cada prueba nombra una situación real:

- **Sin nada seleccionado**, aceptar, rechazar y buscar no hacen nada. Se llega pulsando Escape.
- **Otro decidió primero**: el conflicto conserva la ficha con lo que el catálogo tiene y la deja
  seleccionada; y toca **sólo** la ficha de la que iba, no las demás.
- **La ficha ya no existe**: se rechaza sin nada que poner en su sitio, y la lista se queda igual.
  Sustituirla por un nulo habría sido el fallo.
- **Sin sus colaboradores**, una decisión retenida se rechaza en vez de lanzar: la sección es
  invisible en esa configuración, pero cualquiera con una oferta en la mano puede pedirla.
- **Los guardias de argumento** de las tres clases, que es donde vive el contrato.
- **Repetir un valor no anuncia nada**, porque una lista que dice que cambió es una lista que la
  pantalla reconstruye.

## Las puertas / The gates

`dotnet format --verify-no-changes --severity warn`, compilación con `-warnaserror` (0 avisos), las
**diez suites** con cobertura a la misma carpeta —2 080 pruebas, ninguna roja— y
`eng/check-coverage.ps1`: **seis archivos vigilados, los seis en 100/100**. / All green, with the
watched list now six files long and every one of them at 100/100.
