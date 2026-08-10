# Dos endurecimientos que la auditoría dejó anotados / Two hardenings the audit left noted

La auditoría los archivó como **no explotables** y por eso llevaban meses esperando. Al medirlos, uno
resultó ser mucho peor de lo anotado y el otro no ser lo que decía. / The audit filed both as **not
exploitable**. Measured, one turned out far worse than noted and the other turned out not to be the
thing it was described as.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-10.

## El lanzador externo entregaba al shell lo que le dieran / The external launcher handed the shell whatever it was given

`ShellExternalPlaybackLauncher` es el único sitio del programa que le pasa una ruta a Windows para
que la abra con su manejador registrado. La nota decía «revalidar la extensión en vez de confiar en
que todos los llamantes ya filtran». / It is the one place in the program that hands a path to
Windows to open with its registered handler.

**El rojo, medido, no supuesto:** de cinco extensiones que la biblioteca no cataloga, **tres llegaron
al shell y abrieron su manejador**. / **The red, measured rather than assumed**: of five extensions
the library does not catalogue, **three reached the shell and opened their handler**.

| Extensión / Extension | Antes / Before | Después / After |
|---|---|---|
| `.ps1` | **abrió su manejador / opened its handler** | rechazada / refused |
| `.txt` | **abrió su manejador / opened its handler** | rechazada / refused |
| *(sin extensión / none)* | **abrió su manejador / opened its handler** | rechazada / refused |
| `.exe` | rechazada por el shell / refused by the shell | rechazada / refused |
| `.lnk` | rechazada por el shell / refused by the shell | rechazada / refused |

Que `.exe` y `.lnk` salieran en falso no era una defensa: fue el shell negándose a arrancar un
archivo de un byte, no el programa negándose a pedírselo. / That `.exe` and `.lnk` came back false was
not a defence: it was the shell refusing to start a one-byte file, not the program refusing to ask.

La comprobación es ahora la lista del dominio, `MediaFileExtensions`, la misma que decide qué
cataloga el escáner — una lista, no dos, que es la razón por la que esa clase existe. /
The check is now the domain's own list, the same one that decides what the scanner catalogues.

`ExternalPlaybackLauncherTests`: 9 de 9. La mitad positiva no se conduce a propósito: esta suite no
arranca ningún manejador, y una prueba que abriera un reproductor en la máquina que la ejecuta sería
peor que la cobertura que compra. / The positive half is deliberately not driven: this suite starts no
handler.

## El ZIP de backup: la nota describía un riesgo que no era ése / The backup ZIP: the note described a risk that was not the one there

La nota decía «acotar la copia al tamaño declarado, porque hoy los topes se apoyan en un dato que el
propio archivo declara de sí mismo». Se implementó la copia acotada y **la prueba salió verde antes
de la corrección**, que es la señal de que la hipótesis estaba mal. / The note said to bound the copy
by the declared size. The bounded copy was implemented and **the test passed before the fix**, which
is the sign that the hypothesis was wrong.

Se forjó un archivo cuyo directorio central declara **un byte** para una entrada que contiene una base
de datos entera, parcheando los dos sitios donde un ZIP registra el tamaño. Resultado medido: el
marco devuelve **un byte y termina**. Una entrada no puede entregar más de lo que declara, así que
acotar la copia por la declaración no defendía de nada. / An archive was forged whose central
directory declares **one byte** for an entry holding a whole database. Measured result: the framework
hands back **one byte and stops**.

Eso deja la declaración como toda la exposición, y ahí sí había un hueco: los topes
—512 MB por entrada, 2 GB en total— los aplicaba `BackupValidator`, que es **un paso que el llamante
tiene que acordarse de dar**, y `ExtractAsync` es público. Un archivo que declarase cuatro gigabytes
y llegara sin inspeccionar se desempaquetaba entero. Ahora la extracción aplica los mismos dos topes
por su cuenta. / That leaves the declaration as the whole exposure, and there the gap was real: the
ceilings were enforced by the validator, a step a caller has to remember, while `ExtractAsync` is
public. Unpacking now applies both ceilings itself.

`RestoreValidationTests`: 28 de 28, dos pruebas nuevas. La segunda fija el comportamiento del marco
del que ahora depende el razonamiento, porque una defensa que descansa en una suposición sobre una
biblioteca ajena es una defensa que nadie ha comprobado. / The second new test pins the framework
behaviour the reasoning now rests on, because a defence resting on an assumption about somebody
else's library is a defence nobody has checked.

## Lo que esto deja dicho / What this leaves said

Un hallazgo archivado como «no explotable» sigue siendo un hallazgo **sin medir**. Uno de los dos
resultó ser una entrega directa al shell de Windows de cualquier archivo que un llamante futuro le
pasara; el otro resultó no existir en la forma descrita, y sólo se supo forjando el archivo que lo
habría explotado. / A finding filed as "not exploitable" is still a finding **not measured**. One of
these two turned out to be a direct hand-off to the Windows shell; the other turned out not to exist
in the form described, and that was only learned by forging the archive that would have exploited it.
