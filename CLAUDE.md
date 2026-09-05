# AP Reelume — guía para agentes

Biblioteca de medios local para Windows 11: cataloga y reproduce vídeos que ya están en el disco de
quien la usa. Sin cuentas, sin telemetría, sin servidor. `GPL-3.0-or-later`.

Este archivo es para un agente que llega al repositorio sin contexto. Está en español porque el
proyecto se piensa en español y se publica en dos idiomas; el código, los commits y los nombres de
prueba van en inglés.

El orden es deliberado: **primero lo que hace falta para tocar el código**, y al final
[lo que el repositorio automatiza por ti](#lo-que-el-repositorio-automatiza-por-ti) — hooks, MCP,
skills y agentes—, que corre solo y se consulta cuando algo salta. Estuvo arriba hasta el 2026-08-30
y eran noventa y cinco líneas de herramientas antes de la primera sobre la aplicación.

## Regla 0, inquebrantable: la documentación antes que nada

**Antes de afirmar cómo se comporta una herramienta, y antes de proponer cambiar una regla de este
repositorio porque «no contempla mi caso», se lee su documentación.** No es una sugerencia ni un
último recurso: es el primer paso, delante incluso de la lista de lectura de abajo.

**El caso más frecuente es Avalonia y tiene su MCP**, y por eso ocupa el resto de esta sección. Pero
la regla es más ancha, y el 2026-09-02 costó una propuesta entera: la puerta de archivos nuevos
rechazaba un adaptador de audio que no podía llegar al listón, y estaba a punto de proponerse
**ensancharla**. El propietario lo paró con una frase —«antes de tomar ninguna decisión necesito que
te asegures, documéntate»— y la documentación de coverlet contestó en una consulta que el mecanismo
ya existía, que no hacía falta tocar ninguna guarda, **y** que aplicarlo a lo bruto habría sido el
arreglo equivocado. Ver la regla 10.

**Para lo que no tiene MCP hay `firecrawl` y `context7`**, y el orden es el mismo: la documentación
de la herramienta antes que el razonamiento sobre ella. Un razonamiento correcto sobre una premisa
que nadie comprobó es la forma más cara de equivocarse que tiene este repositorio.

Las tres herramientas, y cuándo va cada una:

- `mcp__avalonia-docs__get_avalonia_expert_rules` — **al empezar cualquier sesión que toque la
  presentación.** Trae de una vez la sintaxis AXAML, el sistema de propiedades, los selectores y los
  errores habituales.
- `mcp__avalonia-docs__lookup_avalonia_api` — **antes de usar un tipo o una propiedad**: `TextBlock`,
  `Shape`, `Stretch`, `DockPanel`. Si devuelve «no results», eso también es un dato: significa que
  hay que buscar por la otra vía antes de suponer.
- `mcp__avalonia-docs__search_avalonia_docs` — **antes de afirmar por qué algo se ve como se ve.**

### Por qué está escrita, con la factura del día que se aprendió

El 2026-08-28, midiendo por qué los botones se veían desalineados, se supuso el comportamiento del
framework en lugar de consultarlo. Lo que costó:

- **Una hipótesis falsa perseguida hasta el final**: que el render ajustaba la línea base a la
  rejilla de píxeles. Cuando por fin se consultó, la página de `TextOptions` contestó en **una sola
  llamada** que `BaselinePixelAlignment` y `TextHintingMode` existen, qué hacen y cuáles son sus
  valores por defecto — y la medición confirmó que **ninguno de los cuatro modos cambia un solo
  píxel** aquí. La consulta habría descartado la hipótesis antes de escribir el arnés.
- **Seis vueltas de compilación** adivinando la superficie de la API: `IGlyphTypeface` no es público,
  `FontMetrics` no expone `CapHeight`, `GlyphTypeface` no lleva `GetGlyphMetrics`, `Shape` no expone
  `TranslatePoint` sin castear a `Visual`. Cada una fue un error de compilación que una consulta
  contesta antes.

**Y la regla no termina en la consulta: lo que el MCP diga se mide.** El día que se escribió esto, la
documentación era correcta y la hipótesis que la motivó era falsa de todos modos. La consulta evita
inventarse la API; sólo la medición dice qué pasa en esta aplicación.

**El corolario que ese día dejó**, y que vale para cualquier vista: **medir el layout no es medir lo
que se ve.** `Bounds`, `TranslatePoint` y las métricas de una fuente describen el modelo; el defecto
puede vivir sólo en el píxel. Cuando lo que se investiga es algo que alguien **ve**, se rasteriza —
`window.CaptureRenderedFrame()`, que aquí funciona porque `TestAppBuilder` levanta Skia de verdad con
`UseHeadlessDrawing = false`— y se cuentan píxeles. Dos puertas de este repositorio estaban verdes
sobre dos píxeles de desalineación visible por medir el modelo en vez de la tinta.

## Antes de tocar nada, lee esto en este orden

1. [docs/FEATURES.md](docs/FEATURES.md) — el registro **canónico** del alcance: qué existe, en qué
   estado y con qué evidencia. Si algo contradice esta guía, manda la matriz.

   **Para «qué falta» no se lee a mano: `pwsh -NoProfile -File eng/list-pending.ps1`.** Son 71 <!--medido:identificadores-de-alcance-->
   filas
   en seis tablas, con los dos idiomas dentro de cada celda y lo hecho mezclado con lo que no, y
   leerlas a ojo es cómo se pierden. El 2026-08-31 se perdieron **ocho de golpe** —`UX-007` entre
   ellas, que era justo la que se preguntaba— porque el patrón escrito a mano pedía tres mayúsculas
   y `UX` tiene dos. El guion **no es un segundo registro**: no guarda lista propia, así que no
   puede desviarse de la matriz. Y **no puede callar**: lee los estados y las versiones de las dos
   leyendas del propio documento, cuenta las filas por dos caminos distintos y exige que cuadren, y
   ante cualquier fila que no entienda **se niega a imprimir** en vez de imprimir una lista más
   corta. Acepta `-Target MVP` y `-Json`.
2. [docs/NEXT-SESSION.es.md](docs/NEXT-SESSION.es.md) — dónde se retomó por última vez.
3. [CONTRIBUTING.md](CONTRIBUTING.md) — el ciclo de trabajo, que no es opcional.
4. [docs/legal/LEGAL.es.md](docs/legal/LEGAL.es.md) — lo que está resuelto y lo que sigue abierto.

## Arranque

El SDK no está en el `PATH` del sistema:

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet --version   # la versión que fija global.json
```

Toda ejecución de pruebas sobre la solución lleva `-m:1 --settings eng/test.runsettings`. Sin eso,
las suites que tocan SQLite y LibVLC compiten entre sí y producen rojos que no son del código.

**Y para verla correr**, que es lo que este archivo no decía y `CONTRIBUTING.md` tampoco:

```powershell
dotnet run --project src/ApSolutions.LocalMedia.Windows -c Debug
```

Es `WinExe` sobre `net10.0-windows10.0.22621.0`: **abre una ventana y no vuelve**, así que no se
lanza desde una sesión que no pueda cerrarla. Y arrancarla **no es medir**: para algo que alguien
**ve** se rasteriza y se cuentan píxeles (regla 0), porque `Bounds` describe el modelo y el defecto
puede vivir sólo en el píxel.

## El ciclo, en corto

**Rojo archivado → corrección mínima → verde con las puertas → evidencia → changelog en dos idiomas
→ un commit.**

Las puertas, todas, antes de cada commit:

```powershell
dotnet format --verify-no-changes --severity warn
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
dotnet test <suite afectada> -c Release -m:1 --settings eng/test.runsettings
pwsh -NoProfile -File eng/verify-docs.ps1
```

`eng/verify.ps1` las corre todas más el empaquetado y la puerta de cobertura.

**Empaquetar y publicar** no es parte del ciclo por commit, y por eso se olvida que existe:

```powershell
pwsh -NoProfile -File eng/package-x64.ps1      # el MSIX y el ZIP independiente
pwsh -NoProfile -File eng/verify-package.ps1   # su ciclo de vida, y que dos builds de un commit coincidan
pwsh -NoProfile -File eng/prepare-release.ps1  # contesta si este árbol podría publicarse, y produce la release
```

**Y desde el 2026-08-31 hay una regla del propietario por encima de ese guion: no se publica nada
hasta que todo lo comprometido esté verificado.** No se corta una primera publicación parcial para
mejorarla después; las tres versiones siguen ordenando en qué orden se construye y ya no autorizan
publicar al terminar la primera. Cuenta todo lo que la matriz trata como compromiso —incluidas las
`DEFERRED`, que son aplazadas y no rechazadas— y **no** cuenta lo `OUT_OF_SCOPE`, que es una decisión
escrita de no hacerlo. Está en la
[hoja de ruta](docs/roadmap/README.es.md) con sus dos bloqueos duros: `PRD-003` pide una máquina
ARM64 que no hay, y `PRD-002` pide el certificado comercial de firma.

`package-arm64.ps1` **no produce nada publicable todavía**, y desde el 2026-09-04 el motivo ya no es
el que decía esta línea. Decía «PRD-003 está BLOCKED por hardware»: **la máquina existe y es
prestada**, porque los runners `windows-11-arm` de GitHub son gratis e ilimitados en repositorios
públicos. Un trabajo de CI corre ahí la matriz y **cinco de sus seis fases pasan**; la que falta pide
que el trabajo x64 le pase su carpeta de datos. **Cómo se lanza, cómo se lee su resultado —que NO es
el color del run— y sus cinco trampas están en
[docs/development/arm64-ci.es.md](docs/development/arm64-ci.es.md), y se lee antes de tocar nada que
dependa del sistema operativo.**

**Aquí, sobre un anfitrión x64, la matriz sigue contestando «6 de 6 fases no pasadas», y eso es
correcto**: la rama que las ejecuta sólo corre cuando el anfitrión es ARM64. **Pero sí hay que
correrlo al tocar el manifiesto**, porque `Arm64PackageTests` compara el manifiesto empaquetado con
el fuente: el 2026-08-31 ese artefacto llevaba nueve días caducado —era del 22 de agosto y el color
de fondo cambió el 24— y daba un rojo local que nadie perseguía.

Tocar el manifiesto caduca además dos mediciones del sandbox, así que después toca rehacer su ciclo.
**Los informes se comparan con los archivados antes de sustituirlos**: lo caducado es la huella, no
necesariamente la medición, y una diferencia inesperada ahí es un hallazgo.

**Una sola prueba**, que es lo que se quiere mientras se persigue un rojo — segundos en vez de
minutos:

```powershell
dotnet test tests/ApSolutions.LocalMedia.Domain.Tests -c Release --no-build -m:1 `
  --settings eng/test.runsettings --filter "FullyQualifiedName~El_nombre_de_la_prueba"
```

`--no-build` sólo después de haber compilado, o se mide el binario anterior y el resultado miente.

**Y «la suite afectada» es quien LEE el archivo, no quien está en su carpeta.** Diez suites, y la
elección se equivoca hacia abajo con facilidad —tocar el shell rompió una obligación de TMDB en
`IntegrationTests`—:

| Suite | Qué cubre | Coste |
| --- | --- | --- |
| `Domain.Tests` | políticas puras | < 1 s |
| `Application.Tests` | casos de uso y puertos | ~ 1 s |
| `ArchitectureTests` | las cinco reglas, red declarada, servicios huérfanos | ~ 2 s |
| `DocumentationTests` | bilingüismo y matriz de alcance | < 1 s |
| `UiTests` | AXAML, ViewModels, las 61 vistas <!--medido:vistas--> | ~ 1 min |
| `AccessibilityTests` | recorrido y paseo autónomo | ~ 5 min |
| `IntegrationTests` | SQLite, sistema de archivos, TMDB | ~ 7 min |
| `MediaTests` | LibVLC con vídeo real | ~ 7 min |
| `PackagingTests` | MSIX y firma | ~ 10 s |
| `PerformanceTests` | presupuestos de tiempo | ~ 2,5 min |

`PackagingTests` da **30 rojos aquí y verde en CI** —le faltan `lifecycle.json` y
`reproducibility.json` en `artifacts/package`—, así que no se persigue en local.

**Pero esos rojos son por artefactos ausentes, no por esta máquina, y se cierran cuando el trabajo ya
paga el ciclo de empaquetado.** El 2026-08-31, tras `package-x64.ps1` + `verify-package.ps1 -Mode
Verify` + `package-arm64.ps1`, la suite dio **194 de 194**. Si tocas el manifiesto ya estás pagando
ese ciclo: entonces sí se corre entera, porque es la única que mide lo que cambiaste.

**Y para mirar CI se usa `eng/watch-ci.ps1`, nunca un bucle escrito a mano.**

```powershell
pwsh -NoProfile -File eng/watch-ci.ps1 -Sha <sha>
```

Emite una línea por desenlace y **los cinco están cubiertos**: la conclusión literal —incluida una
vacía—, que el push no disparara el flujo, que `gh` falle, un latido cada 30 minutos mientras corre,
y un techo a los 120 que avisa y sale.

**Y desde el 2026-09-03 avisa también del progreso, que no es un desenlace**: una línea por cada paso
que termina, en cuanto termina. El paso pesado de este flujo dura más de media hora, así que un fallo
dentro de él **sólo se conocía por la conclusión del run, cuarenta minutos después**. Son pasos y no
trabajos porque este flujo tiene **un solo trabajo**, así que un aviso por trabajo llegaría en el
mismo segundo que el final y no adelantaría nada — medido contra un run vivo, junto con lo que lo
hace posible: `gh run view <id> --json jobs` devuelve cada paso con su estado **mientras el run sigue
en curso**. El andamiaje del runner se filtra mientras pasa y **nunca cuando falla**, y cada paso se
anuncia **una sola vez**: el guion mira cada minuto durante tres cuartos de hora, y un aviso que suena
cuarenta veces es el que enseña a ignorarlo. El motivo de que sea un guion y no un bucle en el momento:
el filtro obvio pregunta por `status == "completed"` y **calla en todo lo demás**, y un vigía callado
es indistinguible de un run que sigue. Peor aún, `2>/dev/null` sobre la consulta **entierra** el error
de `gh` y `|| true` lo convierte en una cadena vacía que se lee como «aún no ha terminado».

**Cuánto tarda un run NO se escribe aquí: se mide cuando hace falta.**

```powershell
pwsh -NoProfile -File eng/measure-ci-time.ps1              # la duración de hoy, y el margen que queda
pwsh -NoProfile -File eng/measure-ci-time.ps1 -Detailed    # y en qué suite se va el tiempo
```

**Ésta es la cifra que más veces ha estado mal en este archivo, y por eso ya no vive en él.** Dijo
55-80, luego 42-53, luego 49-57, y el 2026-09-04 eran 41-43 — cuatro cifras en cinco días, cada una
correcta el día que alguien la midió y ninguna correcta después. El daño no es la imprecisión: es que
**el criterio escrito al lado depende de ella**. «Cortado a los 90 es un atasco» se lee muy distinto
según si un run sano tarda 43 o 86, y con la cifra vieja delante un rojo por reloj se persigue como
si fuera un cuelgue. El propietario lo zanjó el 2026-09-04: **si un dato siempre va a estar
desfasado, no se guarda — se mide en el momento en que alguien lo necesita.**

El guion contesta las dos preguntas que la cifra pretendía contestar —cuánto tarda y cuánto margen
queda hasta el corte, que lee del propio flujo en vez de repetirlo— y **avisa solo** cuando el margen
baja de diez minutos. Subir el techo es la respuesta cómoda; el propio comentario del flujo dice lo
contrario, que un run sano acercándose al corte significa que el trabajo ha crecido.

**El mecanismo de `<!--medido:clave-->` no sirve para esto y conviene saber por qué**: mide el árbol,
y una duración vive en el servidor. Una prueba que fuera a buscarla abriría una conexión que ninguna
finalidad declara, que es la regla 2. De ahí que sea un guion y no una puerta.

**Y una lectura no es una tendencia**, que es lo que el guion repite al pie: la suite de integración
ha dado **7,5 y 27 minutos el mismo día con el mismo trabajo**.

**Y ese 27 NO fue un runner lento**, que es lo que se dedujo de una lectura: en ese mismo run las
otras nueve suites fueron normales o más rápidas. Sólo la de integración se multiplicó por 3,6, así
que es contención dentro de ella. Las cuatro pruebas de volumen se llevan **el 20 % del trabajo**, la
misma proporción en el run bueno y en el malo; lo que se dispara son **220 pruebas que abren base de
datos**, que pagan un peaje fijo de casi 22 segundos hagan lo que hagan.

**El corolario práctico: un rojo por tiempo en esa suite puede ser el sorteo del runner y no tu
cambio.** Antes de perseguirlo, compara con otro run del mismo día.

**Ese recorrido corría CUATRO veces por run y desde el 2026-09-03 corre tres**: una en la
verificación, dos como puerta —seguidas y sobre el mismo código— y una cuarta que ya no ocurre,
porque el trinquete del paseo lee con `-SkipRun` el informe que la puerta acaba de dejar —el paseo
escribe en `artifacts/walk` con o sin variable, que es lo que lo hace posible—, **0,5 s en vez de
2m39s y el veredicto idéntico**. Cuántas corre hoy y cuánto cuestan entre sí lo dice
`measure-ci-time.ps1 -Detailed`, en la columna `Passes`; sigue siendo la partida más cara del run.

**Y sacarla de la verificación NO es gratis, medido el mismo día y con un rojo.** Parecía el ahorro
grande —8,8 minutos— porque la puerta corre esa misma suite dos veces justo después. Pero esa suite
monta la aplicación entera y recorre todas las vistas, así que **es lo único que cubre buena parte de
`Presentation` EN ESA PASADA**, que es la pasada que la puerta de cobertura mide. Al quitarla cayeron
media docena de archivos de golpe —`ScanSettingsViewModel` de 81/50 a **18/0**— y la puerta lo
rechazó con razón. El ahorro sigue existiendo, pero cuesta recoger cobertura también en la puerta y
fusionarla, que es tocar su guion y no un interruptor.

**La lección, que es la de la casa otra vez: un ahorro que no mide lo que la pieza aportaba DE PASO
no es un ahorro.**

**Y el sexto desenlace no es un desenlace, sino la pregunta: mirar donde el run no está.** Hasta el
2026-09-02 el guion listaba con `--branch` y esa rama era **la local**, que en un worktree no es la
rama a la que se empuja. Un commit escrito en `claude/goofy-aryabhata-1e2f4a` y empujado a
`codex/shell-assembly-isolation` no tenía runs bajo el nombre que el vigía preguntaba —`ci.yml` sólo
dispara en `codex/**`—, así que **afirmó que el push no había disparado el flujo mientras el run
corría**. Eso es peor que el silencio contra el que está escrito: un silencio se espera, una
respuesta segura se obedece. Desde entonces **pregunta por el commit**, que es a quien pertenece un
run; `-Branch` sigue ahí para cuando la pregunta sea de verdad una rama, y el mensaje **nombra dónde
miró**.

**Y `gh run list --commit` exige los cuarenta caracteres.** Con un prefijo contesta `[]` y sale **0**,
que se lee igual que «aún no hay run» — medido el 2026-09-02, junto con lo contrario de una nota que
circulaba: `--commit` **sí** devuelve runs aquí, comprobado con tres SHA y los tres estados
(`in_progress`, `success`, `failure`). Así que el guion resuelve el prefijo con git antes de
preguntar, y si no puede resolverlo **ensancha** la búsqueda en vez de estrecharla mal.

**Y armar el vigía ya no depende de acordarse**: `.claude/hooks/post-push.sh` lo exige tras cada
`git push` —ver «Lo que el repositorio automatiza por ti», al final—. El comando llega con el SHA
**entero**, no con el corto: emitía `rev-parse --short HEAD`, que es justo el prefijo al que `gh`
contesta `[]`.

**Los suelos de cobertura los mide CI, no esta máquina.** Hoy nombra **189** <!--medido:archivos-en-deuda-->
archivos por debajo del listón de **96** <!--medido:listones-de-cobertura--> por ciento. `eng/coverage-debt.txt` se copia del
artefacto `coverage-debt` de un run de CI —el flujo lo emite en cada build, pase o falle— porque
siete archivos de audio, LibVLC y temporizadores dependen de hardware que un runner hospedado no
tiene: `WindowsAudioDeviceCatalog.cs` vale 79/61 aquí y 32/11 allí. Fuera de CI el trinquete informa
y no bloquea. Nunca se edita a mano, y nunca se genera con una ejecución local.

**Y el procedimiento exacto, que hasta el 2026-09-01 nadie había escrito y hubo que deducir bajo un
rojo.** Esa frase —«nunca se edita a mano»— convive con la regla de que el suelo que va a subir entra
en el mismo commit, y las dos parecen contradecirse: el hook deniega tocar el archivo, así que ¿cómo
entra el suelo sin una segunda vuelta? No se contradicen, y la salida no es aflojar la guarda:

1. **Se descarga el artefacto del run que dio el rojo** — `gh run download <id> -n coverage-debt` —,
   que es la única fuente de un suelo.
2. **Se llevan al listón los archivos que se puedan**, con pruebas. Un archivo sale de la lista
   **mejorando**, que es lo que el propio encabezado del archivo dice, y para saber **qué rama** falta
   se usa el JSON de coverlet, que la nombra con línea y offset.
3. **Se copia el artefacto quitando las filas de los que ya llegan.** Eso no inventa ningún número:
   todos vienen de CI, y quitar una fila que mejoró es exactamente lo previsto.
4. **El trinquete se ajusta en el mismo cambio** y las dos cifras tienen que cuadrar.

**Lo que la guarda impide es escribir un suelo a mano; copiar el artefacto y podarlo no es eso.** Un
`.axaml` nuevo es el único caso en que el trinquete **sube**, y la propia puerta lo autoriza por
escrito: «add it with the reason and raise the ratchet in the same change».

**El trinquete no vive en ese archivo: es `$debtRatchet` dentro de `eng/check-coverage.ps1`**, y ése
sí se edita. La lista sólo puede encoger, y las dos cifras tienen que cuadrar. Está en **189** <!--medido:trinquete-de-deuda-->
desde
el 2026-09-05, cuando subió por una vista nueva: `PlaybackSettingsView.axaml` mide 100/50 como las
otras sesenta, porque esa mitad es la única rama que el compilador de Avalonia genera para un
`.axaml`. Antes estuvo en 188 desde el 2026-09-03, cuando el selector de carátula llegó a 100/98 al
ganar su botón y salió de la lista; y en 189 el 2026-08-31, cuando los dos ViewModels de Cursos
salieron, bajando a 188 ese mismo día al llegar `MarkerEditorViewModel` a 100/100.

**Y el ViewModel que llegó con esa vista NO entró en la lista, que es la otra mitad de la regla.** El
run lo midió a 90/95 y la salida fue cubrirlo: el JSON de coverlet nombró las cuatro líneas y las dos
ramas que faltaban, y con tres pruebas quedó en 100/100. **Un archivo nuevo sólo entra en la lista
cuando no puede mejorar**; si puede, se cubre.

**Y el 2026-09-02 subió por hardware ausente, que es el octavo archivo de esa clase.**
`WindowsAudioEndpointConfigurator.cs` escribe el formato de un endpoint de audio, así que casi todo
su cuerpo pide un dispositivo de render: mide 64/54 aquí y **23/20** en el runner, que es de donde
sale el suelo. **Y en la misma tanda un suelo NO bajó**: `LibVlcAudioOutputAdapter.cs` leyó 77/75 en
el run que lo destapó porque ese run midió código nuevo antes de que existieran las pruebas que lo
cubren; entraron en el mismo cambio y lo devolvieron a 88/87. **Un suelo que baja es una bajada, y la
salida es cubrir, no rebajar.**

**Y el 2026-09-01 subió por primera vez, a 189, con la única razón que la puerta acepta por escrito**
—«add it with the reason and raise the ratchet in the same change»—: `LessonsPanelView.axaml` mide
100/50 y **eso no es deuda**, es la única rama que el compilador de Avalonia genera para un `.axaml`,
en la línea del elemento raíz, y todas las vistas del árbol miden exactamente eso. **Una vista nueva
sube este número en uno.** Los otros cuatro archivos que esa tanda trajo a la lista salieron de ella
**mejorando**, que es el único camino que admite: las ramas que faltaban se nombraron con el JSON de
coverlet —línea y offset— y se cubrieron con pruebas antes de escribir el archivo.

**Y esta frase decía 205 mientras el guion decía 191**, durante toda una tanda: la cifra se copió a
mano y nadie la volvió a mirar, que es el mismo defecto que el propio párrafo describe. **La única
fuente es `$debtRatchet`**; lo de aquí es una referencia y puede estar vieja, así que se comprueba
en el guion antes de citarla.

**La puerta falla igual ante un suelo que se queda corto y ante uno que se queda largo**: en cuanto un
archivo mejora, el run se pone rojo pidiendo sacarlo de la lista o subir su suelo.

**Eso NO significa que subir cobertura cueste dos vueltas.** Lo decía esta guía y era una excusa
disfrazada de regla: se anunciaba el rojo en vez de evitarlo. El 2026-08-31 se midió
`ResourceKeyConverter` en **83,33 %** de ramas y CI dijo **83**: el número estaba en la mano y se usó
para escribir un aviso en el relevo en lugar de para corregir el archivo.

**Y ya no es una frase, porque una frase no dispara:**

```powershell
pwsh -NoProfile -File eng/preview-coverage-floors.ps1
pwsh -NoProfile -File eng/preview-coverage-floors.ps1 -Suites Domain.Tests,UiTests,IntegrationTests
```

Corre las suites que le nombras, lee los informes con **la aritmética de la puerta** y dice qué
suelos se van a quedar cortos y qué archivo nuevo no llega a 96/96. **No escribe
`eng/coverage-debt.txt`**: los suelos siguen saliendo del artefacto de CI, que es lo único que mide
los siete archivos de hardware. Lo que cambia es que el artefacto **confirma** en vez de descubrir.

**Sus dos límites están escritos dentro y un silencio suyo no es un certificado**: sólo conoce las
suites que corres —un archivo cubierto por una que no corriste lee bajo—, y esos siete archivos
nunca leen aquí como leen en el runner.

**Y no basta con mirar los archivos nuevos.** Un archivo sube por dos vías, y la segunda es la que se
olvida: porque le añades una rama cubierta, o porque **pruebas nuevas lo recorren de paso**. Ese día
`AddLibraryRoot` pasó de 85 a 92 sin que nadie lo tocara, sólo porque las pruebas de
`DeclareCourseFolder` lo atraviesan. Las dos vías se leen en el propio diff: **qué archivos toco, y
qué archivos ejecuta lo que acabo de probar**.

**Cuándo sí son dos vueltas**, y entonces se dice por qué en concreto en vez de citar esta sección:
cuando el archivo que sube es uno de los **siete que dependen de hardware** que un runner hospedado no
tiene, porque su cifra local no es la de CI y no hay forma de adelantarla.

**Lo que sí se puede hacer aquí es reproducir lo que CI mide, y evita perseguir a ciegas.**
`gh run download <id> -n test-results` trae los **20 informes Cobertura** del run; fusionados con el
mismo `reportgenerator` y leídos con la aritmética de `check-coverage.ps1` —líneas por número con
«cubierta en cualquier sitio gana», ramas **sumadas**— reprodujeron los nueve suelos de `Domain`
**exactos**. Medir una sola suite en local **miente**: `MatchModels` da 5 de 8 ramas en `Domain.Tests`
y 7 de 8 en la fusión.

**Cobertura dice «3 de 4» y no cuál; el JSON de coverlet sí lo dice.**
`--collect:"XPlat Code Coverage;Format=json"` nombra la rama con línea, offset y camino, y con el
offset en la mano `GetMethodBody().GetILAsByteArray()` cierra la pregunta. Así se supo que **tres de
las quince ramas de `Domain` eran inalcanzables** —un caché de delegado sobre una clausura que se
reasigna en cada llamada, un brazo que exigiría que `GetRelativePath` devolviera cadena vacía, y una
temporada negativa que ningún `\d{1,3}` produce—. **Un techo medido se escribe en la prueba que
alguien volverá a mirar**, no sólo en la evidencia.

**Quien verifica de verdad es CI, y por eso el orden cambió el 2026-08-18.** CI corre ese mismo
`verify.ps1` **y además** `run-accessibility.ps1 -Passes 2`, `run-recovery.ps1 -Passes 2` y
`check-walk-coverage.ps1`: es un superconjunto estricto de lo que puede correrse aquí, y las carreras
que sólo aparecen en la segunda pasada nunca las vio una ejecución local. Correr `verify.ps1` entero
antes de cada push era hacer dos veces el mismo trabajo —media hora de la máquina de quien programa,
por commit— para obtener una garantía **más débil** que la que llega después.

El orden es ahora:

1. Durante el trabajo, **las suites afectadas** en local. Segundos, y es donde se atrapa lo evidente.
2. `git add -A` → **commit** → push **sólo a la rama**.
3. **CI verifica**, una vez: desde el 2026-08-18 `main` no dispara el flujo, porque recibe el mismo
   SHA por fast-forward y un check pertenece al commit, no a la referencia.
4. **Con CI en verde**, el fast-forward a `main` — que es instantáneo y no vuelve a verificar.

Así `main` no recibe nunca un commit sin verificar, y lo garantiza el paso 3 en lugar de una
verificación local más floja. Lo que se cede está medido: un fallo se conoce unos cuarenta minutos
más tarde, en la rama, donde no molesta. **Lo que exige es mirar CI del commit anterior antes de
avanzar `main`**, o un rojo queda debajo del trabajo siguiente.

`eng/verify.ps1` sigue siendo la herramienta cuando hace falta la respuesta completa aquí y ahora —
un cambio del empaquetado, o una duda que CI tardaría en contestar—. Deja de ser un peaje por commit.

Medir antes de corregir. «Funciona» no es evidencia; un número lo es. La evidencia vive en
`docs/evidence/`, y la de auditorías se acumula en `docs/evidence/stable/`.

## Arquitectura, en una pantalla

Cinco capas, dependencias hacia dentro:

- `Domain` — políticas puras, sin E/S. Casi todas las **decisiones de seguridad** viven aquí:
  `RenamePolicy`, `UpdatePolicy`, `DiagnosticsAllowlist`, `MediaFileExtensions`.
- `Application` — casos de uso y puertos (`IMetadataProvider`, `IUpdateSource`, `ISettingsStore`).
- `Infrastructure` — los adaptadores, y por tanto **toda la superficie de ataque real**: SQLite,
  sistema de archivos, LibVLC, TMDB, actualizador, backup/ZIP.
- `Presentation` — Avalonia (AXAML y ViewModels), sin dependencias de Windows.
- `Windows` — el anfitrión: `Program.cs` y `CompositionRoot.cs`, único sitio con `Process.Start` y
  con la construcción de `HttpClient`.

## Las cinco reglas que este repositorio hace cumplir con pruebas

No son estilo: hay una puerta que falla si las rompes.

1. **Licencia por archivo.** Todo fuente nuevo lleva `SPDX-License-Identifier: GPL-3.0-or-later`. Lo
   exige `IDE0073` desde `.editorconfig`, así que lo caza `dotnet format`.
2. **Red declarada.** Ninguna conexión fuera de `NetworkPurposeRegistry`. Una prueba recorre `src/`
   buscando hosts no declarados y falla; otra levanta un proceso hijo y escucha si abre algo.
3. **Diagnóstico por lista blanca.** `DiagnosticsAllowlist` es una lista **cerrada** de campos. No se
   filtra lo malo, se permite lo bueno: un filtro tiene que imaginar de antemano cada cosa que puede
   salir mal.
4. **Bilingüismo.** Cadenas visibles y documentos públicos, en los dos idiomas.
   `BilingualHeadingTests` compara la estructura de ambos.

   **Y desde el 2026-09-05, en los dos idiomas O EN NINGUNO NO BASTA: también tiene que leerla
   alguien.** `OrphanedResourceTests` falla ante una clave traducida que ninguna pantalla pide, y
   existe porque la puerta de al lado **no puede verlo**: compara los dos diccionarios entre sí, así
   que una clave muerta en ambos la deja igual de contenta. Ocho cadenas vivían de ese hueco, entre
   ellas un tercer nombre para el mini reproductor.

   **La lección de cómo se escribió vale más que la puerta**, y es la que hay que recordar antes de
   borrar nada: en su primera versión **dio CUARENTA cadenas vivas por muertas**. Barría sólo
   `Presentation`, y las claves viajan — el dominio entrega los códigos de identificación y los
   hallazgos de restauración como texto, y `Windows` tiene el menú de la bandeja y los diálogos del
   sistema. Barriendo todo `src/` bajó de 58 a 42, y trece seguían vivas porque **una clave se compone
   de dos maneras y sólo conocía una**: `"MarkerKind" + kind` sí, `$"RestoreFinding{finding.Kind}"`
   no. Veintiuna en la tercera pasada, y ésa es la lista que coincide con la auditoría.

   **Borrar sin la puerta, o con la puerta de la primera pasada, se habría llevado cuarenta cadenas
   que el programa dibuja cada sesión, y ninguna prueba habría dicho nada.** Por eso la guardia se
   escribe ANTES que la limpieza. Su lista de excepciones admite una clave sólo cuando algo ya escrito
   dice que se va a dibujar —una fila de la matriz, o un hallazgo de una auditoría que la nombre—, y
   encoge dibujando lo que hay en ella.
5. **Nada personal en el árbol.** Ni rutas de una máquina concreta, ni nombres de la biblioteca de
   nadie. `RepositoryPrivacyTests` lo mide.

## Y cuatro más, si vas a tocar una vista

Llegaron con el rediseño y fallan igual de rápido. Ninguna se deduce leyendo el `.axaml`.

6. **Ningún `.axaml` escribe un número que tenga token.** Las tres escalas —`FontSize*`, los cinco
   `Space*` y los dos `CornerRadius*`— tienen puerta en `ScalarTokenTests`, y lo que se afirma es que
   **el marcado no escribe el número**, no que el valor coincida: un token de 8 y un literal de 8
   pintan igual, así que comparar el valor aprueba justo lo que debía rechazar.
7. **Cada vista lidera con el botón que se decidió, o con ninguno.** `LeadingActionTests` lleva una
   tabla cerrada de las 61 <!--medido:vistas-->; **una vista que no esté en la tabla falla**,
   y `primary-action` se afirma
   como **la única** de su vista. Si tu vista es nueva, la decisión es tuya y hay que escribirla ahí.
8. **Ningún control se dibuja fuera de la ventana más estrecha que la aplicación permite** (900, el
   `MinWidth` de `App.axaml.cs`). Lo mide `ViewOverflowTests` sobre las 61 <!--medido:vistas-->, sin
   contexto de datos —lo
   que deja **todas** las ramas visibles a la vez—. Sus dos limitaciones están escritas dentro: un
   silencio suyo no es un certificado.
9. **Un control nuevo llega con su escena de paseo en el mismo commit.** El trinquete de
   `eng/check-walk-coverage.ps1` **sólo puede encoger**. Estuvo en 0 y **subió a 23** <!--medido:paseo-pendiente-->
   el 2026-08-25,
   por el arnés y no por la aplicación: el hit test headless de Avalonia no sigue el desplazamiento
   de un `ScrollViewer`, y Ajustes creció de 949 a 1.797 px, así que veinte controles de
   `AppearanceSettingsView` caen fuera del primer viewport. Se probaron tres vías y las tres
   contestaron lo mismo; **los veinte se pulsan con un ratón real**. Subirlo otra vez exige medir el
   porqué y escribirlo en la cabecera de `eng/walk-pending.txt`, como aquel día.

   **Y el 2026-09-02 subió a 23, esta vez por la máquina y no por el arnés.** Los tres botones de la
   disposición de canales se ofrecen sólo donde el controlador del dispositivo los admite, y cada
   endpoint físico de esta máquina declara dos canales mientras un runner hospedado no tiene ninguno:
   5.1 y 7.1 salen **deshabilitados** dondequiera que el paseo corra, y el arnés se niega a pulsar un
   control deshabilitado —con razón, porque una persona tampoco puede—. La escena afirma en su lugar
   la correspondencia en los dos sentidos — y **no pulsa ninguno de los tres**, porque esta puerta es
   simétrica: falla un pendiente que no esté en la lista **y** un listado que resulte pulsado. Aquí
   estéreo sí es pulsable y en el runner no, así que ninguna lista puede ser correcta en las dos
   máquinas mientras la escena pulse lo que puede — 219 aquí contra 218 allí, medido. Los tres salen
   de la lista el día que el paseo corra sobre una máquina con salida multicanal.

## Y una décima, para el código que habla con el sistema operativo

10. **Lo que habla con la máquina se separa de lo que decide, y sólo lo primero se excluye de la
    cobertura.** La puerta es la de archivos nuevos: exige 96/96 y **no admite excepción**, a
    diferencia de `eng/coverage-debt.txt`, que sí sabe decir «esto depende de hardware que el runner
    no tiene» y sostiene siete archivos en esos términos.

    **La asimetría es deliberada y la regla sale de ella.** Un archivo nuevo que no llega al listón
    por depender del hardware casi siempre lleva dentro dos cosas distintas, y confundirlas es lo que
    hace que parezca imposible de probar. Medido el 2026-09-02 sobre
    `WindowsAudioEndpointConfigurator`: era COM de arriba abajo y leía **23/20** en un runner sin
    tarjeta de sonido; detrás de dos interfaces —`IEndpointFormatStore` e `IEndpointFormatProbe`,
    públicas por la misma razón que `IAudioOutputTarget` ya lo era— la aritmética que decide cuántos
    canales salen por los altavoces se ejecuta en cualquier sitio, y pasó a **100/100** con
    diecisiete pruebas. El trinquete **bajó** en vez de subir.

    **`[ExcludeFromCodeCoverage]` va sólo sobre la creación de los objetos del sistema y sus
    `catch`** — lo que únicamente puede fallar si Windows falla—, con la razón escrita al lado. Es lo
    que la documentación de coverlet describe para «métodos difíciles o imposibles de probar
    directamente», y lo honra sin configurar nada en `eng/test.runsettings`. **Nunca sobre lo que
    decide**: excluir la aritmética habría dejado sin ejecutar justo lo que una persona oye.

    **Y qué línea excluir no se adivina**: `--collect:"XPlat Code Coverage;Format=json"` las nombra
    una a una con su línea y su offset. Ese día nombró la creación de objetos y nada más, que es lo
    que hizo evidente dónde estaba la costura.

## El defecto característico de este proyecto

**Registrado y nunca alimentado**: un servicio que se registra en el contenedor y que nada resuelve,
o una vista que se construye y a la que nadie llega. Una auditoría encontró 32 de estos de golpe. Hay
pruebas de arquitectura que exigen que cada servicio registrado tenga al menos una resolución fuera
de su propio registro. Si añades un registro, añade también quien lo consume, y compruébalo.

## Trampas conocidas

- **LibVLC** decodifica en proceso y con código nativo: es el mayor riesgo residual y está asumido y
  documentado. No lo empeores pasándole rutas sin filtrar por la lista de extensiones aprobadas.
- **El actualizador** verifica en un orden que importa: firma minisign sobre las notas **antes** de
  extraer el hash, allowlist de host en **cada** salto de redirección, y el archivo vive como
  `.partial` hasta que su hash y su tamaño coinciden. No reordenes esos pasos.
- **La caché de TMDB** tiene un techo duro de retención de 180 días porque sus términos lo exigen. No
  lo subas.
- Los `.axaml` admiten un comentario XML antes del elemento raíz, pero **no** una declaración
  `<?xml?>` después.

## Qué no se hace aquí

Servidor, cuentas, streaming, telemetría, sincronización en la nube. La
[hoja de ruta](docs/roadmap/README.es.md) lo dice con nombres. Una propuesta en esa dirección se
rechaza aunque esté bien implementada.

## Lo que el repositorio automatiza por ti

`.claude/` y `.mcp.json` están versionados, así que esto llega con el clon:

- **`.mcp.json`** declara el MCP de Avalonia. La regla 0 de abajo lo exige, y por eso viene en el
  árbol en vez de en la máquina de cada uno.
- **`disableClaudeAiConnectors`** apaga aquí los conectores de nube de claude.ai, y no es higiene:
  medidos el 2026-08-29, pesaban **298,8k fichas de contexto —el 30 % de la ventana—, y 212,9k eran
  de uno solo**: 102 herramientas de anuncios que este repositorio no usa. La clave la gana cualquier
  fuente que la ponga en `true`, así que **el proyecto puede salirse sin tocar la configuración
  personal de nadie**. `avalonia-docs` no se ve afectado: viene de `.mcp.json`, que es local. **El
  efecto es de arranque**, así que se comprueba con `/context` en la sesión siguiente y no en la que
  lo escribe.
- **`deniedMcpServers`** deniega aquí `gbrain` y `MCP_DOCKER`, los dos servidores locales que fallaban
  al conectar en cada arranque —`REQUEST_TIMEOUT` y `CONNECTION_CLOSED`, leídos de `claude mcp list`—.
  Se deniegan **en el proyecto y no se borran de la máquina**, porque son de quien programa y se usan
  fuera de aquí. A diferencia de la clave de arriba, **ésta sí se midió en el acto**: `claude mcp
  list` enseñaba tres servidores antes y enseña sólo `avalonia-docs` después.
- **Cuatro hooks** que hacen cumplir lo que antes eran frases. Dos **rechazan antes de escribir**:
  `eng/coverage-debt.txt` y `eng/walk-pending.txt`, y un `.cs` o `.axaml` de `src/` o `tests/`
  **cuyo contenido no lleve la cabecera SPDX**. **Los dos primeros se rechazan por motivos
  distintos, y confundirlos costó una corrección el 2026-08-29**: `coverage-debt.txt` **lo produce
  CI** y se copia de su artefacto, mientras que `walk-pending.txt` **no lo produce nadie más que
  este árbol** —`ci.yml` no lo emite— y es un trinquete que sólo puede encoger. Decirle a alguien
  que espere un artefacto de CI para un archivo que CI nunca publica es peor que no decirle nada. El tercero **avisa después** si se
  toca un `.es.md` y su pareja `.en.md` se queda como está en `HEAD` — pregunta a git y no al reloj,
  porque comparar `mtime` hacía que sonara también con los dos idiomas al día.

  **Rechazar y avisar no son lo mismo, y la diferencia se midió el 2026-08-29**: un `deny` llega al
  agente como error de la herramienta, mientras que un `systemMessage` **no entra en el contexto de
  quien está escribiendo el archivo**. Por eso la comprobación del SPDX pasó a `PreToolUse`, donde
  `tool_input.content` ya existe y se puede leer antes de escribir nada.

  **Y avisar era emitir para nadie, hasta que se cambió el canal.** El mismo día se midió lo que
  faltaba: un `systemMessage` **no llega a la pantalla de quien programa** —dos avisos provocados a
  propósito, uno por `Write` y otro por `Edit`, con el propietario delante y dentro de la sesión,
  pasaron sin que viera nada— y tampoco entra en el contexto del agente. Corrían, acertaban, y su
  salida moría en el `.jsonl`: registrado y nunca alimentado, el defecto de la casa en sus propias
  herramientas.

  **Los dos avisos del `PostToolUse` escriben ahora a stderr y salen con código 2**, que sí llega al
  agente que está escribiendo el archivo. Medido con la sonda al lado del caso conocido —el
  `systemMessage` dejó su rastro y no llegó; el stderr llegó— y luego por tubería con **siete casos,
  cuatro de ellos de los que debe dejar pasar**. El registro los anota como `hook_blocking_error`.

  **Su precio se midió y se bajó, y por eso el comando vive en un archivo.** El harness imprime el
  comando entero **dos veces** delante del texto útil, así que en línea costaba **2.712 caracteres**
  de contexto por aviso. Ahora está en `.claude/hooks/post-write.sh` y el `settings.json` sólo lo
  llama: **488 caracteres**, un 82 % menos, leído del registro y no calculado — la cuenta a mano daba
  528. Los otros dos hooks siguen en línea porque son cortos y **deniegan**, así que su texto nunca
  se imprime dos veces.

  Y como el aviso llega etiquetado de «error» después de una escritura que sí funcionó, los tres
  mensajes **empiezan diciendo que la escritura no falló**: sin eso se lee como un fallo y se
  reintenta la misma escritura.

  **El cuarto vigila el ciclo, no un archivo.** `post-push.sh` corre en `PostToolUse` sobre
  `Bash|PowerShell` y, tras cualquier `git push`, escribe por stderr y sale con 2 el comando del
  monitor **con el SHA ya resuelto**. Existe porque «para mirar CI se usa `eng/watch-ci.ps1`» era una
  frase, y **una frase no dispara**: el 2026-08-30 el propietario tuvo que pedirlo. Y tenía razón
  aunque el monitor estuviera armado — `TaskOutput` decía `running` con **0 KB**, porque el guion late
  cada 30 minutos: **corriendo y callado se ve igual que no existir**. Suena también en el
  fast-forward a `main`, que no dispara el flujo, y eso es deliberado: distinguirlo pedía adivinar la
  rama de destino, y una guarda que se equivoca **callando** es indistinguible de una que no corrió.

  **Y sonó en su propio commit, que es el defecto que enseñó a escribirlo bien**: buscaba la cadena
  suelta, y el mensaje del commit —escrito con un heredoc— hablaba de «after any git push». Ahora
  **tira los heredocs primero** y exige que `git push` esté en **posición de comando** —inicio de
  línea, o tras `;`, `&`, `|`, `(` o `&&`—. Diez casos por tubería: cuatro que suenan, seis que callan,
  incluidos ese heredoc y `git pushd`. Un aviso que suena cuando no toca enseña a ignorarlo, que es
  peor que no avisar.

  **Y ese arreglo estaba a medias hasta el 2026-09-01, escondido en un espacio.** El regex del
  heredoc era `<<-?['"]?DELIM` **sin admitir espacio**, así que sólo reconocía la forma pegada y
  dejaba pasar `<< 'EOF'` —que es la que este repositorio usa en cada commit— como si no fuera un
  heredoc. Sonó al escribir un relevo que citaba una orden de push dentro de uno. **Reproducido por
  tubería antes de tocarlo y vuelto a medir después, con ocho casos: cuatro que suenan —incluido un
  push que va detrás de un heredoc y sí debe sonar— y cuatro que callan.** Los diez de la línea de
  arriba son los originales y siguen valiendo; lo que faltaba era este caso. **Una guarda que dice
  por escrito haber corregido un defecto puede seguir teniéndolo**: lo que lo demuestra es la
  tubería, no el comentario.

  **Ninguno de los tres primeros dispara escribiendo por Bash** —`cat >`, `sed -i`, un heredoc—, así
  que siguen siendo un adelanto de aviso y no la puerta: la puerta es `dotnet format` con `IDE0073`, y
  `eng/verify-docs.ps1`.

  **Dos cubren `Write`, `Edit` y `MultiEdit`; el del SPDX es sólo `Write`**, y ahí no es un descuido:
  lee `tool_input.content`, que un `Edit` no trae entero. En el del bilingüismo sí lo era, y se
  corrigió el 2026-08-29 midiendo el mismo `Edit` antes y después: no lee contenido —sólo pregunta a
  git— y aun así se perdía **toda** edición, que es justo la herramienta con la que se toca un
  `.es.md` que ya existe, porque `Write` reescribe el archivo entero. La guarda cubría el camino
  menos transitado. `MultiEdit` queda declarado en el matcher pero **sin medir**.

  **Y un hook que calla no deja rastro en el registro de la sesión**: sólo se anota cuando produce
  salida. Un silencio observado en la aplicación no prueba que la guarda corriera —puede que ni
  siquiera la enrutaran—, así que el caso que **debe** callar se mide ejecutando el comando literal
  del `settings.json` por tubería, con un caso que sí debe sonar al lado.
- **`/cerrar-tanda`** ejecuta el ciclo de más abajo, con los fallos que ya ha cometido cada paso.
- **`/medir-pixeles`** trae el arnés de rasterización con sus cinco trampas medidas.
- **`gate-auditor`** busca puertas que pasan sin medir nada; **`prototype-fidelity`** compara la
  aplicación con `design/`.

Si los hooks no disparan, es que la sesión arrancó sin `.claude/settings.json`: se recarga abriendo
`/hooks` una vez.

