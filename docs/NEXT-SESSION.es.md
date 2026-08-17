# Dónde retomar

## La cola decidida el 2026-08-16 (no se re-delibera)

**El objetivo es cero.** Esta aplicación se publica gratis y **nadie la va a probar a mano**: lo que
la suite no cubra no lo cubre nadie. El trinquete de `eng/check-walk-coverage.ps1` va a **0
pendientes** —hoy **3**, con **125 de 128** controles pulsados con ratón— y la puerta de cobertura de
código, a vigilar el árbol entero. Todo lo de abajo está **decidido**; lo que queda es ejecutarlo
midiendo antes de corregir.

### El plan completo hasta 0.2.0, fijado el 2026-08-17

**Diez pasos, y el orden es una decisión, no una lista.** El paseo autónomo **es la red del
rediseño**, así que llega a cero **antes** de que la interfaz cambie; la puerta de cobertura pasa a
vigilar el árbol entero por la misma razón. Lo que le toca al propietario va en un bloque al final,
con una sola excepción: su paseo físico va **antes** del corte, porque un hallazgo suyo obliga a
rehacerlo entero.

| # | Paso | Quién | Deja el trinquete en |
|---|---|---|---|
| ~~1~~ | ~~La sesión suelta no se ve~~ **hecha el 2026-08-17, 6 → 3** | agente | 3 |
| 2 | Los tres últimos de la tanda 1 | agente | **0** |
| 3 | La prueba de los subtítulos | agente | 0 |
| 4 | Cobertura a todo `src/`, suelo 96/96 | agente | 0 |
| 5 | `ARQ-004`, las nueve clases inertes | agente | 0 |
| 6 | **El rediseño**, con el material de Claude Design | agente | 0, con la regla de abajo |
| 7 | El paseo físico de diez minutos | **propietario** | — |
| 8 | Cortar 0.2.0, hasta el instante de firmar | agente | — |
| 9 | Firmar y publicar | **propietario** | — |
| 10 | `REL-004` y la restauración trimestral de la clave | **propietario** | — |

#### ~~1. La sesión suelta no se ve~~ — hecha el 2026-08-17

**Hecha exactamente como estaba decidida** —
[la evidencia](evidence/stable/audit-walk-loose-session.md). `OpenLooseFile` valida y describe,
`ShellSurfaces.OpenLoosePlayer` es la vía única, y los dos llamantes pasan por ella. Tres cosas que no
estaban previstas y quedaron resueltas en el mismo cambio: **`OpenLooseFile` pasó a estático y salió
del contenedor** porque `CA1822` lo dijo al quedarse sin estado; **`ResumeWiringTests` se rompió por
leer la composición como texto** —cuarta vez— y se acotó a la declaración de `OpenPlayerAsync`; y
**`RepositoryPrivacyTests` señaló `design/`** como carpeta desconocida en la raíz, que es la
comprobación funcionando. El tráiler local **ya no está bloqueado**, pero sigue pendiente porque
necesita siembra propia, así que pasa al paso 2.

#### 2. Los tres últimos — decidido cómo se siembran y en qué orden

**Por primera vez en toda la cola, ningún pendiente está bloqueado por un defecto.** Son **dos
escenas, no tres**, porque dos de los tres viven en la misma ficha:

**(a) La ficha de película: «Continuar» y el tráiler local.** Siembra: la película en su carpeta, un
`WatchState` con posición **por encima del suelo de reanudación —30 s— y por debajo del final**, y un
archivo hermano **`<nombre-de-la-película>-trailer.mp4`**, que es lo que `TrailerDiscoveryPolicy`
busca (`Suffix = "-trailer"`, o dentro de una carpeta `Trailers`). **No hace falta grupo de
versiones**: `HasTrailer` sale del descubrimiento por nombre, no del catálogo — la nota antigua de la
cola decía lo contrario y estaba equivocada.

- **«Continuar»** — sonda: la sesión abre **en el punto guardado**, leído del motor y esperando a que
  el demultiplexor aplique la posición de inicio.
- **El tráiler** — sonda: la sesión pasa a reproducir **el archivo del tráiler**, y ahora eso se puede
  afirmar de verdad porque una sesión suelta ya llega a la pantalla: `Player.LooseFile.IsLooseSession`
  y la ruta del medio.
- **Y aquí se mide el primer hallazgo abierto:** con progreso guardado, **«Reproducir desde el
  principio» tiene que dejar el cabezal en 0**. Ahora que la posición pedida manda debería estar
  arreglado; lo que falta es el número.

**(b) La ficha de serie: la fila de episodio.** Siembra: serie con temporada y episodios —el arnés ya
tiene `SeedSeriesAsync`—, llegar a la ficha desde la biblioteca y pulsar la fila. Sonda: la sesión
abre **ese** episodio, por su ruta.

**Predicción medible, y se mide ANTES de pulsar:** la fila de acciones de `MovieDetailsView` es un
`StackPanel Orientation="Horizontal"` con **un `TextBlock` de anchura libre entre botones** —el texto
de la posición de reanudación— y cinco controles. Es **exactamente la forma que ha sacado un control
fuera de la ventana seis veces**, y esta escena es la primera que hace visibles a la vez «Continuar» y
el tráiler, así que la fila será la más larga que ha tenido nunca. Si se sale, pasa a `WrapPanel` como
las otras seis. Se mide con los `bounds` frente a la ventana antes de intentar el clic, no después del
rojo.

#### Los dos hallazgos abiertos, y qué se hace con cada uno

1. **«Reproducir desde el principio»** — se mide en la escena (a), como está dicho arriba.
2. **El progreso se guarda por archivo, no por grupo de versiones.**
   `ContentKey.ForTitle(new TitleId(mediaFileId.Value))` ata el progreso al archivo, así que tras
   cambiar de versión hay **dos `WatchState`** y volver a la anterior no reanudaría donde se dejó.
   **Decidido: se mide primero y sólo se corrige si la medición demuestra pérdida real** — la escena
   de versiones ya existe y basta con afirmar las dos claves tras volver. Cambiar la clave al grupo es
   un cambio de modelo con migración, y hoy la posición viaja en el encargo, así que el síntoma puede
   no existir.

#### Lo que queda decidido del paquete de diseño, para el paso 6

- **Los diez cambios de `SURFACES.es.md` / `.en.md` entran al principio del paso 6**, antes de tocar
  tokens: el inventario tiene que ser correcto antes de rediseñar contra él. Ahí entra también
  **`MiniPlayerWindow`, que no está en el documento** — medido el 2026-08-17 comparando el árbol con
  el inventario, y el paquete lo confirma al darle cinco controles nuevos.
- **La discrepancia de motivos de rechazo del actualizador se resuelve en OCHO**: `README.md` dice 8 y
  `github.md` dice 7, y el que cuadra con los 23 mensajes es el 8 (15 estados + 8 rechazos).
- **Las 25 cadenas de consecuencia se aprueban contra la regla que el propio paquete da** —«si la
  frase ayuda a decidir o a actuar, se traduce; si explica por qué está diseñada así, es un comentario
  del AXAML»— revisándolas una a una al escribirlas. **No bloquean el paso 6**; lo que no pase esa
  regla se queda como comentario.
- **Los 35 activos de instalación siguen bloqueados** en el original vectorial de la marca, y no se
  improvisan.

Lo que sigue debajo se conserva porque describe la forma de la corrección, que es la referencia para
la vía suelta:

**Un archivo activado desde el Explorador se reproduce y no se ve.** Medido el 2026-08-17:

```
singleton.IsLooseSession=True  name='Arrival.2016.mp4'  engine=Playing  pos=00:00:00.15
player=False  playerVisible=False  stages=0  surfaces=0
```

La activación hace su parte entera —`OpenLooseFile` arranca el motor y el banner recibe su sesión—,
pero **nadie construye las superficies del reproductor**, y `HasLooseFile` es
`Player?.LooseFile is not null`. Así que el vídeo suena sin imagen y sin transporte, y el aviso de
«esto no está en tu biblioteca» no llega a la pantalla con sus tres botones dentro. Lo mismo le pasa
al **tráiler local**, que abre por la misma vía.

**La causa de raíz, y por eso la corrección es la que es: hay dos caminos que abren medios y sólo uno
construye pantalla.** `OpenLooseFile` arranca el coordinador por su cuenta; `PlayerViewModel.OpenAsync`
arranca y además tiene superficie. Mientras haya dos, esto vuelve.

**Decidido: `OpenLooseFile` valida y describe, y abrir es siempre del reproductor.** Deja de llamar
al coordinador; conserva sus dos negativas —extensión fuera de la lista aprobada y archivo ausente—,
que son las que hacen falta **antes** de tocar nada. Se añade una vía única que los dos llamantes
usan:

- `ShellSurfaces.OpenLoosePlayer` — `Func<string, CancellationToken, Task<PlayerSurfaces?>>`, con su
  `ShellViewModel.OpenLoosePlayerAsync`, al lado de `OpenPlayer` y por la misma razón que aquélla.
- En la composición: pide la sesión a `OpenLooseFile`, construye `PlayerSurfaces` con **`Player`,
  `LooseFile` y `VideoStatus`** —de ese `record` sólo `Player` es obligatorio— más el transporte del
  contenedor, y llama a `player.OpenAsync(session.MediaFileId, session.Path)`.
- **Sin seguimiento de progreso, sin marcadores, sin versiones y sin oferta de reanudar**, que es lo
  que mantiene la promesa: «una sesión suelta deja la base de datos como la encontró».
- Los dos llamantes pasan por ahí: la activación (`ConfigureWindow`) y el tráiler local
  (`onPlayTrailer` en `CompositionRoot`).

**Lo que se gana de paso, y es una mejora real:** hoy un archivo suelto que no se puede decodificar
hace que el `catch` limpie el banner y no quede nada en pantalla; abriendo por el reproductor, el
fallo llega a `Report` y **aparece la pantalla de recuperación** que la tanda 2e acaba de dejar
probada.

**Lo que no vale, y está comprobado:** reutilizar `OpenPlayerAsync` tal cual —empieza por
`FindByIdAsync` y un archivo suelto no está en el catálogo, y su camino arranca el seguimiento de
progreso—; y dejar que `OpenLooseFile` siga arrancando y abrir otra vez desde el reproductor, que es
un doble arranque sin razón.

**Antes de tocarlo, releer `FileActivationTests`**: afirma la promesa que no se puede perder —el censo
de más de veinte tablas idéntico antes y después de una activación— y **no** afirma que
`OpenLooseFile` arranque el motor, así que mover el arranque no la rompe. `OpenLooseFileTests` sí
habla del coordinador y se actualiza con el cambio.

**Cómo llega el arnés:** `ApplicationHost.PendingActivationPath` antes de `CreateShell` y
**`ConfigureWindow` después**, que es donde la activación se lee y en ningún otro sitio.

#### 3. La prueba de los subtítulos — decidido qué se mide

`A11Y-002` se bloquea **por medición y no por observación**. Primero se intenta lo directo:
decodificar un fotograma con estilo aplicado y otro sin él y comparar los mapas de bits; si el motor
no deja llegar al fotograma, el plan B mide **la causa**, que ya está diagnosticada — la instancia de
LibVLC se cachea por juego de opciones y ninguna de las cacheadas lleva opciones de subtítulos. En los
dos casos el resultado es el mismo bloqueador, con un número detrás. Va en `MediaTests`.

#### 6. El rediseño — la regla del trinquete, decidida antes de empezar

El paseo cuenta hoy **129 declaraciones en 128 identidades** leyendo los `.axaml`, y el guion sólo
sabe encoger. Un rediseño mueve ese inventario, así que:

- **Un control nuevo entra con su escena en el mismo cambio**, nunca con una línea en
  `eng/walk-pending.txt`. La lista de pendientes se cerró y no se reabre.
- **Un control renombrado** cambia su ancla en la escena que lo pulsa; el ancla es la clave de
  recurso tras `AutomationProperties.Name`, y un rediseño cambia la forma sin quitarla.
- **Un control que desaparece** sale del inventario solo, y el trinquete baja con él.
- Los **dos superpuestos que quedan sin dimensionar** —`SkipMarkerButton` y `LooseFileBanner`— se
  corrigen aquí si el rediseño los toca, y si no, cada uno en su escena con su medición.
- Los **cinco activos de marca** encajan en este paso, que es donde vive la dirección visual. Si
  vienen aquí, entran en 0.2.0 sin coste; el paquete se construye hoy sin ellos.

#### Las cinco decisiones del 2026-08-17, tomadas y no reabiertas

~~**1. Las dos respuestas que faltan del diálogo de versiones (2 controles).**~~ **Hecha el
2026-08-17, 10 → 8** — [la evidencia](evidence/stable/audit-walk-version-switch-answers.md). El
control desprendido se confirmó tal cual —`before detached=False en=True`, `after detached=True
en=False name=<null>`, con una fila viva ocupando su sitio—, **pero era el síntoma**: `PressAsync`
reintenta cuando la sonda no cambia, y para el segundo intento la sesión ya se había reconstruido. La
causa medida en la misma ejecución fue `asking=False`: **la pregunta no se levantaba**, porque el
suelo de reanudación son 30 s y la escena cambiaba a una versión de 20 — no existe posición que
cumpla las dos cosas. Duraciones a **60 s y 180 s** y el orden pasa a confirmar → refusar → empezar de
nuevo, que lo fija la misma aritmética. Y con las duraciones arregladas salió **un defecto de producto
del que nadie miraba**: confirmar un cambio calculaba el segundo trasladado, lo guardaba (`00:02:01`)
y luego **abría la otra versión desde cero** escribiendo ese cero encima (`playhead: 0, 0, 0, 1, 1,
2`). `PlayDetailsRequest.StartPosition` estaba **producida en cinco sitios, documentada, vigilada por
una prueba del lado del productor y leída en ninguno** — el defecto de la casa visto desde el
consumidor. Pasa a `TimeSpan?`, donde `null` es «decide tú con la política de reanudación».

