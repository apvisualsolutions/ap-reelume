# Una prueba deja de borrar un archivo que otra está leyendo / A test stops deleting a file another one is reading

Evidencia del rojo de CI del **2026-08-14** en `main`, con el mismo commit que había pasado en la
rama. / Evidence for the CI red of **2026-08-14** on `main`, with the same commit that had passed on
the branch.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que dijo CI / What CI said

```
SegmentCorpusTests.An_episode_materialises_from_nothing_with_the_expected_duration [FAIL]
  System.IO.IOException : The process cannot access the file
  '…\artifacts\test-media\segments\S03\S03E03.mkv' because it is being used by another process.
     at System.IO.File.Delete(String path)
```

**Segunda aparición, idéntica.** La primera está archivada en el plan (2026-08-09, run 31307701534,
relanzada y en verde), y aquella vez se anotó como aparición sin corregir nada, que es lo correcto
para una sola. Dos veces el mismo archivo y la misma línea ya no es una aparición: es una carrera con
dueño conocido. / Second appearance, identical. The first was archived as an appearance, which is the
right treatment for one; twice on the same file and the same line is a race with a known owner.

**Y no era el rojo que se estaba vigilando**: `first-launch` pasó en esa misma ejecución —ventana en
3340 ms, 16 migraciones, código de salida 0—, así que el intermitente instrumentado la sesión pasada
sigue sin reaparecer. / And it was not the red being watched: `first-launch` passed in that very run.

## La causa / The cause

El corpus de segmentos es **compartido** por cinco archivos de prueba de este proyecto, y las clases
de prueba corren en paralelo. Esta prueba borra `S03E03.mkv` a propósito —comprobar que el generador
puede partir de cero exige que no haya nada— mientras cualquier otra puede estar sondeándolo con
LibVLC. En esta máquina no se reproduce; en un runner hospedado, dos veces. / The corpus is shared by
five test files and test classes run in parallel: this one deletes the episode on purpose while
another may be probing it.

## La corrección / The fix

Quitar la carrera, no reintentar contra ella: la prueba produce en **una carpeta propia**
(`segments-from-nothing/`) en vez de en el corpus compartido. Lo que afirma es que el generador
arranca sin artefactos previos, no en qué carpeta escribe, así que el aislamiento no le cuesta nada
a lo que mide. Un reintento con espera habría dejado la carrera puesta y sólo habría cambiado su
frecuencia. / Remove the race rather than retry against it: the test produces into a folder of its
own. What it asserts is that the generator starts from nothing, not which folder it writes to.

## Verde / Green

| Puerta / Gate | Resultado / Result |
|---|---|
| `SegmentCorpusTests` | 5 de 5 / of 5 |
| `eng/verify.ps1` completo / full | verde / green |
