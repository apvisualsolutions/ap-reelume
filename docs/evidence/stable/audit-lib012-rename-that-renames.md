# El renombrado que renombra / The rename that renames

`LIB-012` estaba en `BLOCKED` porque una función cuyo titular es «renombrar» no podía renombrar nada.
Ya puede, y los tres controles de su superficie se pulsan con el ratón leyendo el efecto **del sistema
de archivos**. / `LIB-012` was blocked because a feature titled "rename" could rename nothing. It can
now, and its three controls are pressed with the mouse with the effect read off **the file system**.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## La primera medición, antes de escribir nada / The first measurement, before writing anything

La pregunta que decidía el convenio: **¿cuántas fichas producirían un nombre distinto del actual?** Si
la respuesta fuera «casi ninguna», el convenio estaría mal elegido. / The question that decided the
convention: if almost nothing changed, the convention would be the wrong one.

| Corpus | Cambian / Changed | Ya cumplen / Already right | Sin propuesta / No proposal |
|---|---|---|---|
| 12 nombres aprobados / 12 approved names | **8** | 1 | 3 |

El corpus es `tests/.../Fixtures/media-name-cases.json`, el conjunto aprobado de **formas** que este
proyecto reconoce —`S01E02`, `s1e2`, `1x02`, `Cap.803`, `Temporada 2 Episodio 03`, separadores por
puntos y guiones bajos, etiquetas de ruido, unicode—, medido con el analizador real y con el
componente de producción, no con una reimplementación. / The corpus is the approved set of **shapes**,
measured with the real parser and the real production component.

**Lo que la medición no es:** no hay biblioteca real que medir. La del perfil local de esta máquina
tiene **2 archivos y cero fichas**, así que no aporta nada; n=12 son formas, no volumen. / **What the
measurement is not:** there is no real library to measure here — the local profile holds 2 files and
zero entries.

El único que sale igual es `東京物語 (1953).mkv`, que **ya sigue el convenio**. Que un archivo bien
nombrado no se toque es la señal de que el convenio elegido es el correcto. / The one unchanged name
already follows the convention, which is the signal that the convention is right.

## Lo que se escribió / What was written

**`TitleFileNamePolicy`**, pura, en `Domain/Discovery`, con el convenio que comparten Plex, Jellyfin y
Kodi —el mismo que este proyecto ya sigue en `TrailerDiscoveryPolicy`, así que no se inventa nada—:

- `Título (Año).ext` para una película, y `Título.ext` sin año.
- `Serie (Año) - SxxEyy - Título.ext` para un episodio, sin la tercera parte cuando nadie sabe el
  título del episodio, y sin el paréntesis cuando nadie sabe el año.
- **No sanea y no resuelve colisiones.** `RenamePolicy` es la dueña de ambas cosas y no se tocó: dos
  saneadores serían dos opiniones sobre qué es seguro escribir. / It neither sanitizes nor resolves
  collisions; `RenamePolicy` owns both and was not touched.
- Su respuesta puede ser **nada**. Una ficha que nadie ha identificado, cuyo título es el nombre del
  archivo, no tiene un nombre mejor que proponer. / Its answer is allowed to be nothing.

**El llamante**, en `OpenRenameAsync`, que antes pedía `Path.GetFileName(file.Path)` —el nombre que el
archivo ya tenía— y por eso el plan salía siempre vacío. / The caller, which used to ask for the name
the file already had.

## Dos reglas que salieron de medir, no de deliberar / Two rules that came out of measuring

**Un analizador que avisa no propone.** `Tomorrow.9999.mkv` se lee como «Tomorrow»: el analizador
descarta el 9999 por no ser un año posible y su título limpio ya no lo lleva, así que renombrar a lo
que leyó **tiraría parte del único nombre que existe**. Los tres nombres del corpus que el analizador
no reconoce llevan los tres su advertencia, así que la regla no cuesta ni un caso bueno. / A parser
that warned is a parser saying it does not know what this is, and its clean title has already dropped
what confused it.

**El título y el año viajan juntos desde una fuente, nunca uno de cada.** Una ficha identificada cuyo
título sigue siendo el nombre del archivo lleva el año **dentro** del título; emparejarlo con el año
que el analizador encontró lo escribe dos veces. Medido en la ejecución completa de la suite:
`Arrival 2016 (2016).mp4`. La temporada y el episodio son la excepción, porque **ninguna ficha los
guarda**: viven sólo en el nombre. / Title and year travel together from one source: pairing them
wrote `Arrival 2016 (2016).mp4`, caught by the full suite run.

## El paseo, con el efecto en el disco / The walk, with the effect on the disk

La escena siembra un archivo llamado `Arrival.2016.1080p.mp4` y una ficha que dice «La llegada» y
2016. Que el destino sea `La llegada (2016).mp4` sólo puede venir de la ficha. / The file says one
thing and the entry says another, so the destination can only have come from the entry.

1. **Consentimiento** — la casilla, pulsada; después `ExecuteCommand` se puede ejecutar.
2. **Renombrar** — la sonda es `File.Exists(destino)`, y el origen deja de existir: se movió, no se
   copió.
3. **Deshacer** — la sonda es `File.Exists(origen)`, y el destino desaparece. El consentimiento se
   pide **otra vez**, porque ejecutar lo borra: un segundo movimiento irreversible merece una segunda
   decisión. / Undo asks for consent again, because executing cleared it.

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 30 | **33** |
| Pendientes / Pending | 98 | **95** |

Las tres líneas de `eng/walk-pending.txt` que decían `blocked: nothing composes a desired name` ya no
están, y el trinquete de `eng/check-walk-coverage.ps1` bajó con ellas. / The three blocked lines are
gone and the ratchet came down with them.

## Y una duplicación que no se hizo por tercera vez / And a duplication not made a third time

`FileNameContext` —el nombre del archivo más las carpetas entre él y la raíz— se construía con el
mismo bloque de seis líneas en `IdentifyScannedFiles` y en `GroupScannedVersions`. El renombrado
habría sido la tercera copia, así que la construcción subió al propio tipo como
`FileNameContext.ForFile`, y los tres la usan. Sin cambio de comportamiento: lo sostienen las suites
que ya cubrían a los dos primeros. / The third copy was not made; the construction moved onto the type
itself, with no behaviour change.

## Las puertas / The gates

`dotnet format --verify-no-changes`, compilación con `-warnaserror`, y las suites de dominio (442),
aplicación (223), arquitectura (26), interfaz (439), integración (443) y accesibilidad (86), más
`eng/check-walk-coverage.ps1`. / All green.