**Hallazgo abierto que salió de ahí, sin medir y por tanto sin corregir:** «Reproducir desde el
principio» de la ficha de película pasaba `TimeSpan.Zero` a un anfitrión que lo ignoraba, así que con
progreso guardado **probablemente no empezaba por el principio**. Ahora que la posición pedida manda,
debería estar arreglado de paso; lo que falta es **medirlo**, y el sitio natural es la escena de la
tanda 1 que ya tiene que sembrar progreso para «Continuar».

~~**2. La novena salida de la regla de aislamiento (2 controles de la 2e).**~~ **Hecha el 2026-08-17,
8 → 6** — [la evidencia](evidence/stable/audit-walk-player-recovery.md). El anotador
(`RecordingExternalPlaybackLauncher`, verbo `play-externally <ruta>`) entra en los vigilados de
`check-coverage.ps1` al 100/100, y sus **dos negativas** —extensión fuera de la lista y archivo
ausente— se afirman en `IsolatedRunTests` junto a las dos mitades de la elección. Lo que **no** se
cumplió fue que las dos pulsaciones compartieran superficie: `corrupted=True canRetry=False
canOpenExternally=True`. La política da a un medio corrupto elegir otra versión y abrir fuera, **sin
reintentar**, y tiene razón —reabrir los mismos bytes falla igual—; reintentar se ofrece cuando
**falta el archivo**. La escena abre dos veces y cada pulsación encuentra el fallo que la ofrece.

**3. Los superpuestos que siguen sin dimensionarse** —ya sólo `SkipMarkerButton` y `LooseFileBanner`,
porque `VersionSwitchDialog` se corrigió el 2026-08-17 con su medición (`surfaces=1 [0, 0, 1280, 1400]`
sobre `stage=0, 0, 1280, 1400`)— se corrigen **cada uno en su escena y con su medición**: `Border` con
alineación, fondo y borde, y las filas de botones a `WrapPanel`. La medición se toma antes: los
`bounds` del control frente a los del escenario.

**4. `A11Y-002` en el corte de versión: pasa a `BLOCKED`.** El estilo de subtítulos llega a la base de
datos y **no a la imagen** —LibVLC toma su dibujado de las opciones con las que se construye la
instancia, y aquí hay una instancia cacheada por juego de opciones sin ninguna de subtítulos—, así que
«subtítulos personalizables» no está entregado por mucho que sus seis controles existan y persistan. Se
cambia **en el corte**, que es donde el manifiesto se regenera con un paquete recién construido, con el
bloqueador nombrado en `eng/generate-verification-manifest.ps1` y en `release-readiness.md`. El paseo
físico de diez minutos gana además una comprobación: **mirar si los subtítulos se ven como se
pidieron**.

~~**5. El apagado con una sesión activa se corrige, y así.**~~ **Hecha el 2026-08-17**, exactamente
como estaba decidida: `ApplicationHost.DisposeAsync` para la sesión (`StopAsync`, con
`ObjectDisposedException` tragada) antes de `_services.DisposeAsync()`. Lo que la prueba **no** es la
escena de la 2a sino la nueva de recuperación, que termina con un vídeo sonando por la misma razón:
cerrar el reproductor primero es lo que hacían todas las escenas y es lo que tapaba esto.

Después: la cobertura a todo `src/`, lo que queda de `ARQ-004`, y el rediseño.

~~**7c — «Cancelar» del actualizador.**~~ **Hecha el 2026-08-17, 34 → 33** —
[la evidencia](evidence/stable/audit-walk-update-cancel.md)—, tal como estaba decidida: campo
**opcional** `serveDelayMilliseconds` en el manifiesto, `await Task.Delay(delay, ct)` en el
transporte, escena propia y sonda de estado. **La ventana se midió y 3000 ms no valía:** las dos
pulsaciones gastan **950 ms**, pero la ventana también tiene que aguantar el presupuesto de
reintentos de `PressAsync` —ocho pulsaciones a un asentamiento de distancia, **2400 ms**—, así que
quedó en **5000 ms**, que no cuesta nada porque cancelar abandona el resto de la espera.

~~**6b — «Cancelar» de copias.**~~ **Hecha el 2026-08-17, 33 → 32** —
[la evidencia](evidence/stable/audit-walk-backup-cancel.md)—, y **el destino sigue siendo 0**: la
biblioteca honesta que tarda lo suficiente existe. Las dos palancas se midieron en el orden decidido y
ganó la segunda por una razón que no estaba prevista: **el coste por archivo pesa más que el coste por
megabyte**, así que la palanca es cuántas imágenes hay y no cuánto pesan. El catálogo llega al segundo
sólo con 50.000 filas (1.159 ms) y encarece la escena; **6.000 imágenes de 50 KiB dan 3.944 ms con
293 MB**, donde 6.000 de 100 KiB dan 4.377 ms con el doble de disco. Ni un gancho en la composición, y
sin defecto de producto: el control funcionaba y lo que faltaba era una ventana.

**Tanda 2 — DECIDIDO que se parte en cinco escenas por superficie**, no una sola de 29 controles:

| | Superficie | Controles |
|---|---|---|
| ~~**2a**~~ | ~~Pistas y salida de audio~~ | **hecha el 2026-08-17, 32 → 27** |
| ~~**2b**~~ | ~~Estilo de subtítulos~~ | **hecha el 2026-08-17, 27 → 23** |
| ~~**2c**~~ | ~~Marcadores: editor, revisión y salto~~ | **hecha el 2026-08-17, 23 → 16** |
| ~~**2d**~~ | ~~Reanudar, siguiente episodio y cambiar de versión, con las tres respuestas~~ | **hecha el 2026-08-17, 16 → 8** |
| **2e** | ~~Recuperación del reproductor~~ **hecha el 2026-08-17, 8 → 6**; el archivo suelto espera al defecto de arriba | 3 |

Es la única tanda que necesita **vídeo real**. Y la advertencia sigue medida: **los superpuestos que
quedan no fijan alineación** y se estiran sobre todo el escenario, igual que el de estado corregido el
2026-08-15; **cada uno se corrige en su escena con su medición**, nunca en bloque. Quedan dos.

**Lo que dejó la 2a, y ahorra tiempo en las cuatro que quedan:** un desplegable se prueba **abriéndolo**
—lo que se elige dentro cae en otra raíz de ventana— y se cierra con Escape antes del siguiente;
`RequireMultiTrackSampleAsync` produce y cachea una muestra con **dos pistas de audio y una de
subtítulos**; y el defecto que salió es de la familia de la casa vista del revés: un ámbito que la
aplicación **lee** y que nada dentro de ella podía **escribir** —
[la evidencia](evidence/stable/audit-walk-tracks-and-audio-output.md).

**Los tres de la tanda 1** son los que necesitan siembra que el paseo aún no hace: la fila de episodio
(exige serie, temporada y episodios), «Continuar» de la ficha de película (exige progreso guardado que
merezca volver) y su tráiler local (exige un archivo de tráiler junto a la película, con grupo de
versiones).

**El corte de versión.** Ya son **trece** evidencias esperando a entrar en `FEATURES.md`, y regenerar
el manifiesto es parte de cortar una versión, no de una sesión de trabajo. **Decidido**: cuando el
paseo llegue a su mínimo se corta **0.2.0** con un paquete recién construido, y ahí entran las trece
de golpe.

**El apagado desde la ventana y desde la bandeja se queda directo.** No son controles del inventario
—no hay AXAML detrás—, así que no tocan el trinquete, y una ejecución aislada no llega a ellos hoy. Se
revisa si el rediseño toca el ciclo de vida, y no antes: inventar ahí trabajo sería inventarlo.

### La decisión que lo desbloquea todo: una ejecución aislada no toca nada fuera de su raíz

Tres controles se declararon incubribles por la misma razón, y la tercera vez deja de ser casualidad
para ser una regla del proyecto:

- **Conceder el arranque con Windows** escribía en la clave que Windows lee al iniciar sesión.
  **Resuelto el 2026-08-16** con `IAppDataPaths.StartupRegistrySubKey`.
- **El enlace al tráiler del proveedor** (`DetailsTrailerLinkAction`, en la ficha de película y en la
  de serie) entrega la dirección al shell de Windows, que abre un navegador de verdad.
- **Los selectores de archivo y carpeta** (copias, restauración, añadir una raíz) abren un diálogo
  modal del sistema que ningún arnés puede contestar.

**Regla, y se aplica a los tres:** una ejecución cuya raíz de datos **no** es la del perfil —un
arnés, el paseo, una comprobación de ciclo de vida— **no escribe ni abre nada fuera de esa raíz**. En
vez de abrir el navegador, escribe la dirección que habría abierto; en vez de abrir el diálogo,
resuelve la ruta que su raíz declara. Quien es dueño del perfil se comporta exactamente igual que
hoy. No es sólo para poder pulsar: **hoy nadie comprueba que la dirección que se abriría sea la
correcta**, y con esto se comprueba.

### El orden

1. ~~**La regla de aislamiento, y con ella los dos `DetailsTrailerLinkAction`.**~~ **Hecha el
   2026-08-16.** `IAppDataPaths.SystemHandoffDirectory`: una raíz que no es la del perfil recibe una
   carpeta donde anotar lo que habría entregado a Windows, y quien es dueño recibe `null` porque la
   distinción no es dónde ocurre la entrega sino **si** ocurre. Las negativas del enlace salieron a
   `ExternalLinkPolicy`, en el dominio, y las usan las dos salidas. El paseo lee las dos direcciones
   —`FilmTrailer` en la película, `ShowTrailer` en la serie— en el orden en que se pulsaron. De regalo,
   la puerta de cobertura destapó una **guarda inalcanzable** (anfitrión vacío con `https` ya exigido)
   y se retiró con su medición. **75 → 73.**
2. **Tanda 5 — la bandeja de revisión (6) y los duplicados (1).** **Cuatro hechas el 2026-08-16**
   —cargar más, aceptar, rechazar y buscar a mano— con **dos defectos del producto corregidos**: el
   botón «Buscar» no se habilitaba nunca (una clase de comando privada con `CanExecuteChanged` vacío,
   superviviente de `ARQ-004`) y su evento **no lo escuchaba nadie** en `src/`; ahora llega a
   `SearchForMatch`, la contraparte manual de `IdentifyScannedFiles`. **73 → 69.**

   **Las dos reasignaciones, hechas el 2026-08-16**, con **dos defectos más del producto** y uno del
   arnés — [la evidencia](evidence/stable/audit-walk-reassignment.md). La receta previa se quedó corta
   en un punto que decidió todo lo demás: `FileReconciliationPolicy` contesta `Exact` para una
   identidad estable y para una huella única, así que **la única oferta que la aplicación produce es
   una colisión de huella**, y una colisión son **dos candidatos, y por tanto dos botones**. De ahí:
   (1) los dos «Es el mismo, reasignar» tenían el mismo nombre accesible y decidían entidades
   distintas — ahora llevan la ruta del candidato como texto de ayuda, como `EpisodeRowView`; (2) la
   fila era un `StackPanel` horizontal, que ofrece **ancho infinito**, así que la ruta no se plegaba
   nunca y empujaba el botón a **x=2234 en una ventana de 1600**: fuera de la pantalla, sin nada que
   desplazar, con la confirmación **imposible** para cualquier ruta de una biblioteca real. Es ahora un
   `Grid` con `*,Auto`. Y del arnés: `WalkLedger` leía la vista del control **después** del efecto, y
   confirmar retira la oferta en la que el botón vive, así que la identidad se toma antes de pulsar.
   **69 → 67.**
   **El radio de duplicados, hecho el 2026-08-16** y **sin un solo defecto**: el primero en cuatro
   sesiones — [la evidencia](evidence/stable/audit-walk-duplicate-version.md). Continúa la primera
   escena del paseo en vez de montar nada nuevo, se pulsa la copia que **no** es ya la efectiva, y la
   sonda es `preferred_media_file_id` del grupo —nulo antes, el archivo pulsado después—, porque sin
   preferencia guardada la política ya contesta con una de las dos y leer `IsEffective` habría llamado
   igual a «la copia mejor» y a «la que alguien eligió». **67 → 66, y la tanda 5 cerrada.**
   **Y la deuda, pagada el 2026-08-16** — [la evidencia](evidence/stable/audit-review-inbox-coverage.md).
   `ReviewInboxViewModel.cs` va de **92,13/59,26 vigilado por nadie** a **100/100 vigilado en cada
   ejecución**, y con él la lista de `eng/check-coverage.ps1` llega a seis archivos. Dos cosas que la
   medición enseñó y que valen para todo lo que viene: **pulsar los dos botones no movió una sola
   rama** —un paseo prueba que un control trabaja, nunca los caminos de cuando algo va mal—, y **una
   rama se cubre entera dentro de una sola suite o no se cubre**, porque al fusionar informes Cobertura
   se guarda el mejor de los dos para cada línea y no la unión: el lado «hay más página» lo tomaba el
   paseo, el lado «no hay más» las pruebas de interfaz, y la rama se leía a la mitad para siempre. De
   regalo, tres ramas **inalcanzables** por un `as AsyncRelayCommand` que no podía fallar pero sí dejar
   de coincidir, que es el camino de vuelta a `ARQ-004`.
