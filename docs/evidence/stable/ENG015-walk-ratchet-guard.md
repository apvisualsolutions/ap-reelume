# ENG-015 — La guarda del trinquete del paseo aprende a distinguir / The walk ratchet guard learns to tell two things apart

Evidencia de `ENG-015`, cerrada el 2026-09-20. El defecto no estaba en el texto desfasado: estaba en
que el guardián impedía arreglarlo. / Evidence for `ENG-015`, closed on 2026-09-20. The defect was
not the stale text: it was that the guard made it impossible to fix.

## El RED archivado / The archived RED

`eng/walk-pending.txt` llevaba en su cabecera «The ratchet in eng/check-walk-coverage.ps1 is 20» y
«The twenty below», mientras `eng/check-walk-coverage.ps1:100` decía `$maximumPending = 23` y el
fichero tenía 23 filas. El hook de `PreToolUse` en `.claude/settings.json` denegaba **toda** escritura
sobre ese fichero sin mirar qué cambiaba, así que la cabecera no se podía corregir con las
herramientas de edición y el propio guardián sostenía la contradicción. / The header of
`eng/walk-pending.txt` read "The ratchet in eng/check-walk-coverage.ps1 is 20" and "The twenty
below", while `eng/check-walk-coverage.ps1:100` said `$maximumPending = 23` and the file held 23
rows. The `PreToolUse` hook in `.claude/settings.json` refused **every** write to that file without
looking at what changed, so the header could not be fixed with the editing tools and the guard
itself kept the contradiction alive.

## La secuencia real, medida y no copiada / The real sequence, measured rather than copied

`ENG-015` avisaba de que `CLAUDE.md` cuenta una subida a 23 el 2026-08-25 **y** otra «a 23» el
2026-09-02, lo que no puede ser, y pedía reconstruir la secuencia antes de reescribir nada o se
sustituye una cifra falsa por otra. / `ENG-015` warned that `CLAUDE.md` tells of a rise to 23 on
2026-08-25 **and** another "to 23" on 2026-09-02, which cannot both be true, and asked for the
sequence to be rebuilt first or one false figure just replaces another.

**El instrumento importa aquí**: `git log -S 'maximumPending = '` devuelve **un solo commit**, porque
`-S` cuenta apariciones de una cadena y esa cadena no cambia de número de apariciones cuando sólo
cambia la cifra. Con `-G`, que es una expresión regular sobre el diff, salen los treinta y uno. /
**The instrument matters here**: `git log -S 'maximumPending = '` returns **one commit**, because
`-S` counts occurrences of a string and that string's count does not change when only the number
does. With `-G`, a regular expression over the diff, all thirty-one show up.

```
git log --format=%h -G 'maximumPending = [0-9]+' -- eng/check-walk-coverage.ps1
```

| Fecha / Date | Commit | Valor / Value |
| --- | --- | --- |
| 2026-08-18 | `76a6df46` | 0 |
| 2026-08-25 | `3a54522a` | **20** |
| 2026-09-02 | `64e957d1` | 22 |
| 2026-09-02 | `9294d880` | **23** |

Con eso, los textos desfasados eran **dos y no tres**: las líneas 5 y 9 del fichero. La 53 dice
«from 20 to 22» y estaba **completa**, porque la 65 del mismo bloque cuenta la segunda subida del
mismo día. Y la cuarta corrección estaba en `CLAUDE.md`, que decía que subió a 23 el 2026-08-25
cuando subió a 20: ésa era **falsa**, no vieja. El `<!--medido:paseo-pendiente-->` se dejó sobre el 23
de hoy, porque `QuotedFigureTests` cuenta las filas reales del fichero. / With that, the stale texts
were **two, not three**: lines 5 and 9. Line 53 says "from 20 to 22" and was **complete**, because
line 65 of the same block tells of the second rise on the same day. The fourth correction was in
`CLAUDE.md`, which said it rose to 23 on 2026-08-25 when it rose to 20: that one was **false**, not
stale. The `<!--medido:paseo-pendiente-->` marker was left over today's 23, because
`QuotedFigureTests` counts the file's real rows.

## La corrección / The fix

`.claude/hooks/pre-write-ratchets.sh` sustituye el bloque en línea. El criterio: **deniega si el
cambio toca una FILA, deja pasar si sólo toca COMENTARIOS `##`**.

