# La página de apariencia sabía si Windows anima y decía lo mismo en los dos casos / The appearance page knew whether Windows animates and said the same thing either way

Primer trabajo del tramo 5 de la §4, **y el primero cuya discrepancia con el documento no la decide una
medición sino una decisión que el árbol ya tenía escrita**. / §4's fifth tranche, and the first whose
disagreement with the document is settled by a decision the tree had already written down rather than
by a measurement.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Los 3 botones de tema no pasan a 5 / Three theme buttons do not become five

La §4 abre la fila de Ajustes con `De 3 botones de tema a 5`. **`ThemePreference` tiene exactamente
tres valores**, y `Theme/ThemePreference.cs` lleva la razón escrita justo encima de los dos de alto
contraste: / `ThemePreference` has exactly three values, and the reason sits above the two
high-contrast variants:

> «son un estado, no una cuarta elección: las tres píldoras de Apariencia se quedan como están, y cuál
> de estas se aplica **se lee del sistema** en vez de elegirse»

El alto contraste de Windows es un ajuste de accesibilidad **del sistema**. Una aplicación que ofrece su
propio selector o lo ignora o lo duplica, y en los dos casos alguien acaba con un tema que no pidió. /
An application offering its own high-contrast picker either ignores the system setting or duplicates it.

**Sexta discrepancia §4↔árbol, y la primera de su clase.** Las cinco anteriores las decidió un número;
ésta la decide una decisión ya razonada. **Antes de «cumplir» una línea del paquete, mira si el árbol ya
razonó lo contrario.** Y se afirma con prueba —una píldora por valor del `enum`, ni una más— para que la
decisión no se deslice de vuelta. / Asserted with a test so the decision cannot drift back.

## El `WrapPanel` sí va, pero por la otra razón / The WrapPanel does go in, for the other reason

La §4 lo justifica con «cinco botones no caben en los 620 px de `MaxWidth`». **Con tres, eso es falso**,
y está medido a 900 px de ventana: / Measured in a 900-wide window:

```
es-ES  fila de 3: [84, 72, 83]  total=263  columna=620
es-ES  fila de 2: [83, 83]      total=178  columna=620
en-US  fila de 3: [79, 70, 68]  total=241  columna=620
en-US  fila de 2: [83, 83]      total=178  columna=620
```

Sobran **357 px**. Pero la forma se cambia igual, y la razón es la que este árbol lleva medida **ocho
veces**: un `StackPanel` horizontal **ofrece a sus hijos anchura infinita** y los dibuja donde caigan.
Y hay algo más aquí: **el largo de estas etiquetas lo decide quien traduce**, no este archivo. **Son dos
filas, no una** — la §4 sólo nombra la de temas y la de idiomas tiene la misma forma. / The length of
these labels belongs to a translator, and there are two such rows, not the one §4 names.

**Una razón falsa no convierte en falsa la conclusión, y una conclusión correcta no valida la razón.**
Las dos se escriben.

## Y el defecto: la página tenía la respuesta y decía lo mismo siempre / And the defect: the page had the answer and said the same thing regardless

El aviso de movimiento reducido decía **una frase fija** —«AP Reelume respeta la preferencia de
reducción de movimiento de Windows»— estuviera activa o no. Es una frase sobre las intenciones de la
aplicación, no sobre el estado de la máquina. / A sentence about the application's intentions rather
than about the machine.

**Y la respuesta ya estaba en la vista**: `AppearanceSettingsViewModel` sostiene `IThemeService`, cuyo
`AnimationsEnabled` es exactamente `!IReducedMotionService.IsEnabled`. La página **tenía** el servicio
que lo sabe y no lo gastaba. No hizo falta ninguna dependencia nueva ni tocar el contenedor. / The page
held the service that knew and spent nothing with it.

Ahora hay dos frases y el modelo elige **la clave**, no las palabras —igual que viajan los motivos de
recomendación—, así que el texto sigue al idioma en vez de decidirse cuando se leyó la propiedad. Las
dos van en los dos idiomas, y la prueba las afirma **en las dos direcciones**: un aviso que dijera
siempre «activo» cumpliría la mitad. / The model picks the key and not the words, and the test asserts
both directions.

## Y lo que la puerta de cobertura destapó de paso / And what the coverage gate turned up on the way

La segunda vuelta de CI pidió subir **dos** suelos, y el segundo dice algo:
`WindowsReducedMotionService.cs` estaba en **0/0** y pasa a **87/50**. Cero significa que **nada en toda
la suite preguntaba jamás si el movimiento reducido estaba activo**. / Zero means nothing in the whole
suite ever asked whether reduced motion was on.

El servicio estaba registrado, tenía consumidor —`FluentThemeService`— y ese consumidor sólo lo leía
desde `AnimationsEnabled` y `MotionDuration`, **que nadie llamaba**. Es el defecto de la casa en su
forma más callada: no un registro sin resolver, sino **una cadena entera de resolución cuyo último
eslabón no ejercía nadie**. Darle al aviso su primer lector real le dio también su primera medición. /
A whole resolution chain whose last link nobody exercised.

El otro suelo, `AppearanceSettingsViewModel.cs`, sube de **97/65 a 97/66**: la propiedad nueva llegó con
sus dos ramas cubiertas. Los dos se levantan **copiando entero el artefacto `coverage-debt` del run**,
que es la única forma que la puerta admite. / Both floors are raised by copying the run's artefact
whole, which is the only way the gate accepts.

## El verde / The green

```
UiTests             663/663
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
ArchitectureTests   30/30
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```

Sin archivo nuevo en `src/`, así que la puerta de archivos nuevos no entra: lo tocado es un modelo que
ya está en `eng/coverage-debt.txt` con suelo **97/65**, y lo añadido es **una línea y dos ramas, las
dos cubiertas**, que sólo puede subirlo. / No new file, so the new-file gate does not apply.