3. ~~**Tanda 8 — el shell y el inicio (14).**~~ **Hecha el 2026-08-16, 66 → 52** — el mayor salto de
   una sola tanda desde que existe el trinquete, y [la evidencia](evidence/stable/audit-walk-shell-and-home.md)
   cuenta **dos defectos más**, uno de ellos el peor de la jornada. (1) **«Continuar» estaba cableado
   a nada**: `onResume: null` en el contenedor, con el botón habilitándose solo porque había progreso
   al que volver; la acción principal de la aplicación, en la primera pantalla, sin hacer nada. Ahora
   abre la sesión con la versión de la que salió la posición —`watch_state` la guarda para eso— y el
   shell se lee al pulsar, no al construir. (2) **«Mini reproductor» y «Pantalla completa» estaban
   fuera de la pantalla**, a x=1737 y más allá, y no por el tamaño de la ventana: su columna mide
   **320 px por definición** y los tres botones suman unos 800, así que no cabían a ninguna anchura.
   Ahora es un `WrapPanel`, como el de copias — tercera vez en el día que un `StackPanel` horizontal
   esconde un control. Del arnés: `Resolve` prefiere el **control de mando** cuando un nombre lo
   comparten una acción y la región a la que lleva (el botón «Inicio» y la pantalla «Inicio»), y sólo
   si eso deja exactamente uno, para no tapar el defecto de dos botones con un nombre.

   **Anotado, no re-deliberado:** `CompositionRoot.Library.cs` quedó en 97,14/100 y **no entra** en la
   lista de vigilados, porque esa lista es para archivos que **deciden**, no para los que declaran; y
   un gancho opcional que el contenedor deja a nulo **no lo caza `ServiceConsumptionTests`**, porque
   no es un registro sin consumidor. Quien lo vigila desde hoy es el paseo.
4. ~~**Tanda 9 — el onboarding de raíces (8).**~~ **Hecha el 2026-08-16, 52 → 44** —
   [la evidencia](evidence/stable/audit-walk-root-onboarding.md)—, y con **una premisa de esta cola
   desmentida**: no hay ningún selector de carpeta aquí. La carpeta **se escribe en una caja de
   texto**, así que la tanda no tenía condición previa ninguna y podría haberse hecho antes; los
   selectores de verdad (`OpenFilePickerAsync`, `SaveFilePickerAsync`) están en copias y restauración,
   o sea en la tanda 6, donde la condición **sí** sigue en pie. El defecto: **«Retirar» a x=2146 en una
   ventana de 1600**, otra vez un `StackPanel` horizontal con una ruta al lado. **Cuarta vez en el día,
   y ya es regla de la casa: lo que va junto a un dato de anchura libre se coloca en una rejilla.** Y
   del arnés: **una sonda se compara por valor** — devolver la lista de carpetas hacía que el clic de
   control «cambiara» siempre, porque cada lectura es un array nuevo; el caso vacío pasaba porque un
   array vacío es la misma instancia compartida.
5. ~~**Tanda 6a — copias y restauración (4).**~~ **Hecha el 2026-08-16, 44 → 40** —
   [la evidencia](evidence/stable/audit-walk-backup-and-restore.md)—, y **sin un solo defecto de
   producto**: el segundo en cinco sesiones. La regla de aislamiento llegó a los selectores de archivo
   sin interfaz nueva, decidida en la composición por `SystemHandoffDirectory` igual que el lanzador
   de enlaces; `HandoffArchivePicker` responde dentro de la carpeta de traspaso y responde `null`
   cuando no hay nada, que es lo que contesta un diálogo cancelado. La escena **no compone ninguna
   ruta**: exporta donde la aplicación dice y restaura lo que la aplicación encuentra allí.

   **Lo que costó una ejecución: una sonda de disco que pasa no significa que la pantalla haya
   terminado.** La carpeta de la copia se publica **antes** de que corra la continuación que fija el
   estado, así que `BackupStatusRunning` seguía en pantalla cuando la sonda ya estaba satisfecha. Se
   espera al reposo y **luego** se dice qué salió. Es la misma carrera que la escena de privacidad
   encontró desde el otro lado, y con dos apariciones ya es regla.

   **Y quedó comprobado por primera vez desde la aplicación ensamblada** que el intercambio funciona
   con el programa abierto y la biblioteca cargada: `SwapAsync` llama a `ClearAllPools()` antes de
   mover nada y el catálogo abre y cierra su conexión por operación.

   **Un hallazgo que era falso, corregido el 2026-08-17.** Esta entrada afirmaba que el segundo
   constructor de `StagedRestoreService` (`availableBytes`, `beforeSwap`) no lo usaba nadie. **Sí lo
   usa `DisasterRecoveryTests`**, y para lo que el gancho existe: `onBeforeSwap: cancellation.Cancel`
   prueba una cancelación justo antes del intercambio, y `onBeforeSwap: () => throw new IOException(…)`
   un intercambio interrumpido — los dos caminos que deciden si un fallo a mitad pierde la biblioteca
   de alguien. La llamada es `new(Paths, _ => availableBytes, onBeforeSwap)`, con el tipo inferido, y
   un `grep` de `new StagedRestoreService(` no la encuentra. **Se pregunta al compilador quién
   construye un tipo, no al buscador**: retirar el miembro y compilar cuesta un minuto y no puede
   equivocarse.

5b. ~~**Tanda 6b — «Cancelar» (1).**~~ **Hecha el 2026-08-17, 33 → 32** —
   [la evidencia](evidence/stable/audit-walk-backup-cancel.md)—. La condición previa era la de abajo y
   se cumplió sembrando: **6.000 imágenes de 50 KiB** dan una copia de **3.944 ms**, y las dos
   pulsaciones gastan **1.211 ms**. Lo que no se había previsto es cuál de las dos palancas gana: **el
   coste por archivo pesa más que el coste por megabyte**.

   - **La condición previa es dura y es la única**: lleva `IsEnabled="{Binding IsRunning}"` **y**
     `CanExecute => IsRunning`, así que el botón sólo existe mientras una copia está en marcha. Con
     la biblioteca de un arnés la copia acaba en **milisegundos** —medido el 2026-08-16 en la escena
     de la 6a, que la crea entera sin que se vea la barra—. Hay que **sembrar una biblioteca que
     tarde** —muchas filas, y artwork personal, que es lo que la copia recorre— y medir cuánto dura
     **antes** de escribir la escena. No se improvisa al final.
   - **Lo demás ya está puesto por la 6a**: la escena navega a copias, la regla de aislamiento
     responde los dos diálogos, y la sonda de una cancelación es `BackupStatusCancelled` con **ninguna
     carpeta nueva** publicada — porque una copia cancelada no debe dejar nada que restaurar.
6. ~~**El ciclo de vida en el sandbox, caducado desde `DES-001`.**~~ **Hecho el 2026-08-16** —
   [la evidencia](evidence/stable/audit-sandbox-lifecycle-reproduced.md)—, y **la premisa era medio
   falsa**: el informe archivado ya traía las nueve fases, así que lo caducado era **sólo la huella
   del manifiesto** (`402ae30c…` archivada contra `5e341b5f…` actual). Lo que de verdad faltaba era
   que el **guion versionado** supiera producirlas: `sandbox-handover.ps1` instalaba y lanzaba, y el
   propio `README-sandbox.md` declaraba manuales las cuatro del ciclo — que es exactamente lo que
   hacía depender la medición de que alguien se acordara.

   Ahora el guion hace `file-association`, `windows-upgrade`, `windows-downgrade-refused`,
   `windows-repair` y `windows-uninstall`, y `windows-launch` comprueba además que la base **no**
   acabó en la carpeta virtualizada del paquete. El anfitrión obtiene el paquete de la versión
   siguiente **resellando** el actual con la versión subida (`0.1.0.0` → `0.2.0.0`) en vez de
   construir la aplicación dos veces: lo que Windows lee para decidir si una instalación es una
   actualización es la versión del manifiesto y nada más. **Una sola ejecución escribe los dos
   informes**, porque un segundo ciclo instalaría el paquete dos veces para medir una instalación.

   Resultado: `lifecycle.json` con **doce fases en verde**, cinco nativas incluidas, y
   `PackagingTests` en 152. La base sobrevivió a la actualización con **372 736 bytes a los dos
   lados**, y desinstalar no se llevó la biblioteca.

   **Y una alarma falsa que costó un rato:** el campo de la negativa se leyó como `versi�n` y pareció
   evidencia estropeada; los bytes eran `\xc3\xb3`, o sea `ó` en UTF-8 correcto. La corrupción estaba
   en la consola que lo imprimió. Una corrección «obvia» habría tocado un guion que no tenía nada
   que corregir.
7. **Tanda 7 — el actualizador (5) y la recuperación de la base (2). 40 → 33.** Entra aquí la cadena
   decidida y pendiente: cuando el traspaso se rechaza, el mensaje debe decir **dónde está el paquete
   verificado** para que la persona lo abra ella misma — en los dos idiomas, y el diálogo de Windows
   **no se tapa ni se silencia**. **Investigado el 2026-08-16, para no redescubrirlo:**

   **Es más cara que la 6a, y el motivo es medible: la regla de aislamiento tiene que cubrir cuatro
   salidas más.** Van tres cubiertas —registro, navegador, selectores de archivo—; esta tanda
   necesita:

   - **La fuente y la descarga.** `IUpdateSource` es `GitHubReleaseUpdateProvider` contra
     `https://api.github.com/`, y `IUpdateDownloader` baja con `HttpClient` a `DataRoot/updates`
     (`CompositionRoot.Updates.cs:40` y `:45`). Un arnés no puede ni debe salir a la red. Aislado, las
     dos leen de la carpeta de traspaso.
   - **El lanzador.** `IUpdateLauncher` es `WindowsUpdateLauncher(OpenWithWindows)`, y
     `OpenWithWindows` es `Process.Start` con `UseShellExecute` (`CompositionRoot.cs:1224`). Aislado,
     anota el paquete que habría entregado — igual que el lanzador de enlaces anota la dirección.
   - **Abrir la carpeta de copias.** `HandleRecoveryAction` hace otro `Process.Start` sobre una
     carpeta (`CompositionRoot.cs:1298`).
   - **Salir.** `RecoveryExit` llama a `desktop.Shutdown()` **sólo si** el `ApplicationLifetime` es
     `IClassicDesktopStyleApplicationLifetime`, y bajo el arnés headless **no lo es**: hoy el botón no
     hace nada que sondear. Necesita un punto de apagado que la raíz decida, como los otros tres.

   **Y la recuperación tiene una condición previa propia y dura:** su vista **no está en el shell**.
   `CreateShell` la construye sólo cuando `PrepareDatabaseAsync` devuelve una negativa
   (`CompositionRoot.cs:303`), así que la escena tiene que **sembrar una base que falle** la
   integridad o la migración, y **no puede usar `ShowShell()`**, que afirma `IsType<ShellView>`.
   Necesita montaje propio.

   **Pero el montaje propio sale barato, y está medido:** `AssembledStartup.FinalContent` **ya**
   devuelve `DatabaseRecoveryView` cuando la base se niega, y en el arnés hay **sólo cinco usos** de
   `host.Shell`, **todos** `GetVisualDescendants()`. Basta con que `ShellHost.Shell` pase de
   `ShellView` a `Control`; los **67** usos de `host.ViewModel` no se tocan si el record guarda el
   modelo como opcional y expone la propiedad de siempre. No hace falta una segunda clase de anfitrión
   ni una segunda versión de `PressAsync`.

   **Los cinco del actualizador, con sus sondas:** el interruptor de comprobación automática —el
   ajuste guardado, y es el único sin condición previa—; buscar —la oferta en pantalla—; descargar
   —el paquete en `DataRoot/updates`—; instalar —lo anotado por el lanzador aislado—; y **«Cancelar»,
   que es el mismo obstáculo que la 6b**: `IsEnabled="{Binding IsBusy}"` y `CanExecute => IsBusy`.
   Con una fuente local la descarga acaba en milisegundos, **pero aquí hay una ventaja que copias no
   tiene**: la fuente es del arnés, así que puede servir despacio a propósito. Medirlo antes de
   escribir la escena.

   **DECIDIDO el 2026-08-17, no se re-delibera:**

   - **La tanda se parte, y la recuperación va PRIMERO.** **7b — recuperación (2 controles), 40 → 38**:
     necesita dos salidas (abrir carpeta, salir), su montaje cuesta un cambio de tipo, y **no depende
     de ninguna decisión abierta**. Después **7a — el actualizador (5), 38 → 33**.
   - **Cómo se sirve la fuente de actualización a una ejecución aislada.** `IUpdateSource` **se
     sustituye** por uno que lee un manifiesto de la carpeta de traspaso, y `VerifiedUpdateDownloader`
     **se conserva** con un transporte local, para que el hash, el tamaño y el `.partial` sigan siendo
     los de verdad. **Lo que NO se hace: hacer que `UpdateSigningKey.PublicKey` dependa de la raíz.**
     Eso movería una decisión de seguridad para poder probar, que es exactamente el razonamiento que
     este repositorio tiene prohibido. La verificación minisign se prueba donde ya se prueba: en sus
     pruebas unitarias, con sus propios vectores.
   - **El orden dentro de cada mitad**: las salidas primero, en un commit propio y con
     `IsolatedRunTests` cubriendo **las dos mitades de cada una** —aislada y dueña del perfil— en el
     mismo archivo, porque al fusionar Cobertura se guarda el mejor informe por línea y no la unión.
     Después los controles.
   - **«Cancelar» del actualizador va con la 7a**, no aparte: a diferencia del de copias, su fuente es
     del arnés y puede servir despacio a propósito. Si medida la siembra sale cara, se aparta a una
     7c y se dice en la evidencia, sin silencio.

   **7a — el actualizador. Empezada el 2026-08-17: 38 → 37**, con
   [el interruptor de comprobación automática](evidence/stable/audit-walk-update-automatic-check.md),
   el único de los cinco sin condición previa. **Quedan cuatro** —buscar, descargar, instalar y
   «Cancelar»—, y su investigación está cerrada; **no se re-delibera**:

   - **La dirección la trae el manifiesto, no el código.**
     `NetworkPrivacyTests.No_source_file_names_a_host_that_is_neither_declared_nor_handed_off` recorre
     `src/` buscando `https?://…` y falla con cualquier anfitrión que el registro no declare. Declarar
     uno de arnés **mentiría sobre lo que la aplicación conecta** y ensancharía `IsDeclaredHost`, que
     es en lo que confía el canario de red. Así que el manifiesto de la carpeta de traspaso trae la
     dirección **y** el anfitrión, y `VerifiedUpdateDownloader` recibe su allowlist por parámetro, que
     **ya admite** («tests hand in their loopback server explicitly»).
   - **`UpdatePolicy` exige `release.Sha256Signed`**, que es un veredicto que pone la fuente tras
     verificar minisign con la clave embebida. La fuente del arnés **lo afirma**, como un doble en una
     unitaria, y la evidencia lo dice con todas las letras: en una ejecución aislada la firma no se
     verifica porque no hay nada firmado. Lo real es lo que se conserva a propósito —hash, tamaño y
     `.partial`— sobre transporte local. **Sigue prohibido** hacer que `UpdateSigningKey.PublicKey`
     dependa de la raíz.
   - ~~**El lanzador.**~~ **Hecho el 2026-08-17** —
     [la evidencia](evidence/stable/audit-updater-handover-exit.md)—, y **sin clase ni interfaz
     nueva**: `WindowsUpdateLauncher` ya recibía la entrega como delegado, así que `ISystemHandoff`
     ganó `TryOpenPackage` y la composición se lo pasa. `OpenWithWindows` quedó sin llamantes y **lo
     dijo el compilador**. Con ello la regla de aislamiento cubre **seis** salidas. Y una asimetría
     que estaba en un comentario pasó a estar donde se decide: un proceso nulo es **éxito** para una
     carpeta —cae en una ventana ya abierta— y **rechazo** para un paquete, que es la negativa que de
     verdad ocurre en un Windows sin nada registrado para `.msix`.
   - ~~**La fuente.**~~ **Hecha el 2026-08-17, 37 → 36** —
     [la evidencia](evidence/stable/audit-walk-update-check.md)—, y con ella **«Buscar»**.
     `HandoffUpdateSource` lee `update-manifest.json` de la carpeta de traspaso; la dirección es
     **dato y no código**, y el hash y el tamaño se **declaran** en vez de calcularse del archivo,
     porque calcularlos allí haría que la verificación comprobara el archivo contra sí mismo. Las tres
     respuestas se mantienen aparte: sin manifiesto **no hay release**, ilegible es **inalcanzable**, y
     otra arquitectura es **un rechazo con su motivo**.
   - ~~**La descarga.**~~ **Hecha el 2026-08-17, 36 → 34** —
     [la evidencia](evidence/stable/audit-walk-update-download.md)—, y con ella **descargar e
     instalar**. Se sustituye **el transporte y la allowlist, nada más**:
     `VerifiedUpdateDownloader` hace el trabajo en los dos lados, así que el hash, el tamaño y el
     `.partial` son los de una instalación de verdad — y se comprueba lo contrario, que es lo que lo
     demuestra: con un paquete distinto del prometido, la descarga lo rechaza y **no deja nada**. El
     transporte **no** implementa `Range` (el descargador ya trata lo que no es `PartialContent` como
     «empezar de cero») y **no compone rutas** a partir de la petición.
