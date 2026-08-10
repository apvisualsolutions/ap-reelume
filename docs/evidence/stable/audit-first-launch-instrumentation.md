# El rojo intermitente deja diagnóstico / The intermittent red leaves a diagnosis

Evidencia de la instrumentación del camino de fallo de un arranque en `eng/verify-package.ps1`: qué
dijo el fallo del 2026-08-10, qué no pudo decir, y qué dirá el siguiente. / Evidence for the
instrumentation on a launch's failure path: what the 2026-08-10 failure said, what it could not say,
and what the next one will.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-10.

## El rojo, recuperado del registro archivado / The red, recovered from the archived log

La ejecución `31407972838` (rama, 16:24 UTC) es la única en la que la fase `first-launch` ha fallado.
Su línea, entera: / Run `31407972838` is the only one where the phase has ever failed. Its line, in
full:

```
first-launch   Failed   Window shown: False; exit code -1; 16 migration(s) applied to a new database.
```

La fase duró 137 s —90 s de plazo de ventana más 45 s de cierre, los dos agotados— y en la misma
ejecución `open-with`, `repair`, `downgrade-refused` y las cuatro fases `windows-*` arrancaron la
aplicación y la vieron pintar. / The phase took 137 s — both deadlines exhausted — while seven other
launches in the same run started the application and saw it paint.

**Y ese registro contesta media pregunta que este proyecto tenía por abierta.** El plan y la nota de
retomada planteaban dos hipótesis indistinguibles, «murió antes de migrar» y «migró y nunca pintó».
Las **dieciséis** migraciones de esa línea descartan la primera: el proceso vivió lo suficiente para
aplicarlas todas. Lo que ningún registro dice es la otra mitad —si al llegar el plazo quedaba algo
vivo que pintar—, porque `exit code -1` es el matarile de este mismo arnés y no habla del arranque. /
**The archived log answers half of a question this project had recorded as open**: the sixteen
migrations rule out the death-before-migrating hypothesis. What no record says is the other half —
whether anything was still alive to paint — because that exit code is the harness's own kill.

## Lo que se hizo, y lo que a propósito no / What was done, and what deliberately was not

No se sube el plazo de 90 s y no se sale a buscar la carrera: no se reproduce en esta máquina, y una
carrera se quita, no se persigue. Lo que se instrumenta es el **camino del fallo**, que hoy es mudo. /
The 90 s deadline is not raised and the race is not hunted; what gets instrumented is the failure
path, which today says nothing.

Cuando la ventana no llega, y **antes** de matar el proceso —el matarile es lo que borra la
respuesta—, `Invoke-Application` anota:

- si el proceso seguía vivo, y si no, con qué código salió por su cuenta; / whether the process was
  still alive, and if not, the code it exited with on its own;
- cuánto procesador había gastado y cuántos hilos tenía, que separa **girar** de **esperar** —lo
  único que el registro archivado ya no permite distinguir—; / processor time and thread count, which
  separate spinning from waiting;
- si existe `library.db`, cuánto ocupa y cuántas filas tiene `schema_history`; / the database, its
  size, and its migration count;
- qué hay en la carpeta de datos, que es donde se ve un `-wal` o un `-journal` de una escritura a
  medias. / what the data folder holds.

Nada de eso puede lanzar: un diagnóstico que falla sustituye al fallo que venía a explicar, que es lo
único peor que no tener diagnóstico. Cada lectura va en su `try`, y lo que salga mal se anota como
nota dentro de la misma frase. / None of it may throw — a diagnosis that fails replaces the failure
it was called to explain — so every read is guarded and anything that goes wrong becomes a note
inside the same sentence.

Y un dato que cambia el análisis del próximo: **desde ARQ-005 la ventana se crea antes de migrar**,
así que si el plazo de ventana vuelve a agotarse, la migración queda descartada por construcción y el
fallo está por debajo — Avalonia, el arranque del runtime o el propio proceso. / Since ARQ-005 the
window precedes the migration, so a repeat rules the migration out by construction.

## Verde / Green

`LaunchDiagnosisTests` toma las funciones **del guion que se publica**, parseándolo en vez de
copiarlas, y las ejecuta contra procesos de estado conocido: uno vivo con una carpeta vacía y uno que
ya salió con código 7 sobre un `library.db` que no es una base de datos. / The test takes the
functions out of the shipped script by parsing it, and runs them against processes whose state is
known.

**Rojo archivado**: con el guion anterior, la misma prueba falla nombrando lo que falta. / **Red**:
against the previous script the same test fails, naming what is missing.

```
The diagnosis script failed: eng/verify-package.ps1 no longer defines:
Get-LaunchDiagnosis, Format-LaunchDiagnosis.
```

**Verde**, con las frases que produce: / **Green**, with the sentences it produces:

```
ALIVE:  the process was still running (0,39 s of processor time across 27 thread(s)); no library.db;
        the data folder is empty
EXITED: the process had already exited with code 7; library.db of 22 byte(s), schema_history unread;
        the data folder holds library.db [library.db could not be read: … 'file is not a database'.]
```

La segunda es la que importa más de las dos: la base ilegible **no rompe nada**, se cuenta. / The
second matters most: the unreadable database breaks nothing, it gets reported.

Y la línea que vería quien lea CI la próxima vez que esto ocurra, que es el entregable de la tarea: /
And the line whoever reads CI would see next time, which is the deliverable:

```
No window inside 90000 ms — the process was still running (0,39 s of processor time across
27 thread(s)); no library.db; the data folder is empty
```

| Puerta / Gate | Resultado / Result |
|---|---|
| `LaunchDiagnosisTests` | 1 de 1 / of 1 |
| `eng/verify.ps1` completo / full | verde, con las siete fases sustitutas en su sitio / green |