- `Edit` y `MultiEdit`: se miran las dos caras del cambio, porque insertar texto dentro de una fila
  es tan cambio como borrarla. Se deniega si alguna línea completa es una fila, y también si el
  fragmento **empieza o termina a media fila** — sin eso, un cambio que arrancara en `Surround51` y
  siguiera hacia los comentarios pasaría, porque esa línea suelta no lleva almohadilla.
- `Write`: se compara por efecto. Las filas del contenido propuesto tienen que ser las mismas y en
  el mismo orden que las del fichero en disco.
- `eng/coverage-debt.txt` conserva la denegación total: ahí no hay nada que distinguir, porque **lo
  produce CI** y se copia de su artefacto por consola.

/ `.claude/hooks/pre-write-ratchets.sh` replaces the inline block. The criterion: **refuse if the
change touches a ROW, let through if it only touches `##` COMMENTS**. `Edit` and `MultiEdit` are
checked on both sides of the change, including fragments that start or end mid-row; `Write` is
compared by effect against the rows on disk; `eng/coverage-debt.txt` keeps its total refusal because
CI produces it.

## El GREEN, y por qué la bateria vive en el arbol / The GREEN, and why the battery lives in the tree

Un hook que calla **no deja rastro** en el registro de la sesión —sólo se anota cuando produce
salida—, así que un silencio observado no prueba que la guarda corriera. Lo único que lo prueba es
ejecutarla por tubería con un caso que debe sonar al lado del que debe callar. Por eso
`.claude/hooks/pre-write-ratchets.test.sh` está versionado y no en un directorio temporal. / A hook
that stays quiet **leaves no trace** in the session log, so an observed silence proves nothing. Only
running it through a pipe, with a case that must fire beside one that must stay quiet, proves it.

```
bash .claude/hooks/pre-write-ratchets.test.sh      # 12 bien, 0 mal
```

Siete casos que deben denegar —añadir una fila, borrarla, cambiar su texto, cortar a media fila, un
`Write` con una fila de más, otro con una de menos, y cualquier escritura a `coverage-debt.txt`— y
cinco que deben dejar pasar —dos correcciones de comentario, un `MultiEdit` con tres, un `Write` que
sólo cambia comentarios, y un `Edit` sobre otro fichero—.

**Y dos mutantes, sin los cuales la batería no verifica nada:**

| Mutante / Mutant | Resultado / Result |
| --- | --- |
| Nunca deniega (`deny` emite `allow`) / Never refuses | rompe los **7** que deben sonar |
| Los comentarios cuentan como filas / Comments count as rows | rompe **2** de los 5 que deben callar |

**El primer intento de mutante salió IDÉNTICO al original** porque el `sed` buscaba
`permissionDecision":"deny`, que no existe en el fuente: el JSON lo construye `jq` sin esas comillas.
Dio 12 de 12 y no medía nada. Lo cazó el control que compara el mutante con el original antes de
creerse el resultado, y por eso ese control está dentro del guion. / **The first mutant came out
IDENTICAL** to the original because the `sed` pattern did not match: `jq` builds the JSON without
those quotes. It scored 12 of 12 and measured nothing. The check that diffs the mutant against the
original caught it, which is why that check now lives inside the script.

## Medido también en la aplicación / Measured in the application too

La tubería prueba el guion; no prueba que el harness lo enrute. Medido con el par de controles dentro
de la sesión, **y sin recargar los hooks**: el `Edit` que corrige el comentario de la línea 5 **pasó**,
y el `Edit` que añade `AudioOutputView#ControlDePruebaQueNoDebeEntrar` **quedó denegado** con el
mensaje nuevo. Después, `grep -c` sobre el fichero confirma que esa fila **no entró** y que siguen
siendo 23. / The pipe tests the script; it does not test that the harness routes it. Measured with
the pair of controls inside the session, **without reloading hooks**: the comment fix passed, the
row addition was refused, and the file still holds 23 rows.

## Las puertas / The gates

| Puerta / Gate | Resultado / Result |
| --- | --- |
| `eng/check-walk-coverage.ps1 -SkipRun` | 278 controles, 246 pulsados, **23 pendientes** |
| `eng/verify-docs.ps1` | 323 ficheros, 38 localizados |
| `DocumentationTests` | **118 de 118** |
| `dotnet format --verify-no-changes` | 0 |