7c. ~~**«Cancelar» del actualizador (1). 34 → 33.**~~ **Hecha el 2026-08-17** —
   [la evidencia](evidence/stable/audit-walk-update-cancel.md)—. El rojo archivado dice por qué
   llevaba dos tandas esperando: la pulsación de Descargar volvía con `UpdateStatusReady` donde la
   escena esperaba `UpdateStatusDownloading`, porque con el paquete en el disco al lado la descarga
   entera acaba en milisegundos. El manifiesto declara ahora una espera **opcional**, el transporte
   la sostiene con el token del que llama, y **no se tocó el producto**: lo que la cancelación
   recorre —token, interrupción, estado— es suyo.

   ~~**7b — la recuperación de la base (2).**~~ **Hecha el 2026-08-17, 40 → 38**, en dos commits y
   **sin un solo defecto de producto** —el tercero así en once tandas—:
   [las dos salidas](evidence/stable/audit-recovery-exits.md) y
   [la pantalla pulsada](evidence/stable/audit-walk-database-recovery.md).

   - **Las dos salidas son un puerto con dos métodos y un solo llamante**, elegido en la composición
     por `SystemHandoffDirectory` igual que las tres anteriores. Lo anotado es una línea por entrega
     con un verbo delante —`open-folder <ruta>`, `exit`—, y el verbo es lo que deja a una sonda
     distinguir las dos sin analizar nada.
   - **Un hallazgo que decidió el diseño, y lo decidió el compilador:**
     `IClassicDesktopStyleApplicationLifetime` **no es implementable por código de usuario** —Avalonia
     lleva un miembro cuyo nombre es el propio aviso—, así que ningún doble puede sustituirlo y la
     mitad «hay ciclo de vida» no se puede ejercitar en ningún sitio. La búsqueda se queda en
     `CompositionRoot`, donde ya vivían **dos copias literales** de la misma expresión y ahora hay
     una; lo que llega a la salida es la llamada, y las dos clases nuevas quedan al **100/100**.
   - **El montaje costó exactamente lo previsto: un cambio de tipo.** `ShellHost.Shell` de `ShellView`
     a `Control`, con el modelo de vista opcional **detrás de la misma propiedad**; los cinco usos de
     `Shell` sólo recorren el árbol visual y los sesenta y siete de `ViewModel` no se tocan. Salió
     además un `Mount` común, porque las dos formas de montar sólo se diferencian en qué contenido
     afirman.
   - **Lo que sigue fuera y está dicho:** cerrar la ventana y salir por la bandeja **siguen apagando
     directamente**. Es otro camino del producto, con el guardado de la posición alrededor, y una
     ejecución aislada que llegue por ahí sí apagaría — sin medir.
8. **Tanda 2 (resto) — el reproductor y sus superpuestos (29).** La más larga y la única que necesita
   vídeo real: pistas, salida de audio, estilo de subtítulos, marcadores, reanudar, siguiente
   episodio, cambio de versión, archivo suelto y recuperación del reproductor. **Advertencia medida:
   los cinco superpuestos restantes no fijan alineación** y se estiran sobre todo el escenario, igual
   que el de estado corregido el 2026-08-15; **cada uno se corrige en su tanda con su medición**, no
   en bloque. **32 → 3**, y los tres últimos con ellos: **0.**
9. **La cobertura de código, al mismo destino.** Hoy la puerta vigila los archivos nuevos y una lista
   corta —**trece desde el 2026-08-17**, con `AppDataPaths.cs`, `ShellExternalLinkLauncher.cs`,
   `HandoffArchivePicker.cs`, `WindowsSystemHandoff.cs`, `RecordingSystemHandoff.cs`,
   `HandoffUpdateSource.cs`, `HandoffUpdateManifest.cs`, `HandoffUpdateDownloader.cs` y
   `HandoffUpdateTransport.cs` al 100/100 porque son las **salidas** que la
   regla de aislamiento atravesó y las que deciden qué sale de la aplicación y qué alcanza—, así que
   **un archivo antiguo que empeora sigue sin vigilarse**. **Decidido**: cada archivo antiguo que una tanda toque y
   deje en el suelo entra en esa lista al cerrarla, y cuando el paseo llegue a 0,
   `check-coverage.ps1` pasa a medir **todo `src/`** con el suelo de siempre (96 % de líneas y de
   ramas) y una lista de excepciones con la regla de la casa: **sólo puede encoger**.
10. **Rediseño y documentación**, con el paseo entero como red.

**Una tarea decidida y medida el 2026-08-16, sin sitio propio en la cola todavía: lo que queda de
`ARQ-004`.** Sobreviven **nueve** clases de comando privadas con `CanExecuteChanged { add { } remove
{ } }` —en `LibraryViewModel`, `RootOnboardingViewModel`, `ShortcutSettingsViewModel`,
`DatabaseRecoveryViewModel`, `AppearanceSettingsViewModel` (dos), `LifecycleSettingsViewModel`,
`ShellViewModel` y `WindowsTrayService`—. **Ocho son inertes** porque su `CanExecute` es constante.
La novena, la de `LibraryViewModel`, **sí lleva predicado** (`BackCommand` con
`Surface != LibrarySurface.Browse`), que es exactamente la forma que dejó el botón «Buscar» apagado
para siempre; hoy **no muerde**, y eso está medido: el paseo pulsa `LibraryBackAction` y funciona,
porque la vista se hace visible y el botón vuelve a preguntar. **Decidido**: no se tocan por ahora
—no hay defecto observable y sustituirlas es una migración mecánica sobre nueve archivos, que en esta
casa exige tres redes—, pero se hacen **en una tanda propia, después de la 2**, cuando el paseo cubra
los 128 controles y pueda servir de red. Si antes de eso alguien añade un `CanExecute` condicional a
cualquiera de las ocho, esa clase se sustituye **en ese mismo cambio**.

**Lo que ningún arnés headless puede probar, y por tanto no se disfraza de cubierto:** la imagen en
una pantalla física y TMDB contestando por red. Eso es el paseo físico de diez minutos, y es del
propietario.

**Una decisión aplazada, y por qué.** Las evidencias del 2026-08-16 **no se han añadido a
`FEATURES.md`**. `EvidenceLinkTests` exige que la matriz y
`docs/evidence/mvp/verification-manifest.json` citen exactamente lo mismo, y el manifiesto **describe
un artefacto**: su procedencia es la del paquete, así que regenerarlo con el `artifacts/package/` de
otra compilación escribiría una procedencia que no es la de nadie. Regenerar el manifiesto es parte de
cortar una versión, no de una sesión de trabajo. **Decidido**: entran en la matriz **cuando se
regenere el manifiesto con un paquete recién construido** —ya son trece, así que ese paso deja de ser
opcional en la próxima versión— y hasta entonces viven en `docs/evidence/stable/`, enlazadas aquí:

1. [el enlace al tráiler](evidence/stable/audit-walk-trailer-links.md)
2. [la bandeja de revisión](evidence/stable/audit-walk-review-inbox.md)
3. [las reasignaciones](evidence/stable/audit-walk-reassignment.md)
4. [la copia que se reproduce](evidence/stable/audit-walk-duplicate-version.md)
5. [la cobertura de la bandeja](evidence/stable/audit-review-inbox-coverage.md)
6. [el shell y el inicio](evidence/stable/audit-walk-shell-and-home.md)
7. [el onboarding de raíces](evidence/stable/audit-walk-root-onboarding.md)
8. [las dos salidas de la recuperación](evidence/stable/audit-recovery-exits.md)
9. [la pantalla de recuperación pulsada](evidence/stable/audit-walk-database-recovery.md)
10. [el permiso para buscar actualizaciones](evidence/stable/audit-walk-update-automatic-check.md)
11. [la entrega del paquete a Windows](evidence/stable/audit-updater-handover-exit.md)
12. [buscar actualizaciones sin red](evidence/stable/audit-walk-update-check.md)
13. [la descarga y la confirmación](evidence/stable/audit-walk-update-download.md)

Estado al cerrar la **segunda sesión del 2026-08-16**, que ejecutó el paso 1 entero y cuatro
séptimos del 2. **Tres commits**: `1d80815` (una ejecución aislada dice a dónde habría ido el
navegador), `d91b497` (la puerta encontró una guarda que nunca corrió) y `a799f17` (el botón Buscar
de la bandeja no se podía pulsar, y no hacía nada si se pudiera). Antes, la primera sesión del día
dejó cinco: `5f85fbd` (el renombrado renombra), `3eab024` (el paseo dice dónde fue el clic),
`5f96ac3` (vuelve arriba antes de pulsar), `679d9f1` (arranque aislado y los veinte ajustes) y
`2596bf6` (esta cola). La versión inglesa está en
[NEXT-SESSION.en.md](NEXT-SESSION.en.md). El registro canónico del alcance sigue siendo
[FEATURES.md](FEATURES.md) —**43 verificados, 1 fuera de alcance, 2 bloqueados** (`PLY-004`,
`PRD-002`)—; el trabajo pendiente de la auditoría vive en
[2026-08-08-audit-remediation.md](superpowers/plans/2026-08-08-audit-remediation.md). Esto es sólo el
punto de retomada.

