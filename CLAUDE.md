# AP Reelume — guía para agentes

Biblioteca de medios local para Windows 11: cataloga y reproduce vídeos que ya están en el disco de
quien la usa. Sin cuentas, sin telemetría, sin servidor. `GPL-3.0-or-later`.

Este archivo es para un agente que llega al repositorio sin contexto. Está en español porque el
proyecto se piensa en español y se publica en dos idiomas; el código, los commits y los nombres de
prueba van en inglés.

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
- **Tres hooks** que hacen cumplir lo que antes eran frases. Dos **rechazan antes de escribir**:
  `eng/coverage-debt.txt` y `eng/walk-pending.txt`, que los produce CI, y un `.cs` o `.axaml` de
  `src/` o `tests/` **cuyo contenido no lleve la cabecera SPDX**. El tercero **avisa después** si se
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

  **Ninguno dispara escribiendo por Bash** —`cat >`, `sed -i`, un heredoc—, así que siguen siendo un
  adelanto de aviso y no la puerta: la puerta es `dotnet format` con `IDE0073`, y
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

## Regla 0, inquebrantable: el MCP de Avalonia antes que nada

**Antes de escribir una línea de AXAML, de tocar un estilo o de afirmar cómo se comporta un control,
se consulta el MCP de Avalonia.** No es una sugerencia ni un último recurso: es el primer paso,
delante incluso de la lista de lectura de abajo.

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

**Y para mirar CI se usa `eng/watch-ci.ps1`, nunca un bucle escrito a mano.**

```powershell
pwsh -NoProfile -File eng/watch-ci.ps1 -Sha <sha>
```

Emite una línea por desenlace y **los cinco están cubiertos**: la conclusión literal —incluida una
vacía—, que el push no disparara el flujo, que `gh` falle, un latido cada 30 minutos mientras corre,
y un techo a los 120 que avisa y sale. El motivo de que sea un guion y no un bucle en el momento:
el filtro obvio pregunta por `status == "completed"` y **calla en todo lo demás**, y un vigía callado
es indistinguible de un run que sigue. Peor aún, `2>/dev/null` sobre la consulta **entierra** el error
de `gh` y `|| true` lo convierte en una cadena vacía que se lee como «aún no ha terminado». Un run de
este repositorio tarda **55-80 minutos**, así que hay hueco de sobra para no enterarse.

**Los suelos de cobertura los mide CI, no esta máquina.** `eng/coverage-debt.txt` se copia del
artefacto `coverage-debt` de un run de CI —el flujo lo emite en cada build, pase o falle— porque
siete archivos de audio, LibVLC y temporizadores dependen de hardware que un runner hospedado no
tiene: `WindowsAudioDeviceCatalog.cs` vale 79/61 aquí y 32/11 allí. Fuera de CI el trinquete informa
y no bloquea. Nunca se edita a mano, y nunca se genera con una ejecución local.

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
5. **Nada personal en el árbol.** Ni rutas de una máquina concreta, ni nombres de la biblioteca de
   nadie. `RepositoryPrivacyTests` lo mide.

## Y cuatro más, si vas a tocar una vista

Llegaron con el rediseño y fallan igual de rápido. Ninguna se deduce leyendo el `.axaml`.

6. **Ningún `.axaml` escribe un número que tenga token.** Las tres escalas —`FontSize*`, los cinco
   `Space*` y los dos `CornerRadius*`— tienen puerta en `ScalarTokenTests`, y lo que se afirma es que
   **el marcado no escribe el número**, no que el valor coincida: un token de 8 y un literal de 8
   pintan igual, así que comparar el valor aprueba justo lo que debía rechazar.
7. **Cada vista lidera con el botón que se decidió, o con ninguno.** `LeadingActionTests` lleva una
   tabla cerrada de las 48; **una vista que no esté en la tabla falla**, y `primary-action` se afirma
   como **la única** de su vista. Si tu vista es nueva, la decisión es tuya y hay que escribirla ahí.
8. **Ningún control se dibuja fuera de la ventana más estrecha que la aplicación permite** (900, el
   `MinWidth` de `App.axaml.cs`). Lo mide `ViewOverflowTests` sobre las 48, sin contexto de datos —lo
   que deja **todas** las ramas visibles a la vez—. Sus dos limitaciones están escritas dentro: un
   silencio suyo no es un certificado.
9. **Un control nuevo llega con su escena de paseo en el mismo commit.** `eng/check-walk-coverage.ps1`
   está en **0 pendientes** y no vuelve a subir.

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