## Verificación de arranque

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet --version                                        # 10.0.302
git status --short --branch                             # limpio, sobre origin/codex/ap-reelume-mvp-x64
git merge-base --is-ancestor main HEAD; $LASTEXITCODE   # 0
```

## Dónde vive el código ahora

`apvisualsolutions/ap-reelume` es **público** desde el 2026-08-10, con un corte fresco de un solo
commit raíz. El historial completo de desarrollo quedó en `apvisualsolutions/ap-reelume-archive`
(privado), de modo que **los SHA que citan los documentos de evidencia resuelven en el archivo, no
aquí**. Los remotos locales son `origin` (público) y `archive` (el histórico), y las ramas viejas se
conservan como `archived/main` y `archived/codex/…` apuntando al archivo.

La CI corre en runners hospedados, gratuitos en repositorios públicos. El runner self-hosted sigue
instalado en `.runner/` (ignorado por git) pero **apagado**, y el workflow ya no tiene forma de
llamarlo.

## Lo terminado el 2026-08-10 (segunda sesión, la legal)

Cuatro commits, cada uno con su ciclo completo y su evidencia bilingüe.

- **El artefacto entrega las licencias que nombra.** Era el único incumplimiento legal abierto.
  `licenses/`, dentro de los dos artefactos, lleva los cinco textos canónicos —LGPL-2.1, GPL-2.0,
  Apache-2.0, MIT, BSD-3-Clause— y los avisos de copyright de ANGLE, SkiaSharp, HarfBuzzSharp,
  BouncyCastle, SQLite, SQLitePCLRaw y VideoLAN: quince archivos, 209 KiB. El aviso nativo de Skia y
  HarfBuzz destapó **veintitantas bibliotecas** que `libSkiaSharp.dll` transporta —freetype, ICU,
  libpng, libwebp, zlib— y que no aparecían en ningún documento del proyecto. Nada está transcrito:
  `LicenceTextTests` compara cada copia byte a byte contra el paquete NuGet que la compilación
  consumió, y lee cada copyright del `.nuspec` restaurado. Detalle en
  [audit-legal-licence-texts.md](evidence/stable/audit-legal-licence-texts.md).
- **El logotipo de TMDB está en Créditos**, que cierra el último punto abierto de sus términos. El
  archivo es el que TMDB publica y se puede demostrar: la huella SHA-256 que ellos incrustan en la
  dirección del recurso coincide con la del archivo versionado. Sólo publican SVG y Avalonia no dibuja
  SVG, así que la vista lleva la geometría del archivo —una prueba las compara carácter a carácter—
  en vez de traer un renderizador y media docena de paquetes con sus licencias. La especificación
  decía 24 px frente a 48 px del nombre del producto; ese 48 no existía en ninguna vista, así que se
  midió y quedó en 16 frente a 24. Detalle en
  [audit-legal-tmdb-logo.md](evidence/stable/audit-legal-tmdb-logo.md).
- **ARQ-001 / WIN-005 / resto de BUG-004.** El proveedor de servicios tiene dueño y se libera al
  salir; `PendingActivationPath` y el estado de la sesión de reproducción salieron de los estáticos.
  `DisableParallelization` se retiró de `AssembledShellSuites` y las 70 pruebas de accesibilidad pasan
  sin él, que es la prueba de que la propiedad es real. `WindowLifecycle` se extrajo, la puerta de
  cobertura lo midió al 70,89 % de líneas y 28,57 % de ramas, y **volvió** igual que
  `WindowsFilePickers`. Detalle en
  [audit-arq001-application-host.md](evidence/stable/audit-arq001-application-host.md).
- **La integración continua dejaba de hablar durante una hora.** Seis de las diez ejecuciones de esta
  sesión murieron al techo de sesenta minutos, con el registro mudo desde «compilación correcta»
  hasta la cancelación cincuenta y seis minutos después. El paso siguiente,
  `eng/generate-test-media.ps1`, arrancaba FFmpeg sin ningún tope. Ahora cada llamada tiene techo,
  cada muestra se anuncia antes de producirse, y una prueba con un codificador que nunca vuelve
  comprueba que el guion muere en segundos nombrando la receta. Producir la matriz entera cuesta
  1,6 s medidos, así que el techo no es un presupuesto de rendimiento. La primera ejecución con el
  techo puesto falló en cuatro minutos y nombró al culpable: `mkv-dual-audio-english-first`. Las once
  recetas que usaban `-shortest` —un bloqueo documentado de FFmpeg— fijan ahora la duración de salida
  de forma explícita. **El cuelgue nunca se reprodujo en local**: es una carrera, y lo que se retiró
  es la clase de riesgo, no una reproducción. Si vuelve a aparecer, ahora dirá en qué receta.
- **Los dos endurecimientos que la auditoría archivó como «no explotables».** Uno lo era mucho menos
  de lo anotado: el lanzador externo entregaba al shell de Windows un `.ps1`, un `.txt` y un archivo
  sin extensión. El otro no existía en la forma descrita, y sólo se supo forjando el archivo que lo
  habría explotado. Detalle en
  [audit-hardening-launcher-and-restore.md](evidence/stable/audit-hardening-launcher-and-restore.md).

## Lo que sigue

La cola de dos —la última deuda de cobertura y la instrumentación del rojo intermitente— **se
ejecutó entera el 2026-08-10** (quinta sesión), y `BUG-010` cayó detrás. **La cola siguiente está
decidida entera en
[el plan](superpowers/plans/2026-08-08-audit-remediation.md): forma, sitio, primera medición y
criterio de aceptación de cada una.** Se ejecuta en este orden y no se re-delibera; lo que sí se
hace en cada una es **medir antes de corregir**, porque tres premisas escritas se han caído esta
semana al medirlas.

1. ~~**`BUG-011`**~~ **Hecho el 2026-08-14**: una sola cola de liberación para el proceso. La
   fábrica aprendió a vaciar a petición con techo que no lanza; el motor soltó su cola, su candado,
   su bandera, su drenaje y su constante de reposo; la lista que sólo puede encoger de
   `NativeInstanceOwnershipTests` quedó **vacía**. El orden «medios antes que reproductor» y la
   ventana de 1 s siguen intactos.
   [audit-bug011-engine-release-queue.md](evidence/stable/audit-bug011-engine-release-queue.md).
2. ~~**`ARQ-013`**~~ **Hecho el 2026-08-14**: la lectura de referencias salió a `SurfaceReferences`
   y ahora quita los comentarios antes de casar. Medido antes de tocar: **nada** se escondía detrás
   de un comentario, así que la puerta era ciega pero no estaba tapando ningún huérfano.
   [audit-arq013-reachability-comments.md](evidence/stable/audit-arq013-reachability-comments.md).
3. ~~**`ARQ-014`**~~ **Hecho el 2026-08-14**: la versión sale del ensamblado y la prueba afirma sobre
   la cabecera que sale de verdad, con la versión esperada leída de `Directory.Build.props`.
   [audit-arq014-updater-identity.md](evidence/stable/audit-arq014-updater-identity.md).
4. ~~**`ARQ-012`**~~ **Hecho el 2026-08-14**, y adelantado a `ARQ-014` porque ese necesita leer
   `Directory.Build.props` desde la raíz y sin ancla compartida habría escrito una copia más. La
   estimación del plan se quedó **cinco veces corta**: 59 archivos buscaban la raíz y 56 nombraban
   el ancla.
   [audit-arq012-repository-anchor.md](evidence/stable/audit-arq012-repository-anchor.md).
5. ~~**`QA-001`**~~ **Hecho el 2026-08-14**, y la medición dio la vuelta a la tarea: **cero avisos**
   en toda la solución, así que no era deuda sino puerta — y hacía falta igual, porque las tres
   reglas vienen apagadas. El cero se comprobó con un canario antes de creérselo.
   [audit-qa001-culture-gate.md](evidence/stable/audit-qa001-culture-gate.md).
6. **Documentación al final**: `DOC-101`, `DOC-201`, `T44.1`-`T44.6` y el manual de usuario, que se
   escribe desde la aplicación construida y no desde el código.

**Decisión sobre la primera publicación**: la tubería ya no está bloqueada —el secreto de firma está
puesto—, pero **no se corta `v0.1.0` todavía**. Faltan dos cosas que no son de código y no las
decide un agente: el dictamen `REL-004` y el paseo físico. Publicar antes sería cambiar una
verificación pendiente por una fecha.

## Un rojo intermitente que hay que vigilar, no tapar

El 2026-08-10, la fase `first-launch` de `verify-package.ps1` **falló una vez** en la rama y **pasó
con el mismo commit** en `main`. Mismo código, mismo flujo de trabajo, resultado distinto: es
intermitente, y por eso no es un defecto que se pueda encontrar leyendo.

Lo que se sabe, medido del registro:

- La fase duró **137 s**, que es exactamente el plazo de ventana (90 s) más el de cierre (45 s). Los
  dos se agotaron y el proceso acabó matado, de ahí el `exit code -1`.
- En **esa misma ejecución**, `repair`, `downgrade-refused`, `open-with` y las cuatro fases
  `windows-*` arrancaron la aplicación y la vieron pintar. Sólo falló el primer arranque.
- ~~El primer arranque es el único que migra de verdad, y migrar bloquea el hilo de interfaz.~~
  **Medido el 2026-08-10 y desmentido**: cada ciclo recibe su propia carpeta, así que los cinco
  migran una base nueva, y un arranque entero con sus dieciséis migraciones cuesta **2 292 ms**
  frente a los **90 000 ms** de plazo que aquel fallo agotó. Lo que falló allí no fue un arranque
  lento sino uno que no llegó a ocurrir, y la causa candidata sigue abierta. Detalle en
  [audit-arq005-startup-baseline.md](evidence/stable/audit-arq005-startup-baseline.md).
- **Frecuencia observada: una de cuatro.** No se repitió en las tres ejecuciones siguientes que
  llevaban ese mismo código, ni en las ocho posteriores hasta `fa968de`. Es la cifra contra la que
  comparar si vuelve a verse.
- **Y media pregunta ya estaba contestada en el registro archivado, sin que nadie lo hubiera
  leído.** La línea entera de aquella fase decía `16 migration(s) applied to a new database`: el
  proceso vivió lo suficiente para aplicar las dieciséis, así que «murió antes de migrar» estaba
  descartado desde el principio y las dos hipótesis nunca fueron dos. Lo que no dice ningún registro
  es la otra mitad —si al llegar el plazo quedaba algo vivo que pintar—, porque `exit code -1` es el
  matarile del propio arnés.

**Lo que ya no falta**: desde el 2026-08-10 la verificación **deja diagnóstico** cuando la ventana no
llega. Antes de matar el proceso anota si seguía vivo, cuánto procesador gastó y en cuántos hilos —lo
que separa girar de esperar—, el estado de `library.db` y de `schema_history`, y qué hay en la
carpeta de datos, todo en la misma línea que CI imprime. Detalle en
[audit-first-launch-instrumentation.md](evidence/stable/audit-first-launch-instrumentation.md).

**Lo que no se hace**: subir el plazo de 90 s. Eso convierte la única señal que hay en silencio, que
es el error que ya costó seis ejecuciones con los `cancelled` del generador de medios. Si vuelve a
aparecer, deja de ser trabajo pendiente y pasa a ser la corrección urgente — y ahora dirá algo al
hacerlo.

## Lo terminado el 2026-08-10 (tercera sesión)

Cuatro commits, cada uno con su ciclo, su evidencia bilingüe y sus puertas.

- **ARQ-010 — el contenedor se revisa al construirse.** `ValidateOnBuild` encendido, con una prueba
  que le pasa una colección rota **por la ruta del producto**; afirmar sobre una copia de las opciones
  sólo demuestra la copia. **No destapó ningún registro roto**, que era lo que el plan esperaba, y el
  límite quedó medido: valida 109 de 156 registros, porque los 45 construidos con una factoría son
  opacos por construcción. Cuesta +0,22 ms.
  [audit-arq010-container-validation.md](evidence/stable/audit-arq010-container-validation.md).
- **ARQ-004 — un fallo tiene dónde caer, y ya no puede cerrar la aplicación.** La medición invirtió
  el orden de sus dos mitades: `AppDomain.UnhandledException` **no** impide que el proceso termine,
  sólo deja constancia, así que un comando tiene que capturar siempre — y algo que captura siempre
  necesita un destino siempre. Ese destino no existía (2 de 24 superficies tienen estado de fallo), y
  buscándolo apareció que **el informe de diagnóstico se construía de una sola fuente**, la auditoría
  de renombrados: en una sesión sin renombrados, una aplicación que fallaba parecía sana. Luego, de
  **27 `async void` a 2**, y los dos capturan. −582/+227 líneas.
  [audit-arq004-failure-net.md](evidence/stable/audit-arq004-failure-net.md) y
  [audit-arq004-single-command.md](evidence/stable/audit-arq004-single-command.md).
- **ARQ-005, primera mitad — el candado que nadie podía abrir.** La espera de las teclas multimedia
  salió del `lock` y recibió techo. Bloqueaba el hilo de interfaz en **cada apertura de vídeo**, y sin
  contestación el hilo atrapado sujetaba el candado que la cancelación necesitaba.
  [audit-arq005-media-keys.md](evidence/stable/audit-arq005-media-keys.md).

## Lo terminado el 2026-08-10 (cuarta sesión)

Tres commits, cada uno con su ciclo, su evidencia bilingüe y su verificación completa.

- **La verificación dice cuánto se espera a la ventana, no sólo que llegó.** Y en su primera
  medición desmintió dos cosas que estaban escritas aquí: los **cinco** ciclos migran una base nueva
  —contadas las migraciones en cada carpeta—, no sólo el primero, y el primer arranque tampoco es el
  más lento de los tres. Se miden tres fases y no una **a propósito**, y esa decisión se pagó sola:
  al comparar el antes y el después, `open-with` se repitió con 6 ms de dispersión mientras
  `first-launch` variaba 1245 ms entre ejecuciones del mismo código.
  [audit-arq005-startup-baseline.md](evidence/stable/audit-arq005-startup-baseline.md).
- **ARQ-005, segunda mitad: la ventana existe mientras migra.** La medición previa decidió la forma
  y evitó la corrección falsa: `MigrateAsync` **no cede el hilo en ninguno de sus `await`s** —140 ms
  de 140—, así que un `await` habría dejado la ventana igual de bloqueada con aspecto de arreglada.
  El trabajo va a un hilo propio y la ventana sale en el primer fotograma. De regalo, la prueba de
  superficies huérfanas cazó `StartupView` en el acto y pasó a ser la tercera raíz del grafo.
  [audit-arq005-async-startup.md](evidence/stable/audit-arq005-async-startup.md).
- **TST-001: la puerta de cobertura ya vigila código que no es nuevo.** Re-medir fue lo primero y
  el resultado no era el esperado: dos archivos exactamente igual que el día anterior y el tercero
  **quince puntos peor**, porque ARQ-004 se llevó por delante sus líneas cubiertas sin que nada
  avisara. Hay una lista de vigilados con trinquete en los dos sentidos, y dos de las tres deudas
  quedaron al 100 %. [audit-tst1-coverage-debt.md](evidence/stable/audit-tst1-coverage-debt.md).

## Lo terminado el 2026-08-10 (quinta sesión)

Tres commits de código, cada uno con su ciclo, su evidencia bilingüe y su verificación completa.

- **`BUG-010`: la instancia nativa tiene un dueño.** El sondeo de medios levantaba su propia LibVLC
  con las mismas tres opciones que la de reproducción, así que un proceso que catalogaba y
  reproducía mantenía dos motores nativos — y el contador que afirma «una por juego de opciones» no
  podía ver el segundo. Con la cola pasó lo mismo y era peor: desechaba el medio **sin guarda** y
  dejaba su bandera en alto, de modo que un único fallo al liberar habría matado al trabajador para
  siempre. La regla es de código fuente a propósito, porque en ejecución la segunda instancia es
  invisible; y encontró la clase donde el plan nombraba un caso, con una **tercera** cola en el motor
  de reproducción anotada como `BUG-011`.
  [audit-bug010-native-instance.md](evidence/stable/audit-bug010-native-instance.md).

- **TST-001 queda saldado: la última deuda pasa de 86,73 %/76,00 % al 100 % de líneas y de ramas**,
  con nueve unitarias que apuntan a lo que `ReconcileScannedFiles` **decide** —un escaneo cancelado,
  un resultado que el escaneo no pudo catalogar, una ruta sin fila, una identidad ilegible que cuenta
  como fallo sin costarle el escaneo al resto, contenido `Updated` que refresca la identidad, un
  catálogo que lanza, una cancelación que no es un fallo—. Medir la lista antes de escribir la
  recortó en un tercio: cinco de los puntos anotados leyendo el código ya estaban cubiertos. Y
  apareció uno que la lectura no da: la propiedad `AttemptedCount` no la leía **ninguna** prueba,
  porque comparar registros enteros va por campos y no por propiedades. El suelo de la puerta sube a
  100/100. [audit-tst1-reconcile-coverage.md](evidence/stable/audit-tst1-reconcile-coverage.md).
- **El rojo intermitente ya deja diagnóstico.** Y lo primero fue leer el registro archivado de la
  única ejecución que falló, que contestaba media pregunta: `16 migration(s) applied to a new
  database`. Lo que se instrumenta es la mitad que sigue sin recogerse, más el procesador y los
  hilos, que separan girar de esperar. Nada del diagnóstico puede lanzar —sustituiría al fallo que
  viene a explicar—, así que cada lectura va guardada y lo que salga mal se cuenta dentro de la
  frase. `LaunchDiagnosisTests` saca las funciones del guion publicado **parseándolo** y las ejerce
  contra procesos de estado conocido, incluida una `library.db` que no es una base de datos.
  [audit-first-launch-instrumentation.md](evidence/stable/audit-first-launch-instrumentation.md).

## Por dónde se sigue (decidido, no se re-delibera)

1. ~~**`LIB-015`**~~ **Hecho el 2026-08-14**, en el orden que fijaba el plan. Tres cosas cambiaron al
   medirlas: el lanzador endurecido que se iba a reutilizar **no existía** —los tres `Process.Start`
   del árbol abren un `.msix`, una carpeta y un archivo de medios, y ninguno una dirección—; la clave
   de la caché **no incluye la dirección**, así que `append_to_response` habría servido el payload
   anterior como respuesta nueva, y **subir `ProviderVersion` habría sido peor** —esas filas dejarían
   de leerse y el techo de 180 días sólo se aplica al leer esa misma clave, así que nada podría
   borrarlas nunca—, de modo que la migración vacía lo de TMDB; y la puerta de red cazó
   `www.youtube.com` en `src/`, resuelto con una **segunda lista cerrada** (`HandedOff`) en vez de
   declarar una conexión que no existe.
   [audit-lib015-provider-trailer.md](evidence/stable/audit-lib015-provider-trailer.md).
2. ~~**El eslabón que falta**~~ **Hecho el 2026-08-15**, en cuatro commits y con `LIB-006` de vuelta
   en `VERIFIED` (43 verificados, 2 bloqueados). La primera medición que el plan exigía dio más de lo
   esperado: el puente `media_file_id` → `title_id` **es la identidad del GUID** y no costó ninguna
   migración —`titles` no lo escribe nadie en `src/`, así que todo título del catálogo es un archivo
   escaneado—, y lo que sí faltaba era el **nombre del proveedor**, que no viaja con el candidato y
   sin el cual `GetDetailsAsync` lanza.
   [audit-apply-identification.md](evidence/stable/audit-apply-identification.md).
   Detrás salieron **dos defectos que nadie buscaba**: guardar por primera vez en una ficha sin
   editar devolvía `NotFound` en silencio, y **ningún llamante subía la revisión al guardar**, de modo
   que el control optimista comparaba contra un número que nunca se movía y dos ventanas podían ganar
   las dos. Los dobles en memoria sí la subían, que es por qué ninguna prueba unitaria podía verlo.
   [audit-refresh-resolves-itself.md](evidence/stable/audit-refresh-resolves-itself.md).
   Y el clic destapó el tercero: **el paseo ensamblado montaba la ventana de una forma que la
   aplicación no usa**, dejando el shell fuera del árbol lógico y **todos** los botones enlazados por
   `Command` declarándose deshabilitados. Sólo se veía haciendo clic, y nadie hacía clic.
   [audit-walk-clicks-the-editor.md](evidence/stable/audit-walk-clicks-the-editor.md).

   ~~**El eslabón que falta, tal como se describió el 2026-08-14.**~~ La primera medición de `LIB-016` destapó
   que **nada convierte una identificación en metadata guardada**: `catalog_metadata` sólo lo
   escriben el editor y un `RefreshMetadata` que nadie alimenta —la única asignación de su entrada en
   todo el repositorio está **en una prueba**—, `ResolveMatch` publica un evento que no escucha nadie,
   y `ReviewState.Automatic` sólo se calcula. La sinopsis de `LIB-013` y la clave de `LIB-015` sólo
   llegan a la base a mano.
   **Decidido entero, con el orden de los commits**, en
   [audit-identification-never-reaches-the-catalogue.md](evidence/stable/audit-identification-never-reaches-the-catalogue.md):
   un caso de uso `ApplyIdentification` con sus dos llamantes, `RefreshMetadata` resolviendo por la
   referencia guardada, el editor sin la propiedad que nadie rellena, y el paseo ensamblado llegando
   al editor con clics. **La migración `0018` ya dejó la base preparada.**
   **La primera medición, antes de escribir una línea**: cómo se llega del `media_file_id` de los
   candidatos al `title_id` de `catalog_metadata`. Ese puente no está medido.
   **Ya se corrigió el estado de la matriz**: `LIB-006` pasó a `BLOCKED` el 2026-08-14 con su bloqueo
   en el manifiesto —42 verificados, 3 bloqueados—, y `LIB-007` **se queda `VERIFIED`** a propósito,
   porque su criterio es de umbrales y de corrección persistente, y ambos se cumplen. `LIB-006` vuelve
   a `VERIFIED` sólo cuando el recorrido con clics esté verde.
3. ~~**`BUG-012` — el vigilante que muere al desbordar su buffer.**~~ **Hecho el 2026-08-15**, y la
   primera medición desmintió la última mitad de lo que estaba escrito aquí: un root `Continuous`
   **nunca sale** de `_watching`, porque `StartAsync` es un `Task.WhenAll` con el planificador de
   reserva y ése **no termina nunca**, así que ni un escaneo manual podía revivir al vigilante —sólo
   volver a arrancar la aplicación—. Un desbordamiento es ahora «he perdido eventos»
   (`WatchErrorPolicy` en el dominio), viaja como `FileChangeBatch.EventsLost`, se convierte en **un**
   escaneo de recuperación y **la vigilancia sigue**; el buffer se pide al techo de 64 KiB; y un
   vigilante que muere de verdad se reintenta en la siguiente pasada de reserva, que es el latido que
   esta parte ya tenía. **El desbordamiento real no se reproduce en esta máquina** —64 000
   operaciones, cero desbordamientos, con 8 KiB y con 64 KiB—, así que la decisión se prueba en el
   dominio y no se finge una prueba de integración determinista.
   [audit-bug012-watcher-survives-overflow.md](evidence/stable/audit-bug012-watcher-survives-overflow.md).
4. ~~**`LIB-016`**~~ **Hecho el 2026-08-15.** Apagado por defecto, rancio a los 90 días —por debajo
   del techo de 180, con prueba de la desigualdad—, 20 por pasada, las más rancias primero, sólo
   identificadas, y cediendo ante un escaneo o un vídeo abierto, comprobado **antes de cada ficha**.
   El interruptor vive en Ajustes → Privacidad y **no existe sin conexión consentida**. El texto del
   propósito de red cambió con el código. **La medición añadió lo que no estaba previsto**: un
   `refreshed_utc` nulo con `provider_key` no lo escribe **ninguna** ruta de producción hoy
   (`identifiedWithNoDate=0`), así que los nulos primero son la guarda de una fila que nadie escribe,
   no un caso del campo. Aceptación por ejecución: el canario de red cuenta **0** conexiones con el
   interruptor apagado y **2** encendido, en el mismo proceso hijo.
   [audit-lib016-automatic-refresh.md](evidence/stable/audit-lib016-automatic-refresh.md).

5. **El paseo autónomo de toda la aplicación. Decidido entero el 2026-08-15 y va por delante de
   `DES-001` y del rediseño**, porque es su red y no un extra. Todo lo que sigue está medido, no
   supuesto.

   **Lo medido.** 129 controles de mando en las 48 vistas —95 `Button`, 18 `CheckBox`, 8 `ComboBox`,
   5 `Slider`, 2 `ToggleButton`, 1 `RadioButton`— más 17 `ListBox`. Con ratón se pulsan **dos**:
   `RefreshProviderMetadata` y el candado del título. `MouseDown`/`MouseUp` no aparecen en ningún
   otro archivo.

   **El ancla, ya probada.** Sólo 60 de los 129 tienen `x:Name`, así que el paseo localiza por la
   **clave de recurso** tras `AutomationProperties.Name` —239 elementos la llevan, 80 pruebas la
   exigen y un rediseño no la quita—. Probado contra un control sin `x:Name`. **Los dos que no tienen
   clave sí tienen nombre**, por `{Binding}`: son elementos de lista —el título de la tarjeta, la
   ruta del duplicado— y su ancla es **el dato que el propio paseo sembró**, que es mejor todavía
   porque ata el clic a algo que la prueba controla. No hay defecto de accesibilidad ahí.

   **Una premisa que se cayó al mirarla.** Aquí se escribió que estaba «por medir si el reproductor
   es alcanzable en headless con LibVLC». Ya estaba contestado en el propio archivo del paseo: sus
   escenas corren **con el motor real decodificando fotogramas**, y una de ellas reproduce, pausa con
   la barra espaciadora y guarda un marcador a mitad de sesión. Lo que falta del reproductor no es
   llegar: es **pulsar su transporte con el ratón**.

   **La forma, decidida.** Una puerta que compara los controles de mando del árbol contra los que la
   suite **pulsó de verdad**, con una lista de pendientes que **sólo puede encoger**. El registro es
   **en ejecución** —el propio `Click` anota lo que pulsa y la puerta lee ese informe, como ya hacen
   `run-accessibility.ps1` y `run-recovery.ps1`— y **no** leyendo el fuente como texto, que es lo que
   ya se ha roto tres veces al mover código. Un control cuenta como cubierto sólo con las tres cosas:
   clic real, afirmación **sobre el efecto**, y un clic **al lado** que no hace nada.

   **El orden de las tandas**, por uso y por riesgo: (1) biblioteca y ficha —abrir, filtrar, ordenar,
   favorito, visto, valoración—, (2) transporte del reproductor, (3) editor y renombrado, (4) ajustes
   —incluido el interruptor de `LIB-016`—, (5) bandeja de revisión y duplicados, (6) copia y
   restauración, (7) actualización y recuperación. Cada tanda cierra su área y saca sus entradas de
   la lista.

   **La tanda 1 está hecha (2026-08-15).** De **2** controles pulsados con ratón a **15**, sobre
   **128 identidades** —129 declaraciones, y el único colapso es el botón Atrás, declarado dos veces
   en las dos ramas excluyentes de la biblioteca—. La puerta es `eng/check-walk-coverage.ps1`, la
   lista con motivos es `eng/walk-pending.txt` y el trinquete está en **113**. Detalle en
   [audit-walk-first-batch.md](evidence/stable/audit-walk-first-batch.md).
   - **El ancla entregada no alcanzaba el botón Atrás.** Las dos ramas de detalle viven en el árbol
     visual a la vez, así que casar por la clave encontraba **dos** controles donde un clic sólo
     llega a uno. Sólo lo que está en pantalla es candidato, y los diez botones de valoración
     —que comparten nombre accesible **por diseño**— se desempatan por el `HelpText`.
   - **Y la puerta cazó un segundo defecto en su primera ejecución**: el botón de refresco del editor
     se pulsaba por su `x:Name` y las vistas lo declaran por su clave, así que **el mismo control
     tenía dos nombres** y quedaba pulsado bajo uno y pendiente bajo el otro.
   - **El tercero es el peor y sólo apareció al buscarlo**: el clic «al lado» se ponía a un control
     de altura por encima, y en una fila que envuelve eso es **la fila anterior**. El clic de control
     de «Quitar la nota» **apagaba el interruptor de favorito**, y el paseo callaba porque su aserción
     sólo preguntaba por la nota. Un clic de control que pulsa otra cosa **es una segunda pulsación
     sin registrar**. Ahora el punto se elige **por geometría** —fuera de todo control de mando en
     pantalla— y no con `InputHitTest`, que ya se midió que no predice a dónde va un clic.
   - **Inalcanzables, medidos y nombrados**: los dos `DetailsTrailerLinkAction` —el de la ficha de
     película y el de la de serie—, porque pulsarlos entrega la dirección al shell de Windows y abre
     un navegador de verdad en la máquina que corre la puerta. Es lo que `LIB-015` decidió, así que
     es el límite del paseo, no un defecto.
   - **Pendientes de esta misma área por sembrado, no por alcance**: `MovieResumeAction` (progreso
     guardado), `MovieTrailerAction` (un archivo de tráiler descubierto por grupo de versiones) y
     `EpisodePlayAction` (la ficha de serie).
   - **Al contar los 129 se puede contar mal**: la primera medición dio **142** porque
     `<ComboBoxItem>` casa con `<ComboBox` sin un límite de palabra.

   **La tanda 2 está hecha (2026-08-15): el transporte del reproductor.** De **15** a **22**
   pulsados; el trinquete baja a **106**. Es la tanda que justifica el trabajo: encontró **tres
   defectos del producto**, los tres visibles, activos e incapaces de hacer nada, y los tres vivos
   porque **el reproductor responde al teclado él mismo**. Detalle en
   [audit-walk-second-batch.md](evidence/stable/audit-walk-second-batch.md).
   - **El recuadro de estado de vídeo cubría todo el escenario** —medido en 1280×1200 sobre 1280×1200,
     opaco— encima del vídeo y de la barra, tragándose cada clic. No fijaba alineación; ahora es un
     distintivo en una esquina. **Los otros cinco superpuestos tampoco la fijan**: se corrigió sólo el
     que la medición demostró que estorbaba, y los demás se verán en sus tandas.
   - **`PlayerViewModel` no llamaba a `RaiseCanExecuteChanged` en ninguna parte**, así que el estado
     habilitado de los botones se congelaba: se pausaba con el ratón y **Reanudar quedaba deshabilitado
     para siempre**.
   - **El deslizador de volumen era el único `OneWay` de los cinco** y su vista no tenía manejador;
     `SetVolumeAsync` tenía dos llamantes y los dos eran teclado.
   - **Y cuatro trampas del arnés**: el desmontaje **reemplazaba** el fallo de la escena dentro del
     `using`; un clic fuera de la ventana no decía nada; **el centro de un control de rango suele ser
     donde ya está** (0-200 arrancando en 100); y la caché de muestras **ignoraba la duración pedida**,
     así que un salto de 30 s se salía de un archivo de 12 y dejaba Detener deshabilitado por una
     razón ajena.

   **La tanda 3 está a medias a propósito (2026-08-15): el editor sí, el renombrado no.** 22 → **30**
   pulsados, trinquete **98**. El editor quedó entero —seis candados, Guardar y Restaurar—, y
   **Guardar afirma sobre la base**, que es el primer control del paseo cuyo efecto no está en la
   pantalla. Detalle en [audit-walk-third-batch.md](evidence/stable/audit-walk-third-batch.md).
   - **El renombrado no puede renombrar.** La aplicación ensamblada pide renombrar cada archivo **al
     nombre que ya tiene** —`new RenameRequest(file.Path, Path.GetFileName(file.Path))`— y
     `RenamePolicy` contesta correctamente `NoChange` sin operación. El plan sale **siempre vacío**,
     Renombrar y Deshacer no pueden hacer nada, y la casilla de consentimiento guarda una decisión que
     no se ofrece.
   - **Y no hay nada que componga un nombre**: ése es el único `RenameRequest` de producción del
     repositorio. **No falta un cable, falta una decisión** —cómo se llama un archivo renombrado—, y
     es del propietario. El paseo la registra en vez de inventarse un convenio, y los tres controles
     siguen pendientes nombrando esto.
5. **`DES-001`, la mitad del agente: hecha el 2026-08-15.** La descripción del manifiesto ya no es
   una cadena con una barra: es `ms-resource:AppDescription`, y `eng/build-package-resources.ps1`
   construye un recurso por idioma **declarado en el propio manifiesto**, con el texto leído del
   primer párrafo de cada README —de donde winget ya sacaba el suyo—, así que las dos vías de
   instalación dicen lo mismo. Detalle en
   [audit-des001-package-description.md](evidence/stable/audit-des001-package-description.md).
   - **Dos mediciones decidían si era posible**: `makepri.exe` está junto a `makeappx.exe`, y su
     salida es **determinista** —mismo hash desde dos directorios distintos—, que es lo que la
     comparación de reproducibilidad exige.
   - **Y una trampa que ningún error nombra**: el DOM de XML escribe `xml:space` como `d2p1:space`
     con su propio espacio de nombres, y `makepri` contesta «PRI224: root node not found», que no
     nombra ni el atributo ni el archivo. Los `.resw` se escriben como texto.
   - **Tocar el manifiesto caducó dos mediciones manuales** —`windows-lifecycle.json` degrada a
     «bloqueado» y la suite lo acepta; `updater-handover.json` pone `UpdateHandoverTests` en rojo—, y
     **se rehizo con permiso del propietario**. De paso, el ciclo **quedó versionado**:
     `eng/run-sandbox-handover.ps1`, `eng/sandbox-handover.ps1` y
     `eng/measure-handover-with-handler.ps1`. Antes el documento describía los pasos y el guion vivía
     fuera del repositorio.
   - **Tres defectos del arnés del sandbox, ninguno con un mensaje que lo nombrara**: el `&` de
     PowerShell rompe el `.wsb` porque es **texto XML** («el archivo de configuración no es válido»);
     cerrar el sandbox matando `WindowsSandboxServer` —del anfitrión— tumba la ejecución siguiente; y
     la ventana la tiene `WindowsSandboxRemoteSession`, no un `WindowsSandboxClient`, que en esta
     compilación no existe.
   - **Y un hallazgo del producto, reproducido dos veces**: sin nada registrado para `.msix`,
     `Process.Start` devuelve **nulo** y la aplicación dice «Windows no lo aceptó» —cierto: la base
     quedó en 372 736 bytes antes y después—, **pero Windows deja el diálogo «Elegir una aplicación»
     en pantalla**. Es la imagen espejo de lo que la mitad `withHandler` descarta. Medido y nombrado;
     **no tocado**.
   - **Sigue abierto**: los cinco activos de marca, que son del propietario.
5. **`DES-001` — la instalación también se ve, y hoy no está diseñada.** Los cinco activos de
   `src/ApSolutions.LocalMedia.Windows.Package/Assets/` son marcadores de posición del 3 de agosto
   —de 576 B a 7 KiB— y son **lo primero que alguien ve del producto**, antes que ninguna vista. Y
   hay un defecto medido: el manifiesto declara `es-ES` y `en-US` pero su `Description` es **una sola
   cadena con una barra dentro** («Biblioteca y reproductor de vídeo local / Local video library and
   player»), que Windows enseña tal cual en los dos idiomas. La localización de verdad va con
   `ms-resource:` y un recurso por idioma, como **ya se hace en winget**, que sí tiene sus dos
   `locale.es-ES.yaml` y `locale.en-US.yaml`. Ojo: tocar el paquete obliga a reempaquetar o las
   pruebas de empaquetado fallan.
6. **El rediseño visual**, que el propietario está preparando con Claude Design. Lo que este
   repositorio le debe es el inventario de **todo lo que se ve**, y ya está escrito y medido en
   [docs/design/SURFACES.es.md](design/SURFACES.es.md) y [SURFACES.en.md](design/SURFACES.en.md): 48
   vistas en 15 áreas, las 468 cadenas por idioma, los 23 mensajes distintos de la vista de
   actualización —15 estados y 8 motivos de rechazo, que no son errores del usuario y piden otro
   tratamiento— y los cinco activos de instalación. **`LIB-016` añade superficie visible nueva** (el
   interruptor de refresco automático y su texto), así que el inventario no se cierra hasta que esa
   entrada esté hecha.
7. **Documentación**: `DOC-101`, `DOC-201`, `T44.1`-`T44.6` y el manual de usuario, que se escribe
   desde la aplicación construida y no desde el código — por eso va el último, y sus capturas
   dependen del rediseño.

## Lo terminado el 2026-08-15 (octava sesión)

Cuatro commits, y la cadena que tenía `LIB-006` en `BLOCKED` quedó cerrada: el manifiesto lee **43
verificados, 2 bloqueados**. `ApplyIdentification` escribe lo que el proveedor sabe por sus dos
llamantes —la bandeja y el camino automático de ≥90 %, que no existía—, `RefreshMetadata` resuelve
por la referencia guardada, el editor perdió la propiedad que nadie rellenaba, y el paseo ensamblado
**pulsa el botón con el ratón**. Detalle en
[audit-apply-identification.md](evidence/stable/audit-apply-identification.md),
[audit-refresh-resolves-itself.md](evidence/stable/audit-refresh-resolves-itself.md) y
[audit-walk-clicks-the-editor.md](evidence/stable/audit-walk-clicks-the-editor.md).

**Dos defectos que no estaban en ninguna lista y salieron midiendo**: guardar por primera vez en una
ficha sin editar devolvía `NotFound` en silencio —el editor traducía eso a nada, ni conflicto ni
cambio—, y **ningún llamante subía la revisión al guardar**, de modo que `WHERE revision = $expected`
comparaba contra un número que nunca se movía y dos ventanas podían ganar las dos.

## Lo terminado el 2026-08-14 (sexta sesión)

- **`BUG-011`: una sola cola de liberación en todo el proceso.** El motor de reproducción guardaba
  la tercera, y desechaba el medio nativo **dentro de su candado y sin guarda**: una excepción allí
  salía del bucle con la bandera del trabajador en alto, así que un único fallo lo mataba para
  siempre y todo lo abierto después se filtraba en silencio. El rojo fue **doble a propósito** —la
  regla de origen, que se satisface moviendo texto, y una prueba de comportamiento que mide desde
  fuera dónde descansa el medio cuando el motor lo suelta—. `LibVlcFactory` ganó un vaciado con
  techo que **no lanza** al agotarse, el motor soltó cola, candado, bandera, drenaje y constante de
  reposo (−52/+27), y la lista que sólo puede encoger quedó **vacía**. La prueba de resistencia no
  se escribió de cero: `HandleGrowthTests` ya abría y cerraba el motor treinta veces en un proceso
  hijo, que es el único sitio donde un contador de proceso se puede leer sin que las demás suites
  escriban en él. Y esa columna **no podía ser roja antes**, lo que se dice en la evidencia en vez
  de presentarla como prueba.
  [audit-bug011-engine-release-queue.md](evidence/stable/audit-bug011-engine-release-queue.md).

- **La cola de la auditoría quedó cerrada entera**: `ARQ-013`, `ARQ-012` (−836/+196 en 88 archivos),
  `ARQ-014` y `QA-001` —esta última con **cero** violaciones que corregir: era puerta, no deuda—.
  Más dos rojos ajenos que aparecieron por el camino: el canario de red pedía un puerto de un rango
  que el sistema reserva ([audit-canary-port.md](evidence/stable/audit-canary-port.md)) y una prueba
  del corpus borraba un archivo que otra suite podía estar leyendo
  ([audit-corpus-shared-file-race.md](evidence/stable/audit-corpus-shared-file-race.md)).
- **Y un rojo que habría aparecido en la primera publicación**: la herramienta que firma **no
  compilaba** —faltaba el encabezado de licencia y su proyecto estaba **fuera de la solución**, así
  que ninguna puerta lo construía—, y `release.yml` la ejecuta con `dotnet run` en el paso que
  verifica la firma. Corregido; el proyecto entra en la solución y una regla nueva impide que otro se
  quede fuera. **No lo encontró una puerta de este repositorio**: lo encontró la sesión de IT del
  propietario ejecutando la prueba de restauración de la clave.
  [audit-release-signing-tool-build.md](evidence/stable/audit-release-signing-tool-build.md).

## El bloque nuevo: sinopsis y tráiler (2026-08-14)

Pedido por el propietario y **decidido entero** en
[2026-08-14-synopsis-and-trailer.md](superpowers/plans/2026-08-14-synopsis-and-trailer.md). Va antes
de la documentación a propósito: el manual se escribe desde la aplicación construida, y escribirlo
antes de este bloque sería escribirlo dos veces.

- **`LIB-013` hecho**: la sinopsis se lee en las dos fichas. Ya estaba guardada de punta a punta; lo
  único que faltaba era el camino de lectura.
- **`LIB-014` hecho**: el tráiler **local** se reproduce desde la ficha, con la convención de Plex,
  Jellyfin y Kodi. No hizo falta camino de reproducción nuevo: `OpenLooseFile` ya valida extensión,
  existencia y no escribe fila de catálogo.
- **`LIB-015` pendiente**: la clave de YouTube al navegador. **Cuesta una migración** —campo nuevo en
  `MetadataDetails` y columna—, más `append_to_response=videos` en la petición que ya se hace. No hay
  host nuevo.
- **`LIB-016` pendiente**: el refresco automático, apagado por defecto. **Toca un contrato de
  privacidad**: hoy el propósito declarado de TMDB dice «the metadata a person explicitly asked to
  identify or refresh», y con un refresco automático deja de ser verdad, así que ese texto cambia con
  el código. El techo de 180 días de la caché no se sube nunca.

**La decisión sobre el tráiler remoto no se re-delibera**: dentro de la aplicación sólo si es un
archivo local. La vía de LibVLC contra YouTube incumple sus términos, y su incrustación oficial
pediría un WebView con hosts sin declarar, publicidad y cookies.

## Dos cosas aprendidas el 2026-08-14 que salieron caras

- **Un reemplazo mecánico validado sólo con «compila» es un cambio sin medir.** El guion que migró
  las 59 copias del buscador de raíz se llevó el método **equivocado en catorce archivos**, porque
  buscaba el primero con la forma correcta en vez del que contiene el paseo. Ninguno llegó a un
  commit, y no por suerte: dos los cazó el compilador, **trece los cazó la regla nueva** —la propia
  puerta que ese trabajo añadía midió su propio destrozo— y tres más las suites, porque devolvían
  `…/src/…Presentation` y no la raíz. Lo que cerró el hueco no fue esperar a que algo fallara sino
  buscar en el diff **toda** devolución de un subdirectorio.
- **Un error que habla del arnés esconde el del sistema.** El canario de red falló con un
  `ObjectDisposedException` en su propio constructor, que no dice nada. La primera corrección no hizo
  pasar la prueba: la hizo **decir la verdad** —«ningún puerto libre en el rango»—, y esa frase se
  midió en un comando: Windows reserva 50996-51095 y el rango fijo de la prueba estaba entero dentro.
  No era intermitente; las exclusiones se asignan al arrancar el anfitrión.

## Pendiente tuyo (sólo lo que un agente no puede hacer)

- ~~Añadir el secreto `RELEASE_SIGNING_SECRET_KEY` al repositorio público.~~ **Hecho el 2026-08-10
  (22:46 UTC)**, y era lo único que impedía cortar la primera versión pública: `release.yml` exige
  que `SHA256SUMS.txt.minisig` exista y verifique, y se detenía ahí. Comprobado por nombre y fecha
  con `gh secret list`, que nunca enseña el valor. La copia sigue donde la dejaste (ver
  `SECURITY.md`), y **el respaldo cifrado sigue siendo la única red**: un secreto de Actions no se
  relee.
- El **paseo físico manual de diez minutos**
  ([audit-physical-walk.md](evidence/stable/audit-physical-walk.md)).
- La **copia de seguridad cifrada** de la clave de firma. El destino y el cifrado están decididos
  fuera de este repositorio (bóveda de IT); lo que importa aquí es cómo se comprueba que la copia
  sirve, y **no es que el archivo descifre**: se firma algo trivial con la copia restaurada y se
  verifica contra [`eng/release-signing.pub`](../eng/release-signing.pub). Repetir cada trimestre,
  porque un respaldo corrupto no avisa.
  - **Hecho el 2026-08-14**: la sesión de IT del propietario ejecutó la prueba que vale —restaurar,
    firmar y verificar— y el respaldo **sirve**. Comprobado por ejecución, que es la única forma.
  - **Y de paso, dos avisos sobre cómo se comprueba esto.** Desde aquí se midió el respaldo por
    **tamaño** contra un umbral, y esa medición dio «no cumple» cuando la realidad era que sí: un
    proxy puede estar exactamente en su umbral y no significar nada. Después se dedujo, de un archivo
    de 58 bytes, que no podía ser la clave porque no cabe en el **formato de fichero de minisign** —
    y este proyecto no usa ese formato de fichero, sólo su verificación. **Las dos veces el error fue
    el mismo**: sustituir la ejecución por una deducción sobre metadatos. Del tamaño de un archivo
    cifrado no se deduce su contenido, y de un formato que no se ha comprobado que se use, tampoco.
  - Repetir la prueba **cada trimestre**, porque un respaldo corrupto no avisa.
- **La notificación de exportación** a `crypt@bis.doc.gov` y `enc@nsa.gov`: el texto está redactado
  entero en [LEGAL.es.md](legal/LEGAL.es.md) y sale de tu identidad, por eso es tuya.
- **El dictamen jurídico profesional** (`REL-004`), **reducido el 2026-08-14**. Sus dos preguntas de
  licencia están cerradas por ingeniería, no por dictamen: en vez de encargar quién interpretaba el
  §6 de la LGPL-2.1, la versión pasó a la opción que ambas licencias enuncian **sin condiciones**
  —§6(d) y el último párrafo del §3 de la GPL-2.0: ofrecer el fuente desde el mismo sitio del que se
  descarga el binario—, y `release.yml` adjunta ahora `vlc-3.0.23.tar.xz` verificado y el archivo de
  LibVLCSharp. **Lo que queda del encargo es marca, dominio y la notificación de exportación**, que es
  donde el criterio ajeno aporta algo.
  [audit-corresponding-source.md](evidence/stable/audit-corresponding-source.md).
- Las decisiones económicas de siempre: certificado Authenticode, Store, hardware ARM64.

## Cosas aprendidas que conviene no volver a aprender

- **`eng/verify.ps1` no es lo que ejecuta CI**: CI corre además
  `eng/run-accessibility.ps1 -Mode Verify -Passes 2` y `eng/run-recovery.ps1 -Mode Verify -Passes 2`.
  Conviene correrlas. Pero **cuidado con la conclusión fácil**: el rojo que llegó a `main` el
  2026-08-10 apareció en **tres sitios distintos** en tres ejecuciones —pasada 1, pasada 2, y la
  suite dentro de `verify.ps1`—, así que no faltaba una puerta: **la carrera no se reproduce en
  esta máquina**. Más pasadas son más tiradas, no determinismo. Contra una carrera, lo que vale es
  quitarla, no buscarla.
- **Observar un estado transitorio obliga a esperar a que termine antes de salir.** La prueba que
  comprueba la vista de arranque afirma sobre algo que dura lo que dura el trabajo de fondo, y se
  marchaba en mitad: el `Task.Run` seguía con la base abierta cuando el desmontaje borraba la
  carpeta. Aquí terminaba a tiempo y pasaba; en un runner más lento, no. La aserción no necesita la
  espera, pero el desmontaje sí.
- **Una línea base de una sola medida no es una línea base.** La fase que había que vigilar resultó
  ser la más ruidosa de las tres —1245 ms de variación con el mismo código—, y la señal la dieron
  los dos controles que se añadieron «de más». Medir el sujeto sin medir nada con qué compararlo
  produce un número que no puede distinguir el cambio del día que hacía.
- **Un número heredado se re-mide aunque se dé por hecho que ha mejorado.** De los tres de TST-001,
  dos estaban exactamente donde los dejaron y el tercero había **retrocedido**. Lo esperado era lo
  contrario, y suponerlo habría dejado la deuda peor de como se creía.
- **Una premisa escrita en un documento propio también hay que medirla.** «El primer arranque es el
  único que migra» estaba en dos sitios y era falsa: contar `schema_history` en cada carpeta cuesta
  un minuto y descartó la causa candidata del único rojo abierto.
- **Una prueba que falla porque la corrección funcionó no se arregla en el código.** La del nombre
  accesible perdió una carrera contra el propio arranque asíncrono: para cuando miraba, el shell ya
  había ocupado el sitio. Ahí lo que se reapunta es la prueba, y decir por qué evita que la próxima
  lectura la tome por un defecto.

- **Verificar con el teclado no es verificar con el ratón.** El paseo ensamblado conducía la
  aplicación con `Window.KeyPress` y **nadie usaba los clics** de `Avalonia.Headless`. El primer clic
  destapó que el propio paseo montaba la ventana de una forma que la aplicación no usa —
  `AssembledStartup.FinalContent` **saca** el `ShellView` de su contenedor y el paseo lo remontaba en
  otra ventana—, dejando el shell **fuera del árbol lógico**. Un `Button` sólo consulta el
  `CanExecute` de su comando estando en el árbol lógico, así que **todos** los botones enlazados por
  `Command` se declaraban deshabilitados. Los que usan `Click=` no, y por eso era invisible.
- **`Window.InputHitTest` no predice a dónde va un clic** en Avalonia headless: nombraba el
  `ScrollContentPresenter` mientras el clic llegaba al botón. La guarda escrita «para asegurar» el
  clic era lo único que fallaba, y creerla habría declarado roto algo que funciona. Se afirma sobre
  el **efecto**, con un clic **al lado** como control.
- **Antes de instrumentar un fallo, léelo entero donde quedó archivado.** Las dos hipótesis del rojo
  intermitente se daban por indistinguibles en dos documentos, y la línea de aquella ejecución
  —`16 migration(s) applied to a new database`— descartaba una de las dos desde el primer día. Costó
  un `gh run view --log` filtrado. Un registro que nadie ha leído no es una pregunta abierta.
- **Una lista de huecos escrita leyendo el código es una hipótesis.** La de las ramas que faltaban en
  `ReconcileScannedFiles` se recortó **un tercio** al medirla, y el hueco que no estaba en ella
  —una propiedad que ninguna prueba leía— era el que ninguna lectura podía dar, porque la igualdad
  de un registro va por campos.
- **La puerta de cobertura lee de `HEAD`, no del disco.** Con los archivos nuevos sólo preparados en
  el índice declara «ningún archivo nuevo» y sale verde. Hay que confirmar el commit y volver a
  ejecutar `eng/check-coverage.ps1` **antes** del push, o CI será quien encuentre el rojo.
- **Un hallazgo archivado como «no explotable» es un hallazgo sin medir.** De los dos que la auditoría
  dejó anotados, uno era una entrega directa al shell de Windows y el otro no existía. No se supo
  hasta escribir la prueba que lo habría explotado.
- **Una prueba que sale verde antes de la corrección no es una buena noticia**, es la hipótesis
  avisando de que estaba mal. Ahí es donde hay que parar y volver a medir.
- **`eng/verify-package.ps1` compara dos checkouts limpios**, así que se niega a correr con archivos
  sin preparar en el índice. Un `git add -A` antes de `eng/verify.ps1` ahorra media hora.
- **Se extrae una clase cuando sus pruebas pueden seguirla**, y la puerta de cobertura es quien lo
  decide, no la intuición. `WindowLifecycle` compilaba y los recorridos ensamblados estaban en verde;
  aun así volvió, como `WindowsFilePickers` antes.
- **Las pruebas que leen la composición como texto se rompen cada vez que algo se mueve.** Van tres.
  Al sacar código de `CompositionRoot`, actualizar `CompositionSourceText` y `CompositionGraph` es
  parte del traslado, no un arreglo posterior.
- **`AppDomain.UnhandledException` no impide que el proceso termine**, sólo deja constancia. Eso
  invirtió el orden de las dos mitades de ARQ-004: si un comando no puede permitirse dejar escapar
  nada, tiene que capturar siempre — y algo que captura siempre necesita un destino siempre, así que
  el destino va antes.
- **Antes de escribir «asíncrono», comprobar que algo cede el hilo.** `Microsoft.Data.Sqlite`
  implementa buena parte de su superficie `Async` de forma síncrona. Un `await` sobre algo que no cede
  deja el hilo igual de bloqueado **con aspecto de arreglado**, que es peor que no tocarlo.
- **Migrar N clases a una supone que las N hacían lo mismo, y no lo hacían.** Dos de veinticuatro
  guardaban comportamiento propio, y lo cazó la suite, no la lectura: una comprobaba `CanExecute`
  dentro de `Execute` y de ahí colgaba una validación real. Nunca se migra sin correr la suite entera.
- **Dentro de un `async void`, una guarda de argumento no es una guarda**: lanza dentro de la máquina
  de estados y se postea al contexto, de modo que quien se equivocó nunca se entera. Hay que partir el
  método en uno síncrono que valida y otro que espera.
- **Hay rojos que no son rojos, son cuelgues.** Un candado retenido por un hilo que no vuelve no
  produce un aserto roto, produce una suite que no termina. Ahí el «rojo archivado» no existe, y se
  dice en la evidencia en vez de fingir uno.
