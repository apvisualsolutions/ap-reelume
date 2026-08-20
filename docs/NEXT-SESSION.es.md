# Dónde retomar

## Estado al abrir (2026-08-20, cierre de la sesión de tarde)

**`PlayerView`, la fase de escalares de espacio y la escala de radios están hechas.** Trece commits en
la tanda. Del paso 6 queda **una sola cosa**: **una vista por commit en el orden de `SURFACES.es.md`**.

**Y la red del desbordamiento ya no se escribe vista por vista: hay puerta.**
`ViewOverflowTests` monta **las 48 vistas** sin contexto de datos —todas las ramas visibles a la vez,
que es la cota superior— en una ventana de 900 y afirma que ningún control termina fuera. Probada
fallando a 300: nombra nueve vistas con su control y su coordenada.
[Su evidencia](evidence/stable/audit-view-overflow-gate.md).

**Su limitación está dicha y hay que respetarla:** una vista sola recibe los 900 enteros, y anidada en
el shell recibe menos. Caza la vista demasiado ancha **por sí misma**; la que sólo lo es al anidarse la
sigue cazando el paseo. **Un silencio de esa puerta no es un certificado.**

**Lo que queda por vista es, entonces, UNA SOLA COSA: `primary-action` donde la haya.** Y ésa **no se
puede barrer**, porque cuál es la acción principal de una pantalla es una decisión de esa pantalla.
Medido el 2026-08-20: **34 vistas tienen botón y sólo 3 tienen acción principal** —`ResumeHeroView`,
`PlayerView` y `UpdateView`—. Ése es el trabajo que queda del paso 6.

**Los tokens ya no tienen deuda.** `NotSpentYet` está **vacía** y hay puerta para **las tres escalas**
—tipografía, espaciado y radios— que exige que el `.axaml` **no escriba el número**. Una vista nueva
que escriba a mano un tamaño, un espaciado o una esquina falla en `ScalarTokenTests`. **Eso es lo que
hace que «una vista por commit» cueste de verdad sólo maqueta**: lo que queda por vista es
`primary-action` donde la haya, la red del desbordamiento, y nada más.

**La escala de radios se decidió aquí y no venía en el plan** (`docs/evidence/stable/audit-corner-radius-scale.md`).
Se hizo de una vez por el mismo argumento que ganó en el espaciado —es un mapeo y no una decisión por
pantalla, y sin puerta las vistas siguientes pueden reintroducir literales—, y su lección es la
contraria de la que se esperaba: **un criterio recién probado es cuando más fácil es aplicarlo mal.**
Los tres radios grandes eran las tres tarjetas y parecía faltar un `CornerRadiusLarge`; medir el otro
lado dijo que **de las siete superficies de tarjeta, cuatro ya llevaban 8**. No era un escalón que el
árbol pidiera, era un reparto que nadie decidió. **La pregunta no es «¿tiene sentido el escalón?» sino
«¿lo contradice algo que ya está en el árbol?»**

**Dos advertencias que esta tanda compró caras, y valen para todas las vistas que quedan.**

**La primera, de la fase de escalares: cuando dos mediciones discrepan, se diffan los dos comandos.**
Un recuento propio dio 163 donde el documento decía 183, y lo primero que hice fue construir una
explicación de por qué el documento estaba mal —que además cuadraba con un tercer número—. El
documento tenía razón; mi patrón llevaba un `\b` que no veía `RowSpacing` ni `ColumnSpacing`. **Una
hipótesis que encaja con los números no es una medición**, y aquí habría dejado 23 sitios sin tokenizar
con la fase declarada terminada.

**La segunda, de `PlayerView`:**
la red del desbordamiento se escribe **aunque el cambio parezca sólo cosmético**. Ahí la única prueba
que encontró algo fue la que **pasaba antes del cambio** —no era su rojo, era su red—: midió que la
fila del transporte terminaba **74 píxeles fuera** de una ventana de 900, que es el mínimo que la
aplicación permite. Séptima vez que un `StackPanel` horizontal con etiquetas traducidas saca un
control de la pantalla. **Se mide contra la anchura mínima real, no contra una cómoda.**

**Tres vistas seguidas han costado sólo maqueta** —`MiniPlayerWindow`, `UpdateView` y `PlayerView`—
porque sus controles ya estaban en el paseo. Es lo normal a partir de aquí: el paseo llegó a cero
antes de que la interfaz cambiara, que era el sentido de hacerlo en ese orden.

**Lo que costó esta tanda, y que el resto de las vistas hereda:**

1. **Una ventana secundaria no es como una vista.** `PlayerWindowCoordinator.Apply` asignaba
   `window.Content`, lo que **sustituye el árbol entero del AXAML**: la ventana mini tiraba todo lo que
   declaraba para sí misma en cuanto llegaba una sesión. `Host()` y `MiniPlayerSurface` llevaban ahí
   desde el principio y **sólo los llamaba una prueba** — el defecto de la casa, forma once.
2. **`WalkLedger.Record` exige un `UserControl` ancestro**, y el inventario de la puerta usa el nombre
   del `.axaml`. Un control declarado dentro de un `Window` no puede casar las dos mitades **jamás**.
3. **El paseo no sabía salir de la ventana del shell**, y nadie lo había necesitado. El arnés ganó
   `Reachable`, `SecondaryWindows` y `RootOf`, y cada función de clic apunta ya a la ventana **del
   control**.
4. **Las etiquetas largas no se salen: dejan sin sitio al clic de control.**
5. **Una prueba que compara el VALOR no distingue un literal de un token** mientras los dos coincidan
   — y coinciden justo cuando la tokenización sería correcta, así que el falso verde es el caso
   normal. Se afirma que el `.axaml` **no escribe el número**.

**Los cuatro rojos de CI, y los cuatro fueron puertas haciendo su trabajo:**

- **El trinquete de cobertura encontró una rama que nadie recorría.** El primer intento del mini
  reproductor enrutó por una interfaz y **dejó viva la rama vieja de `Apply`**. La corrección fue
  **borrarla**, no cubrirla: una prueba escrita para alcanzar código muerto pone el número en verde y
  deja el defecto dentro.
- **Y al borrarla el archivo BAJÓ de 100/92 a 100/91**, porque quitar ramas cubiertas sube el peso de
  las que nunca lo estuvieron. Había dos así desde siempre, ambas garantías reales sin prueba.
  Cubiertas, el archivo mide 100/100 y **sale** de la deuda. **Un suelo que baja es una bajada**:
  buscar una explicación de proceso es la forma cómoda de no mirar el código.
- **Una red calibrada en una máquina acusa a otra.** La escena de cancelar la copia comparaba el
  tiempo de sus dos pulsaciones contra una duración medida aquí. Lo que el reloj infería lo dice la
  pantalla: ahora guarda los estados por los que pasa y afirma que `BackupStatusDone` no está.
- **Y el trinquete pidió declarar tres mejoras**, que es la mitad de su trabajo que caza lo que mejora
  sin decirlo.

**Y las tres de siempre siguen valiendo:** un cambio de vistas afecta a **cuatro** suites (`UiTests`,
`AccessibilityTests`, `IntegrationTests`, `DocumentationTests`); el trinquete de cobertura **falla
también cuando algo mejora**; y `verify.ps1` **aborta en el primer fallo**, así que un rojo esconde
los posteriores.

## La cola decidida el 2026-08-16 (no se re-delibera)

**El objetivo es cero.** Esta aplicación se publica gratis y **nadie la va a probar a mano**: lo que
la suite no cubra no lo cubre nadie. El trinquete de `eng/check-walk-coverage.ps1` va a **0
pendientes** —y **desde el 2026-08-18 lo está**: **128 de 128** controles pulsados con ratón, `eng/walk-pending.txt` vacío y el trinquete en 0, que no vuelve a subir. Queda la puerta de cobertura de
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
| ~~2~~ | ~~Los tres últimos de la tanda 1~~ **hecha el 2026-08-18, 3 → 0** | agente | **0** |
| ~~3~~ | ~~La prueba de los subtítulos~~ **hecha el 2026-08-18** | agente | 0 |
| ~~4~~ | ~~Cobertura a todo `src/`~~ **hecha el 2026-08-18 como trinquete: 219 y sólo baja; corregido el mismo día para que el suelo lo mida CI** | agente | 0 |
| ~~5~~ | ~~`ARQ-004`~~ **hecha el 2026-08-18: el comando enlazado, la notificación y la puerta de los siete** | agente | 0 |
| 6 | **El rediseño**, con el material de Claude Design — **fase 2, puerta de escalares, `primary-action`, la tipografía, `MiniPlayerWindow` y `UpdateView` hechas**; quedan **`PlayerView`, la fase de escalares de espacio y el resto de vistas**, todo decidido | agente | 0, con la regla de abajo |
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

#### 5. `ARQ-004` — decidido, y la nota vieja estaba incompleta

Medido el 2026-08-18: hay **ocho** archivos con `CanExecuteChanged` vacío, no nueve clases, y **sólo
uno tiene riesgo real**. Un `CanExecuteChanged` vacío sólo importa cuando `CanExecute` mira **estado
que cambia**; si mira el parámetro, cada consulta con el mismo parámetro da siempre lo mismo y no hay
nada que notificar.

| Archivo | `CanExecute` | Riesgo |
|---|---|---|
| `LibraryViewModel` | `Surface != LibrarySurface.Browse` | **sí, estado que cambia** |
| `RootOnboardingViewModel`, `ShortcutSettingsViewModel`, `LifecycleSettingsViewModel`, `WindowsTrayService` | `true` | no |
| `DatabaseRecoveryViewModel`, `AppearanceSettingsViewModel` (×2), `ShellViewModel` | sólo el parámetro | no |

**Hecho el 2026-08-18 —
[la evidencia](evidence/stable/audit-arq004-command-notification.md)— y la nota era cierta por una
causa falsa.** Decía que hoy no muerde «porque la vista se hace visible y el botón vuelve a
preguntar». Medido: **ningún AXAML enlazaba `BackCommand`**. Los dos botones «Volver» llamaban a
`BackToLibrary()` por el code-behind, así que el predicado no se evaluaba nunca — un comando público
con predicado que ninguna vista consumía, el defecto de la casa con cara de comando. Y el rojo
predicho no podía existir por una segunda razón: cada botón vive **dentro del `Grid` de su propia
superficie**, así que mientras es visible el predicado es verdadero por construcción.

Lo que se hizo, y en qué orden:

1. **Enlazar el comando** (`Command="{Binding BackCommand}"` en los dos botones, fuera `OnBackClick`)
   **y medir**. El rojo apareció en el acto, en la escena del paseo de la biblioteca:
   `Volver a la biblioteca is on screen but cannot be pressed: visible=True, enabled=False`. Las dos
   ramas de detalle viven en el árbol visual a la vez, así que el botón pregunta al adjuntarse con
   `Surface` todavía en `Browse` y el evento vacío tira la suscripción a la basura.
2. **La notificación**, con el evento real en el `RelayCommand` privado y el disparo en el único sitio
   donde `Surface` se asigna. Verde con la misma sonda, y una prueba de unidad nueva donde **la cuenta
   es la aserción**: el predicado solo pasa avisen a quien avisen.
3. **La puerta**, `CommandNotificationTests`, con la lista cerrada de los **siete** que quedan y **el
   predicado exacto** de cada uno: vigila la pareja evento-predicado, no el evento a solas. Se probó
   fallando en las dos direcciones —un octavo sin declarar y un predicado que cambia de forma— y lleva
   su propio suelo anticeguera.

#### Lo que queda decidido del paquete de diseño, para el paso 6

- ~~**Los diez cambios de `SURFACES.es.md` / `.en.md`**~~ **hechos el 2026-08-18**, midiendo cada uno
  contra el árbol antes de escribirlo — y de diez, **tres no eran ciertos**:
  - **`MiniPlayerWindow` ya estaba** en el inventario (Reproductor (16)); la nota que decía que
    faltaba estaba superada.
  - **«`BackupView` tiene historial» no aparece** en `SURFACES`: era un error de la auditoría del
    propio paquete, que lo admite por escrito.
  - **«los siete motivos de fallo de `PlayerView`» son seis.** `PlaybackFailureCode` tiene siete
    valores, pero el séptimo —`UnsupportedCapability`— viaja en `VideoOutputDecision` y sale por
    `VideoStatusOverlay`: el vídeo **se reproduce** con conversión de tono. Pintarlo como fallo diría
    que no hay imagen cuando sí la hay.
  - Dos más no aplicaban al documento (los tokens y la versión de Avalonia no se nombran en él), pero
    sus cifras sí se verificaron y entran en la sección de temas nueva: **58 declaraciones / 40
    nombres** +3 de `Brand.axaml`, y **8** selectores de foco.
  - Lo medido que sí faltaba: **23 listas con datos y sólo 4 con cadena de vacío**, y el vacío de la
    biblioteca lo pinta `ShellView`, así que **buscar sin resultados no muestra nada**; el alto
    contraste es **uno solo** y sobre `Light`, así que quien use el oscuro de Windows recibe el claro;
    y el icono de bandeja es el **sexto** activo, en otro proyecto.

#### Las dos decisiones que quedaban del paso 6, tomadas el 2026-08-18

**1. El alto contraste NO se elige en la aplicación: siguen siendo tres píldoras.** El paquete
sustituye los tres botones de tema por cuatro píldoras, y la cuarta sólo podría ser alto contraste
—`ThemePreference` tiene exactamente `System`, `Light` y `Dark`—. **Se rechaza**, y no por trabajo:
el alto contraste de Windows es un ajuste de **accesibilidad del sistema**, y ofrecer una copia en la
aplicación crea dos fuentes de verdad para la misma necesidad. Alguien con el sistema en alto
contraste y la aplicación en «Claro» tendría una aplicación contradiciendo una necesidad declarada,
que es peor que no ofrecer nada. Además, dejar el enum como está significa que **no hay migración de
ajustes** ni valores huérfanos en los guardados.

Lo que sí falta de verdad **es el cuarto diccionario**, y ése se hace: hoy `AppThemeVariants.HighContrast`
está declarado sobre `ThemeVariant.Light`, así que quien use el alto contraste **oscuro** de Windows
recibe el claro. Se añade `HighContrastLight`, se renombra el existente a `HighContrastDark`, y toca
`AppThemeVariants` y `FluentThemeService` — **no el enum**. Si el rediseño quiere enseñar una cuarta
píldora, que sea **un estado y no una opción**: decir que el sistema está en alto contraste y que se
está respetando. Eso informa sin duplicar el ajuste.

**2. ~~`DebouncedFileWatcher` se arregla por el constructor~~ — hecho el 2026-08-18, y tal como estaba
decidido.** El búfer es ya un parámetro opcional del constructor con el valor de producto por defecto;
la prueba pide el mínimo que la plataforma respeta, desborda de verdad y **lo afirma**. Nada se volvió
`internal` y `WatchSignal` sigue privado.

**La causa registrada era una de tres.** Al medir las dos tiradas enteras aparecieron otras dos
condiciones que ocurrían o no por azar: la otra mitad del mismo manejador —el error que **sí** termina
la vigilancia, que se ejecutaba por casualidad cuando un directorio de prueba desaparecía debajo de un
vigilante vivo— y el switch de coalescencia, cuyas parejas dependían de lo que el sistema entregara
durante la tormenta (13 de 16 ramas en una tirada, 11 de 16 en la otra). Cada una tiene ya su prueba,
y una cuarta cubre el debounce que expira **en el mismo instante** en que llega el cambio que lo
cancela, con un reloj cuya espera termina con éxito al cancelarse.
[La evidencia](evidence/stable/audit-watcher-overflow-determinism.md): de **88,54/73,81 y
93,75/71,43** en dos tiradas del mismo binario a **100/95,83 en tres seguidas**. Las dos ramas que
faltan no son alcanzables.

**Y lo que se midió sin esperarlo: una tormenta secuencial no desborda 4 KiB.** Dos mil archivos con
nombres de cien caracteres, uno tras otro, no desbordaron ni una vez: el cuello de botella no es el
vigilante vaciando el búfer, es crear el archivo, y esas tres décimas de milisegundo son todo el
respiro que su hilo necesita. En paralelo desborda en el primer segundo. El primer intento salió rojo
**por la aserción nueva y no por un tiempo agotado**, que es exactamente para lo que está.
- **La discrepancia de motivos de rechazo del actualizador se resuelve en OCHO**: `README.md` dice 8 y
  `github.md` dice 7, y el que cuadra con los 23 mensajes es el 8 (15 estados + 8 rechazos).
- **Las 25 cadenas de consecuencia se aprueban contra la regla que el propio paquete da** —«si la
  frase ayuda a decidir o a actuar, se traduce; si explica por qué está diseñada así, es un comentario
  del AXAML»— revisándolas una a una al escribirlas. **No bloquean el paso 6**; lo que no pase esa
  regla se queda como comentario.
- **Los 35 activos de instalación siguen bloqueados** en el original vectorial de la marca, y no se
  improvisan.

#### Y un rojo que CI trajo en medio, con un defecto de producto dentro (2026-08-18)

El run de `ba1502e` falló **1 de 117** en el paseo:
`ConfirmSwitchButton is on screen but cannot be pressed: visible=False, enabled=True` — la pregunta
del cambio de versión se iba de la pantalla entre resolver el botón y pulsarlo. Local da 117/117 en
dos pasadas, a 2 minutos por pasada frente a los 5 m 38 s del runner, así que la ventana la abre la
lentitud de allí.

**No era del paseo.** La fila de la otra versión seguía pulsable mientras su propio cambio estaba en
marcha, el arnés vuelve a pulsar a los 300 ms lo que parece no haber hecho nada, y **todo cambio vacía
la posición del reproductor antes de decidir**: una sesión recién abierta responde cero, cero está por
debajo del suelo de reanudación, y entonces la política deja de preguntar, **abre la otra versión sin
preguntar y deja la posición guardada en cero**. Es decir: un doble clic bastaba, sin runner lento.

Corregido donde vino la segunda petición —la fila se apaga mientras su cambio está en marcha, el
patrón que la barra de transporte ya tenía— y **el eslabón que era una deducción se midió**, no se
supuso: [la evidencia](evidence/stable/audit-version-switch-reentry.md). El caso de uso no se toca.

#### La fase 2 del paso 6: el botón hecho, y lo que la plantilla propia debe (2026-08-18)

**El botón está hecho** —95 de las vistas, contra 18 casillas y 15 cuadros de texto, así que es por
donde empieza—: sus cuatro estados de color salen ya de los tokens en los cuatro temas, con borde de
1 px, y en alto contraste el paso de ratón y la pulsación **invierten**.
[La evidencia](evidence/stable/audit-redesign-phase2-button-states.md).

**Lo que se midió y decide cómo se hace el resto:**

- **Un estilo de aplicación NO alcanza los elementos de plantilla de un `ControlTheme`.** Ni
  `Button /template/ ContentPresenter` ni con `#PART_ContentPresenter`. Lo que sí alcanza es **el
  recurso que esa plantilla consume**: `ButtonBackground`, `ButtonForeground`, `ButtonBorderBrush` y
  sus tres estados cada uno. Se redirigen con `<StaticResource x:Key="…" ResourceKey="…" />`, que
  Avalonia acepta como entrada de diccionario, así que **no se duplica ni un valor**. Los demás tipos
  se hacen igual, con sus propios recursos.
- **`ControlTextActiveBrush` es un token nuevo** (van catorce), y hace falta: sin él la inversión de
  alto contraste no se puede expresar sin una regla por tema.
- **Un pincel se lee entero.** `ButtonBackgroundPointerOver` dice `Black` y lleva `Opacity 0,1`;
  leyendo sólo `.Color` se concluyó «texto negro sobre fondo negro», que es falso.

**Lo que la plantilla propia debe, y no se re-delibera:**

1. **El borde punteado del deshabilitado** (`Rectangle` con `StrokeDashArray`). Sin él, **en alto
   contraste deshabilitado es hoy indistinguible de reposo** —el relleno deshabilitado *es* la
   superficie, por diseño—, y eso está medido y afirmado tal cual en la prueba, no aflojado.
2. **El borde de 2 px al pulsar en alto contraste**: la plantilla base tiene un solo grosor para todos
   los estados. Afirmado a 1 px, que es lo cierto hoy, y **el token del grosor llega con la plantilla
   que lo gaste**, no antes: se escribió, se quedó sin consumidor al cambiar de vía, y se retiró.
3. Después: los otros ocho tipos de control, empezando por `CheckBox` (18) y `TextBox` (15).

Y dos cosas del árbol que salieron midiendo: **`primary-action`** (en `ResumeHeroView`) es una clase
que **ningún estilo define y ninguna prueba busca**, y **`navigation-destination`** sí tiene uso, pero
como **marcador de las pruebas**, no como estilo.

#### Las siete decisiones que quedaban del paso 6, tomadas el 2026-08-18 (no se re-deliberan)

1. ~~**El borde punteado del deshabilitado se dibuja como ADORNO, no copiando plantillas.**~~
   **Hecha el 2026-08-18 como estaba decidida** — [la evidencia](evidence/stable/audit-redesign-phase2b-disabled-outline.md).
   Lo que sigue queda por lo que explica el porqué: Una
   propiedad adjunta en `Presentation/Theme` que añade un `Rectangle` con `StrokeDashArray` a la capa
   de adornos cuando el control se deshabilita. **Por qué y no un `ControlTheme` propio por tipo:** el
   anillo de foco ya demostró que la capa de adornos alcanza a los diez tipos con **una**
   implementación —incluidos el `ToggleSwitch`, que lo cuelga del `Grid` de su plantilla, y el
   `NumericUpDown`, que lo cuelga de su `TextBox`—; copiar nueve plantillas de Fluent son nueve
   superficies que se desincronizan con cada actualización de Avalonia, para una raya. Archivo nuevo,
   así que **96/96 desde el primer commit**: la prueba fuerza `IsEnabled=false` y afirma el
   `StrokeDashArray` en la capa.
2. **El borde de 2 px al pulsar en alto contraste NO se hace, y esto es una desviación consciente del
   paquete.** El pulsado en alto contraste ya invierte relleno **y** texto —medido, 21:1—, así que el
   grosor añade una tercera señal a un estado que ya tiene dos, y exigiría otro adorno o una plantilla
   propia. Se documenta como decisión, no como deuda. Si el paseo físico del propietario dice que no se
   distingue, se reabre **con esa medición**.
3. **`primary-action` recibe estilo de acción primaria**, no se borra: es «Continuar» en la portada, y
   el rediseño quiere jerarquía. Necesita un token nuevo, **`AccentTextBrush`** —~~blanco en claro,
   oscuro y alto contraste claro; **negro** en alto contraste oscuro, porque el acento allí es cian~~—, y
   `ContrastTokenTests` lo mide contra `AccentBrush` con el listón de texto, 4,5:1.
   **El token llegó el 2026-08-19 con la casilla, que lo necesitaba antes, y la mitad tachada era
   falsa**: el `AccentBrush` del tema **oscuro** es `#62AEE8`, un azul pálido, y blanco encima mide
   **2,40:1**. Queda `#FFFFFF` en claro y alto contraste claro, `#111827` en oscuro y `#000000` en
   alto contraste oscuro — el color sigue la **luminancia del acento**, no el nombre del tema. Lo mide
   ya la puerta, probada fallando.
4. **`ToggleSwitch` conserva su selector de foco y NO recibe estados.** Cero usos en las 48 vistas: dar
   estados a un tipo que nadie monta es declarar sin gastar. El foco se queda porque ya está escrito y
   cuesta cero.
5. **El orden de los ocho tipos que faltan es el del uso medido**: `CheckBox` (18), `ListBoxItem` (17),
   `TextBox` (15), `ComboBox` (8), `Slider` (5), `NumericUpDown` (5), `ToggleButton` (2),
   `RadioButton` (1). Cada uno por sus **propios recursos de tema**, como el botón.
6. **Los escalares se convierten en trinquete, no en promesa.** Una prueba nueva recorre los `.axaml`
   de `src/` y exige que **cada escalar declarado lo consuma al menos una vista**, con una lista de
   excepciones nombradas —hoy `SpaceXSmall`, `SpaceSmall`, `SpaceMedium`, `SpaceLarge`, `SpaceXLarge`,
   `CornerRadiusSmall`, `CornerRadiusMedium`— **que sólo puede encoger**, igual que la lista de
   huérfanos de `ServiceConsumptionTests` y que `eng/coverage-debt.txt`. Así la deuda es visible y no
   puede crecer en silencio.
7. ~~**La tipografía**~~ — **hecha el 2026-08-19**: [la evidencia](evidence/stable/audit-type-scale.md). **Cinco tokens, no seis**: `FontSizeMono` no se declara porque nada lo gasta y la puerta de escalares lo rechazaría — llega con la primera ruta o hash que lo pida. El `17` fue a `FontSizeBody` **por lo que el texto es** (un párrafo que se ajusta), no por distancia. La línea base de `HomeLayoutTests` movió **un solo campo** y hacia la consistencia. **La tipografía: los tamaños literales del árbol se mapean a seis tokens, y el mapeo es este.**
   **Medido el 2026-08-19: son TRECE, no doce — 52 usos en 30 archivos — y el que falta en el mapeo
   de abajo es el `17` de `ShellView.axaml:140`**, que hay que colocar al ejecutar (por proximidad va
   con el 18, a `FontSizeSubtitle`, pero se mide antes de decidirlo).
   34 y 32 → `FontSizeDisplay` 32; 30, 28 y 26 → `FontSizeTitle` 28; 24, 22, 20 y 18 →
   `FontSizeSubtitle` 20; 16 y 14 → `FontSizeBody` 14; 12 → `FontSizeCaption` 12; y `FontSizeMono` 13
   para rutas, hashes y códecs. Se declaran **en la primera vista que los gaste**, no antes.

#### ~~La fase 2b: el punteado del deshabilitado~~ — hecha el 2026-08-18

**Hecha exactamente como estaba decidida** —
[la evidencia](evidence/stable/audit-redesign-phase2b-disabled-outline.md)—. Propiedad adjunta
`DisabledOutline.IsShown` en `Presentation/Theme`, un `Rectangle` con `StrokeDashArray` en la capa de
adornos, y **el cuándo lo dice un selector** `:disabled` sobre los mismos diez tipos que el foco. El
archivo nuevo mide **100 % de líneas y 100 % de ramas** —comprobado en local antes de empujar, que es
lo que ahorra los 35 minutos de CI por intento— y las 2 170 activaciones no salen de sus pruebas sino
de las vistas reales de la suite de interfaz.

**Lo que la medición añadió, y es la lección de esta tanda: deshabilitar se hereda, y un estilo de
aplicación alcanza también los elementos de plantilla.** Ocho tipos recibían un adorno y **dos
recibían dos**: `ComboBox` y `NumericUpDown` llevan un `TextBox` dentro cuyo `IsEnabled` propio sigue
en `true`, así que se dibujaban dos rectángulos punteados a unos píxeles uno de otro. La condición
correcta **no es el `IsEnabled` local sino `TemplatedParent is null`**, y las dos respuestas difieren
justo donde importa: un control dentro de un panel deshabilitado entero conserva su `IsEnabled` en
`true`, y como un panel no es de los diez tipos, mirar el flag local dejaría ese caso **sin ninguna
raya**. Hoy ese caso no existe —medido: los once `IsEnabled` enlazados del árbol están en nueve
`Button` y dos `CheckBox`, ningún contenedor—, pero la regla se escribió para cuando exista.

**Y de paso se corrigió `SURFACES`**, cuya sección de temas seguía describiendo el árbol de antes de
la fase 1: decía 3 diccionarios (son **4**), 58 declaraciones en 40 nombres (son **140** en **35**,
más 13 escalares fuera), 8 selectores de foco (son **10**) y que no había alto contraste oscuro, que
lleva un día siendo falso.

**Lo que sigue de la fase 2, en orden y sin deliberar:** los ocho tipos por uso medido —`CheckBox`
(18), `ListBoxItem` (17), `TextBox` (15), `ComboBox` (8), `Slider` (5), `NumericUpDown` (5),
`ToggleButton` (2), `RadioButton` (1)—, cada uno por **sus propios recursos de tema** como el botón;
la puerta de escalares consumidos con lista que sólo encoge; `primary-action` con el token
`AccentTextBrush`; y después la tipografía y las vistas, una por commit.

#### El rojo de CI que trajo `4b2c326`, y era del arnés (2026-08-19)

`RestartSwitchButton is on screen but cannot be pressed: visible=False, enabled=True` en la escena del
cambio de versión — [la evidencia](evidence/stable/audit-walk-press-retry.md). **No lo causó el
commit**: toca tokens y pruebas de tema, y la escena pasa seis de seis en local.

**La causa, leída en el bucle de `PressAsync`:** repite una pulsación sin efecto y antes de repetirla
mira **sólo si el control está deshabilitado**. Contestar la pregunta del cambio de versión **la
cierra** —que es lo correcto, y la escena lo afirma—, así que el botón sale del árbol al pulsarlo
mientras su comando sigue habilitado; en un runner cargado el efecto tarda, el bucle da otra vuelta y
pulsa un botón que ya no está.

**Y con eso la regla de la casa gana una excepción, que es lo que hay que recordar:** `visible=False`
acusa al producto **salvo que pulsar ese control sea justamente lo que lo quita de la pantalla**. Los
tres botones de esa pregunta son de esa clase, y también cualquier confirmación que cierre lo que
confirma.

La decisión salió a `WalkPressPolicy`, con su prueba propia —porque el caso **sólo aparece en un
runner lento**, y una regla que se ejercita por suerte no la comprueba nadie— y probada fallando con
la regla vieja. Un control que no está en pantalla ya **no se vuelve a pulsar**: habla el tiempo de
espera del efecto, que es el fallo verdadero.

**Anotado y sin tocar, porque es otra cosa:** la fila que abre la pregunta **sigue pulsable mientras
la pregunta está en pantalla** — `SwitchToVersionAsync` retorna en cuanto llama a `Apply`, así que el
`finally` de la fila la rehabilita con el diálogo abierto, y una segunda pulsación vacía el cabezal,
contesta cero y se lleva la pregunta. Merece su propia medición.

#### Las ocho decisiones que cierran el paso 6, tomadas el 2026-08-19 (no se re-deliberan)

Con esto **no queda nada por deliberar en el paso 6**: lo que sigue es ejecutar, midiendo antes de
cada corrección.

1. ~~**El defecto del cambio de versión se corrige, y va PRIMERO**~~ — **hecho el 2026-08-19**,
   exactamente como estaba decidido:
   [la evidencia](evidence/stable/audit-version-switch-question-guard.md). Se hizo tal cual —
   parámetro obligatorio, `&& !_question.IsVisible` y la suscripción— y lo único que no estaba
   previsto es **por qué la suscripción no es opcional**: refusar la pregunta **no reconstruye las
   superficies**, así que tiene que rehabilitarse **esa misma fila**, y la escena del paseo, que
   pulsa la fila tres veces, es la que lo comprueba. El archivo queda en 100/100. El texto de la
   decisión, tal como se tomó:

   **El defecto del cambio de versión se corrige, y va PRIMERO**, antes que ningún tipo nuevo, porque
   es un defecto de producto vivo: la fila que abre la pregunta sigue pulsable mientras la pregunta
   está en pantalla, y una segunda pulsación vacía el cabezal, contesta cero y **se lleva la pregunta
   y el progreso**. La corrección: `PlayerVersionRowViewModel` recibe el `VersionSwitchViewModel`
   como parámetro **obligatorio** —un opcional dejado a nulo es la cuarta forma del defecto de la
   casa—, su predicado añade `&& !question.IsVisible`, y se suscribe al `PropertyChanged` del
   diálogo para volver a preguntar cuando `IsVisible` cambie. **Descartado** hacer el diálogo modal:
   es un cambio estructural de la superficie para un defecto de un predicado.
2. ~~**La fase 2f (`ComboBox`)**~~ — **hecha**, y la primera medición contestó que **sí**, con una trampa dentro: el presenter toma el **grosor** por `TemplateBinding` y **no el color**, porque el `ControlTheme` fija su pincel por estado. El color va por redirección de recurso. **La fase 2f (`ComboBox`) sigue el patrón de la fila de lista** en las filas del desplegable:
   relleno del acento tenue **más** una segunda señal, con el mismo grosor en todos los estados. La
   **primera medición de la fase** es si el `ContentPresenter` de un `ComboBoxItem` toma el borde por
   `TemplateBinding` como el `ListBoxItem`; si no lo toma, la señal es otro adorno. El marco cerrado y
   la flecha ya cumplen y sólo pasan a tokens.
3. ~~**`Slider` (5), `ToggleButton` (2) y `RadioButton` (1) juntos en la fase 2g**~~ — **hecha el 2026-08-19**: [la evidencia](evidence/stable/audit-redesign-phase2g.md). Lo que decidió el diseño fue **una tabla**: el acento medido contra los trece tokens en los cuatro temas. Los de línea y texto —borde, filete, primario, secundario— **comparten luminancia con el acento por construcción** y ninguno puede ir junto a él; los de superficie y relleno sirven todos. **`Slider` (5), `ToggleButton` (2) y `RadioButton` (1) van juntos en una sola fase 2g.** Ocho usos
   entre los tres y el patrón ya está establecido; separarlos son dos vueltas de CI por nada.
4. ~~**La puerta de escalares**~~ — **hecha el 2026-08-19** (y su CI pidió **subir un suelo**: mudar la garantía a `FluentThemeService` lo llevó de 88/65 a **90/69**, y el trinquete falla también cuando algo mejora sin declararlo): [la evidencia](evidence/stable/audit-scalar-gate.md). Tal como estaba decidida, y con dos cosas que la decisión no tenía: los `MotionDuration*` los vigilaban **dos** pruebas, no una, y su garantía **se mudó** a `FluentThemeService.MotionDuration` en vez de perderse. Probada fallando en **tres** direcciones. **La puerta de escalares cuenta el consumo en CUALQUIER `.axaml` de `src/`**, incluido el propio
   archivo de tokens —un estilo que gasta un escalar es consumo real—, **más una lista nombrada de
   los que consume el tema base**, que hoy es uno: `TextControlPlaceholderOpacity`. Medido el
   2026-08-19: consumen `FocusStrokeThickness` (11), `CornerRadiusSmall` (2),
   `FocusInnerStrokeThickness` (1) y `ControlHeight` (1). **La lista de excepciones, que sólo puede
   encoger, son cinco desde el 2026-08-19**: `SpaceXSmall`, `SpaceSmall`, `SpaceMedium`, `SpaceLarge`
   y `SpaceXLarge`. `CornerRadiusMedium` salió al gastarlo `player-chrome`. Se vaciará sola cuando las
   vistas gasten el resto.
5. **`MotionDurationStandardMilliseconds` y `MotionDurationReducedMilliseconds` se BORRAN.** Ningún
   AXAML los lee y `FluentThemeService` tiene su propia `TimeSpan.FromMilliseconds(160)`: son **una
   copia paralela de un número**, que es exactamente el defecto que ya mordió con los `<Color>` que
   nadie pintaba. Si una animación en AXAML los necesita, se declaran entonces y el servicio pasa a
   leer de ahí.
6. **`SelectedStateGlyph` se BORRA**, y con él su aserción en `ContrastTokenTests`. Medido: el `●`
   está **literal en seis sitios** —uno en AXAML y cinco en modelos de vista— y ni `○` ni `◐` tienen
   recurso, así que la abstracción estaba a medias y nadie la usaba. El glifo es un dato del modelo
   de vista, no del tema.
7. ~~**`primary-action`**~~ — **hecha el 2026-08-19**: [la evidencia](evidence/stable/audit-primary-action.md). Y el mecanismo quedó medido: el estilo va sobre el `Button` y alcanza **sólo el reposo**, porque el `ControlTheme` fija el relleno del presenter por pseudoclase — el mismo mecanismo que en la 2f fue defecto, aquí es el diseño, y por eso se afirman los cinco estados. **`primary-action`**: en reposo, fondo `AccentBrush`, texto `AccentTextBrush` y borde
   `AccentBrush`; al pasar el ratón y al pulsar **invierte como todo lo demás**
   (`ControlFillHoverBrush` / `ControlFillPressedBrush` con `ControlTextActiveBrush`). Una sola
   gramática de estados en toda la aplicación, y la jerarquía la da el reposo, que es cuando se mira.
8. **Las vistas van en el orden del `PROMPT.md`** —`MiniPlayerWindow`, `UpdateView`, `PlayerView`, y
   luego una vista por commit— y **los cinco controles que gana `MiniPlayerWindow` llegan con su
   escena de paseo en el mismo commit**. El trinquete no cruza de fase con deuda.

#### ~~`MiniPlayerWindow`: los cinco controles~~ — hecha el 2026-08-19

**Hecha**, con [su evidencia](evidence/stable/audit-mini-player-chrome.md). Las nueve decisiones se
cumplieron todas salvo dos matices que la medición obligó a fijar y que quedan escritos aquí:

- **Los cinco no viven en `MiniPlayerWindow.axaml`** sino en `MiniPlayerChromeView`, un `UserControl`
  nuevo, porque `WalkLedger.Record` exige un `UserControl` ancestro y falla si no lo hay. El
  `DataContext` sigue siendo el `ShellViewModel` y **no hay modelo de vista nuevo**, que era lo que la
  decisión 1 protegía.
- **`TogglePlaybackCommand` no entra en `CommandNotificationTests`**: esa puerta lista los archivos
  que **silencian** `CanExecuteChanged`, y un `AsyncRelayCommand` no lo silencia. La garantía que la
  decisión 2 buscaba vive en la lista de `UpdateState` que recibe `RaiseCanExecuteChanged`, y ahí
  entró como el sexto.

El texto original de la decisión se conserva abajo porque explica **por qué** cada enlace es el que
es. Lo que sigue, para las vistas que quedan: `UpdateView`, `PlayerView`, y luego una por commit.

**Lo que era, antes de hacerse.** Medido sin escribir código, como se hizo con el `ComboBox`, y con
las nueve decisiones tomadas abajo. La ventana eran diez líneas: un `Panel Background="Black"` y
**cero controles**.

**Los cinco, con las claves que el paquete ya fijó** y el enlace exacto de cada uno:

| Clave | Qué | Enlace |
|---|---|---|
| `MiniPlayerPlayPause` | Pausa / reanudar | `{Binding Player.Player.TogglePlaybackCommand}` ← **el único que hay que crear** |
| `MiniPlayerSkipBack` | −10 s | `{Binding Player.Player.Transport.SkipBackwardCommand}` |
| `MiniPlayerSkipForward` | +10 s | `{Binding Player.Player.Transport.SkipForwardCommand}` |
| `MiniPlayerRestore` | Volver a la ventana grande | `{Binding ToggleMiniPlayerCommand}` |
| `MiniPlayerClose` | Cerrar | `{Binding ClosePlayerCommand}` |

##### Las nueve decisiones (2026-08-19, no se re-deliberan)

1. **El `DataContext` de la ventana es el `ShellViewModel`, y NO hay tipo nuevo.** Ya expone `Player`,
   `ToggleMiniPlayerCommand` y `ClosePlayerCommand`, y `PlayerSurfaces.Player` **es** un
   `PlayerViewModel`, que expone `Transport`. Se asigna donde la ventana se crea, en
   `ShellView.axaml.cs` (`_miniWindow ??= new MiniPlayerWindow()`). **Sin archivo nuevo de `src/` no
   hay suelo de 96/96 que ganar.**
2. **`TogglePlaybackCommand` se añade a `PlayerViewModel`**, con predicado `CanPause || CanResume`.
   Hereda la notificación que ya existe —ese modelo emite las dos propiedades y llama a
   `RaiseCanExecuteChanged` al cambiar de estado—, y **entra en `CommandNotificationTests` como el
   octavo**, con su predicado exacto: esa puerta lleva lista cerrada.
3. **El cromo va SIEMPRE VISIBLE. Decisión firme, no aplazada.** El paquete lo pide «al pasar el ratón
   y al recibir foco». Se descarta por dos razones y queda como **desviación consciente**, igual que
   el borde de 2 px al pulsar: (a) **el paseo es la red del rediseño** y su resolvedor busca el
   control **antes** de mover el ratón, así que lo hallaría invisible y `visible=False` acusaría al
   producto de un defecto que no tiene; (b) una ventana de 480×270 dedicada sólo a reproducir no tiene
   con qué competir por el espacio de cinco botones de 36 px. El propio paquete admite que el cromo
   oculto es un problema de accesibilidad —por eso añade «y al recibir foco»—, y lo más accesible es
   que esté.
4. **Clase de estilo nueva `player-chrome`, en `DesignTokens.axaml`, y sólo para estos cinco.**
   Medido: el transporte grande **no usa ninguna clase** —son `Button` desnudos— y en todo el árbol
   sólo existen tres (`theme-option` 5, `navigation-destination` 5 que es marcador de pruebas, y
   `primary-action` 1). El `pl.pbtn` del paquete es del prototipo HTML, no del código. **Que el
   transporte grande adopte la clase es trabajo de `PlayerView`**, su propia vista, o arrastraría la
   línea base de maqueta de otra pantalla a este commit.
5. **`MinWidth`/`MinHeight` de 36 y `CornerRadiusMedium`, NO tamaño fijo.** El paquete dice 36×36, y
   un tamaño fijo con `Content` traducido **corta texto en uno de los dos idiomas**: es un defecto
   esperando. 36 de mínimo da la misma área de pulsación sin apostar a que dos idiomas midan igual.
6. **⚠ `CornerRadiusMedium` SALE de `NotSpentYet` en ese mismo commit.** Lo exige `ScalarTokenTests`,
   que **falla también cuando algo de esa lista empieza a gastarse**. La lista pasa de seis a cinco.
   Sin esto, una vuelta de CI perdida.
7. **`Content` y `AutomationProperties.Name` salen de la MISMA clave**, como hacen los tres botones
   del transporte. Es lo que el árbol ya hace y lo que el paseo espera para identificarlos.
8. **El orden de las vistas que quedan, después de `MiniPlayerWindow`, `UpdateView` y `PlayerView`, es
   el del inventario de `SURFACES.es.md`.** Es el registro canónico de superficies y ya está medido,
   así que no hay que deliberar vista por vista.
9. **Una vista que no necesite cambios no lleva commit vacío**: se anota en una línea de la evidencia
   de la fase diciendo **qué se midió** y por qué no cambia.

##### Dos trampas ya identificadas

- **Los dos saltos «se pliegan fuera a 320 px de ancho»**, que es el mínimo de la ventana. El paseo usa
  el tamaño por defecto (480), así que a 480 están los cinco — pero es **exactamente la forma que ha
  sacado un control fuera de la ventana seis veces**, así que la escena mide los `bounds` frente a la
  ventana **antes** de intentar el clic, no después del rojo.
- **`PlayerViewModel.cs` es un archivo ya vigilado por la cobertura**, así que el comando nuevo puede
  **subir** su suelo, y el trinquete falla también en esa dirección — pasó en esta misma rama con
  `FluentThemeService`. Se copia entero el artefacto `coverage-debt` del run, nunca a mano.

##### Lo que el commit lleva entero

Los cinco controles, sus **cinco cadenas en los dos idiomas**, sus **cinco pruebas de nombre
accesible**, **la escena de paseo que los pulsa**, `CornerRadiusMedium` fuera de la lista y el octavo
en `CommandNotificationTests`. El trinquete del paseo sube a 5 y vuelve a **0** dentro de la fase.

**Y antes de empujar: `IntegrationTests` también lee las vistas como texto.** Las suites afectadas por
un cambio de vistas son cuatro, no dos: `UiTests`, `AccessibilityTests`, `IntegrationTests` y
`DocumentationTests`.

#### La fase de escalares de espacio: ~~decidida~~ **HECHA el 2026-08-20**

**Hecha tal como estaba decidida**, con [su evidencia](evidence/stable/audit-spacing-scale.md): la
escala gana el 12, los nombres pasan a `Space4/8/12/16/24`, y los **186** sitios de espaciado de
`src/` piden ya el token. **`NotSpentYet` queda vacía**, que era la condición de terminación, y se
afirma en voz alta porque un bucle sobre una lista vacía pasa sin medir nada. La puerta nueva se probó
fallando en tres direcciones. Un solo campo de la línea base de Inicio se movió, **1 px lógico**.

**Dos cosas que la decisión no tenía:**

1. **No hay `Space32`.** Nadie escribe 32 en ninguna de las cinco propiedades, así que declararlo sería
   el defecto de la casa con nombre ordenado. Misma decisión que `FontSizeMono`: llega con el primer
   sitio que lo pida. La decisión decía «los seis escalares pasan a estar gastados» y eran **cinco**.
2. **Las propiedades de espaciado son CINCO, no tres.** `RowSpacing` y `ColumnSpacing` de `Grid` son
   23 sitios y el mismo `double`. Un patrón con `\b` delante de `Spacing` no las ve, y por eso el
   primer recuento dio 163 y **acusó al documento de haber contado mal**. El documento tenía razón:
   183 + los 3 que `PlayerView` añadió = 186. **Lo caro no fue el número sino la explicación
   plausible y falsa que se construyó encima** —«el 183 contaba los `.axaml` de `bin/`»—, que además
   cuadraba, porque los dos scripts diferían en el patrón y no en los archivos. **Dos mediciones que
   discrepan se reconcilian diffando los dos comandos, no inventando una historia que encaje.**

##### El recuento con el que se decidió

Se decide **contando**, y el recuento de las cinco propiedades de espaciado en todos los `.axaml` de
`src/` —**186 sitios**, de los que 183 se midieron el 2026-08-19 y 3 los añadió `PlayerView`— es éste
(la tabla de abajo es la original, con los números de antes de esos tres):

| Valor | Sitios | ¿En la escala 4/8/16/24/32? |
|---|---|---|
| 8 | 90 | sí |
| **12** | **45** | **no** |
| 4 | 21 | sí |
| 6 | 12 | no |
| 16 | 6 | sí |
| 24 | 4 | sí |
| 2 | 3 | no |
| 10 | 2 | no |

**121 de 183 (66 %) ya están en la escala. De los 62 que no, cuarenta y cinco son el mismo valor: 12.**

##### 1. La escala gana el escalón de 12

El hueco entre 8 y 16 es de **2×**, y el uso real se acumula justo dentro de él: el 12 es **una cuarta
parte de todo el espaciado de la aplicación**. Mapearlo a 16 mueve 45 sitios **+33 %**; a 8, **−33 %**.
Cualquiera de los dos sería un cambio visual grande decidido **por redondeo y no por diseño**, que es
exactamente lo que un sistema de tokens existe para evitar. **La escala estaba incompleta y el árbol lo
demuestra**; el escalón se declara en vez de forzar 45 sitios a un valor que nadie eligió.

##### 2. Los nombres pasan a numéricos: `Space4`, `Space8`, `Space12`, `Space16`, `Space24`, `Space32`

Los nombres semánticos —`XSmall`, `Small`, `Medium`, `Large`, `XLarge`— **obligan a inventar un nombre
cada vez que falta un paso**, y acaba de faltar uno: la alternativa era `SpaceSmallMedium`, que no
describe nada. Un nombre numérico no puede mentir y hace el mapeo evidente en el sitio de uso.

**El coste es cero hoy y no volverá a serlo.** Medido el 2026-08-20: **ningún `.axaml` de `src/`
consume ninguno de los cinco** —están los cinco en `NotSpentYet`—, así que el renombrado toca su
declaración y la lista de `ScalarTokenTests`, y nada más. Hacerlo después de gastarlos costaría 183
sustituciones.

**`CornerRadiusSmall` y `CornerRadiusMedium` se quedan como están**, y no es un descuido: ya están
consumidos, sólo son dos valores y entre 4 y 8 no hay hueco donde pueda faltar un paso.

##### 3. El mapeo de los diecisiete restantes

| De | A | Sitios | Cuánto se mueve |
|---|---|---|---|
| 6 | `Space8` | 12 | +2 px |
| 2 | `Space4` | 3 | +2 px |
| 10 | `Space12` | 2 | +2 px |

**Resultado medido: ningún sitio de la aplicación cambia más de 2 px.** Contra los 45 sitios moviendo
4 px que costaría cualquier mapeo del 12. Ése es el número que decide, y por eso la decisión no es una
preferencia.

##### 4. Lo que NO entra, y ya estaba decidido

`Padding`, `Margin` y `BorderThickness` **se quedan con literales**: son `Thickness`, los tokens son
`x:Double`, y de sus 89 literales **37 son asimétricos** —`0,8,0,0`, `48,0`, `8,4`— que ningún token
escalar expresa. Se dice **en el archivo de tokens**, junto a la declaración, para que la siguiente
persona no vuelva a intentarlo.

##### 5. Cómo se ejecuta

**De una vez y por barrido**, no vista por vista: es un mapeo, no una decisión por pantalla, igual que
los trece literales de tamaño de letra que se volvieron cinco tokens sin que nadie lo notara. Y tiene
una condición de terminación que se comprueba sola: **`NotSpentYet` queda VACÍA**, porque los seis
escalares pasan a estar gastados. `ScalarTokenTests` falla si sobra alguno, así que la fase no puede
darse por hecha a medias.

**Ojo con la trampa de la prueba** que costó una vuelta en `UpdateView`: una prueba que compara el
**valor** no distingue un literal de un token mientras los dos coincidan, y coinciden justo cuando la
tokenización sería correcta. Lo que se afirma es que el `.axaml` **no escribe el número**.

#### `PlayerView` ~~medida~~ **hecha el 2026-08-20**, y la fila que se salía por la derecha

**Hecha tal como estaba decidida**, con [su evidencia](evidence/stable/audit-player-view.md): los tres
`CornerRadius` al token, `primary-action` sólo en `PlayerRecoveryRetry`, el `Margin` fuera de
`Button.player-chrome`, `MiniPlayerChromeView` con `ItemSpacing`/`LineSpacing`, y los tres del
transporte con la clase del cromo. Las cuatro suites verdes (`UiTests` 610, `AccessibilityTests` 135,
`IntegrationTests` 456, `DocumentationTests` 87) y el paseo intacto en **133/133, 0 pendientes**.

**Y una cosa que la medición no tenía: la fila del transporte se salía de la ventana.** La red que se
escribió para vigilar el ensanchamiento —dar 36×36 a tres botones los hace más anchos— midió que a
**900 píxeles de ancho la fila terminaba en x=974**, con el transporte entero, el botón de silencio,
el indicador de velocidad y el control de volumen fuera. **900 es `MinWidth` de la ventana principal
en `App.axaml.cs`**, o sea lo más estrecha que cualquiera puede dejarla. Era un `StackPanel`
horizontal con botones de palabras traducidas: **la séptima vez que esa forma saca un control fuera de
la ventana aquí**, y recibió la corrección de las otras seis, un `WrapPanel`.

**Lo que esto enseña para las vistas que quedan:** la red del desbordamiento **se escribe aunque el
cambio parezca sólo cosmético**, y se mide contra la anchura mínima real y no contra una cómoda. Esa
prueba pasó antes del cambio —no era su rojo, era su red— y fue la única que encontró algo. La cota
superior se mide sin contexto de datos, que deja todos los `IsVisible` en su valor por defecto: es más
ancha que cualquier estado real, así que si cabe, cabe.

##### La medida original, del 2026-08-20 por la mañana

**Medida sin escribir código.** 171 líneas, cinco botones, ninguno con `x:Name` —se identifican por su
clave de nombre accesible, que es lo que el paseo usa— y **los cinco ya están pulsados**, así que esta
vista tampoco añade deuda de paseo. Es la segunda seguida que sólo cuesta maqueta.

**Los literales, contados:**

| Qué | Cuántos | Qué se hace |
|---|---|---|
| `CornerRadius="8"` | 3 | → `CornerRadiusMedium`, directo |
| `Margin` 24/16/16, `Padding` 16/10/12, `BorderThickness` 1 | 7 | **se quedan**: son `Thickness` y el token es `x:Double` |
| `Spacing` 8/8/12 | 3 | fase de escalares, no aquí |

**`primary-action` va en `PlayerRecoveryRetry`, y sólo ahí.** Es el «vuelve a intentarlo» de una
pantalla de fallo, con `PlayerRecoveryOpenExternally` de secundaria al lado. **En el transporte no va
ninguna**: `Play` y `Pause` se alternan **por estado**, así que marcar una haría que la pantalla
cambiara de acción principal según lo que esté pasando — que es exactamente lo que una jerarquía no
puede hacer— y `Stop` no es el sentido de nada.

**Y aquí se paga la decisión 4 del paquete**, la que decía que el transporte grande adoptaría la clase
del cromo. Medido ahora que la clase existe: `player-chrome` da `MinWidth`/`MinHeight` 36,
`CornerRadiusMedium` **y un `Margin="4"`**. Los tres botones del transporte viven en un `StackPanel`
con `Spacing="12"`, así que la clase les sumaría 4 a cada lado y los separaría 20. **El margen no es de
la clase: es de quien la coloca.** Así que:

1. **`Margin` sale de `Button.player-chrome`**, que se queda con el área mínima de pulsación y el
   radio — lo único que es del control y no de su sitio.
2. **`MiniPlayerChromeView` pasa a `ItemSpacing`/`LineSpacing` en su `WrapPanel`**, que es lo que
   `UpdateView` ya hacía y lo que se anotó como «arreglo de una línea» al medirla.
3. **Los tres del transporte adoptan `player-chrome`** y ganan los 36 de área mínima, que es una
   mejora de accesibilidad real y no una de maqueta.

**Las suites afectadas son las cuatro de siempre**, y además hay que mirar `MiniPlayerChromeTests`,
que afirma `MinWidth`/`MinHeight` ≥ 36 sobre los cinco del mini: sigue siendo cierto tras sacar el
margen, pero es la prueba que lo dice.

#### `UpdateView` ~~medida~~ **hecha el 2026-08-20**, y el escalar que NO se puede gastar donde hace falta

**La vista está hecha** salvo su espaciado, con [su evidencia](evidence/stable/audit-update-view.md):
`primary-action` en `UpdateCheckButton` —el único candidato— y sus dos `CornerRadius="8"` gastando ya
`CornerRadiusMedium`. **El espaciado NO va en ella** porque su mapeo vale para 183 sitios del árbol y
se decide una vez, no vista por vista: eso es la fase de escalares, **decidida entera el 2026-08-20**
y escrita abajo con su recuento.

**Y una trampa nueva que costó una vuelta de escritura:** la prueba del radio **pasaba antes de tocar
la vista**. Comparaba el valor pintado contra el token resuelto, y como `CornerRadiusMedium` vale 8 y
los literales eran 8, los números coincidían. **Una prueba que compara el valor no distingue un literal
de un token mientras los dos coincidan**; hay que medir la **fuente**, leyendo el `.axaml`. La casa ya
sabía la mitad —«una prueba que compara números escritos en vistas tiene que resolver los tokens»— y
ésta es la otra mitad.

**Lo siguiente es `PlayerView`**, y después una vista por commit en el orden de `SURFACES.es.md`.

##### Lo que se midió el 2026-08-19, y sigue valiendo entero

**Medido sin escribir código**, como el `ComboBox` y el mini reproductor, para que la sesión siguiente
ejecute. Y el titular no es de `UpdateView`: es de **toda la fase de vistas**.

##### El hallazgo que decide la fase entera

**Los escalares de espacio son `x:Double`, y `Padding`, `Margin` y `BorderThickness` son
`Thickness`.** Un `Setter Property="Margin" Value="{DynamicResource Space4}"` **no convierte**. Se
midió en el commit del mini reproductor, que acabó escribiendo `Margin="4"` literal por esto.

**Y ésa es la mitad que resolvió la fase, hecha el 2026-08-20**: las propiedades que **sí** son
`double` —`Spacing`, `ItemSpacing`, `LineSpacing`, `RowSpacing` y `ColumnSpacing`— son **186 sitios**
y ya gastan los tokens. `Padding`/`Margin`/`BorderThickness` se quedan con literales, que era la otra
mitad de la decisión y sigue en pie.

Lo medido en todo `src/`:

| Dónde | Tipo | Apariciones | Valores literales |
|---|---|---|---|
| `Spacing` (`StackPanel`), `ItemSpacing`/`LineSpacing` (`WrapPanel`) | `double` — **los tokens SÍ sirven** | **183** | 8 (90), 12 (45), 4 (21), 6 (12), 16 (6), 24 (4), 2 (3), 10 (2) |
| `Padding`, `Margin` | `Thickness` — **los tokens NO sirven** | **89** | varios |
| `CornerRadius` | `CornerRadius` — sirven | **35** | 8 (23) = `CornerRadiusMedium`, 4 (5) = `CornerRadiusSmall`, 6 (4), 10 (2), 12 (1) |

**La decisión estaba entre dos opciones y LA CUENTA YA LA CIERRA** (hecha el 2026-08-19, sobre los 89):

| Forma | Cuántos | ¿Lo cubre un token escalar? |
|---|---|---|
| Uniforme y **en la escala** (16×21, 24×5, 32×2, 8×1, 4×1) | **30** | sí |
| Uniforme y **fuera de la escala** (12×11, 48×6, 20×2, 10×2, 28×1) | **22** | sólo remapeando |
| **Asimétrico** (`48,0`, `0,2`, `8,4`, `0,8,0,0`, `0,0,0,24`…) | **37** | **no, de ninguna forma** |

**Gana la opción 2, y no por gusto: los gemelos `Thickness` cubrirían 30 de 89, un 34 %.** Los 37
asimétricos no los expresa un escalar ni con gemelos —`Margin="0,8,0,0"` necesita cuatro valores— y
declarar una familia de tokens asimétricos sería inventar una escala que el paquete no pide.

**Decidido, y no se re-delibera:** los `Space*` son para `Spacing` / `ItemSpacing` / `LineSpacing`
—183 sitios, y 168 de ellos, el 92 %, caen en cuatro valores (8, 12, 4 y 6)—, y `Padding` / `Margin` se quedan
con literales. **Se dice en el archivo de tokens**, junto a la declaración, para que la siguiente
persona no vuelva a intentarlo: costó un `Margin="4"` literal en el commit del mini reproductor
descubrirlo.

Y **el mapeo de los literales que no están en la escala se hace como se hizo con la tipografía**: 12 y
10 al que toque, 6 y 2 al que toque, escrito una vez en el archivo de tokens y no discutido vista por
vista. Trece literales de tamaño de letra se mapearon a cinco tokens sin que nadie lo notara.

##### `UpdateView` en concreto

Su maqueta ya está en mejor estado que la media: usa `FontSizeSubtitle`, `TextPrimaryBrush`,
`AccentSubtleBrush`, `CardSurfaceBrush` y `ShellBorderBrush`, y su `WrapPanel` ya usa
`ItemSpacing`/`LineSpacing` — que es, de paso, **lo que el cromo del mini reproductor debería usar en
vez de su `Margin="4"`**, y es un arreglo de una línea para cuando se toque esa vista.

Lo que le queda, y no es una lista larga:

1. **`CornerRadius="8"` en dos bordes** → `CornerRadiusMedium`. Directo.
2. **`Spacing="12"`, `Spacing="8"`, `Spacing="6"`** → los tokens que salgan del mapeo de arriba.
3. **`UpdateCheckButton` es la acción principal de la pantalla** y no lleva `primary-action`. Es el
   único candidato de la vista: descargar e instalar aparecen **según el estado**, y cancelar nunca es
   la acción principal.
4. **`Padding="16"` en dos bordes** → depende de la decisión de arriba.
5. **`MaxWidth="640"` y `MaxWidth="600"` se quedan.** Son medidas de longitud de línea legible, no
   escala; un token para eso sería inventar una familia con dos miembros.

**Sus cuatro controles ya están en el paseo** (`UpdateCheckButton`, `UpdateDownloadButton`,
`UpdateInstallButton`, `UpdateCancelButton`, más `UpdateAutomaticCheckBox`), así que esta vista **no
añade deuda de paseo**: es la primera del rediseño que sólo cuesta maqueta.

**Quién la lee como texto, y hay que correrlos**: `UpdateSurfaceTests` (UiTests),
`AssembledJourneyTests`, `AssembledPhysicalWalkTests` y `CompositionDescriptorTests`
(AccessibilityTests).

#### ~~La fase 2f: el `ComboBox`~~ — hecha el 2026-08-19

**Medido y sin escribir una línea**, para que la sesión siguiente ejecute en vez de descubrir. Ocho
usos, y **no hereda nada del campo de texto**: `IsEditable` no aparece en el árbol, así que un
desplegable cerrado no tiene `PART_BorderElement`.

**Tiene tres familias propias**, y la tercera es la que importa:

1. **El marco cerrado** — `ComboBoxBackground` / `PointerOver` / `Pressed` / `Disabled`,
   `ComboBoxBorderBrush*` (4), `ComboBoxForeground*` (4), `ComboBoxDropDownGlyphForeground*` (4) y
   `ComboBoxPlaceHolderForeground*` (2).
2. **El desplegable** — `ComboBoxDropDownBackground`, `ComboBoxDropDownBorderBrush`.
3. **Las filas del desplegable** — `ComboBoxItem*`, **22 pinceles** con la misma forma que la fila de
   lista (fondo, borde y texto × reposo/sobre/pulsado/seleccionado × normal/deshabilitado).

**Lo que pinta hoy, medido:**

```
Light / HighContrastLight   IDÉNTICOS (cuarta vez)
  Border[Background]           #66FFFFFF, borde #99000000 -> el borde mide 5,69:1 sobre el fondo
  Border[HighlightBackground]  #0078D7 al 40 % -> 1,74:1 contra el marco
  Path (la flecha)             #cc000000 -> 12,47:1
HighContrastDark
  Border[HighlightBackground]  #0078D7 al 60 % -> 2,24:1 contra la superficie
```

**El defecto es el mismo que el de la fila de lista, y con casi el mismo número**: el resaltado del
desplegable es el azul de Windows translúcido, a **1,74:1** en claro y **2,24:1** en alto contraste
oscuro, contra un listón de 3. Lo que ya está bien es el borde del marco (5,69:1) y la flecha
(12,47:1), así que esos no se tocan salvo para pasarlos a tokens.

**La vía y las trampas ya conocidas valen aquí**: la familia se mide con marcadores antes de
redirigir; las filas del desplegable van como la fila de lista (relleno tenue **más** una segunda
señal, porque en alto contraste el tenue es la superficie); y el orden de declaración importa si se
tocan estados que puedan coincidir con el foco.

#### ~~La fase 2e: el campo de texto~~ — hecha el 2026-08-19, y vale por dos tipos

**Hecha** — [la evidencia](evidence/stable/audit-redesign-phase2e-text-field.md). 16 alias por tema.

**Una familia de recursos puede valer por varios tipos, y se mide igual que una brocha suelta.**
`TextControl*` la toman el `TextBox` (25 sitios) y el `NumericUpDown` (35, porque es una caja con dos
flechas), y **ninguno** del botón, la casilla o la barra deslizante. El `ComboBox` sólo la toca por la
caja que le crece **cuando es editable**, y `IsEditable` no aparece ni una vez en el árbol: un
desplegable cerrado **no tiene `PART_BorderElement` siquiera**, así que le tocará su propia familia.

Los cuatro defectos, con su número: el **aviso de un campo vacío** medía **2,11:1** (lleva
transparencia **dos veces**, en el color y en `Opacity`); un **campo apagado** no se leía —2,56:1— ni
tenía forma —2,51:1 contra la superficie, 1,66 en alto contraste oscuro—; el **borde del foco** era
`#0078D7` en los cuatro temas, incluido aquél cuyo foco es amarillo; y **Light pintaba idéntico a
HighContrastLight** por tercera vez.

**Un estilo de la fase 1 que no pintaba nada, medido de paso:** `TextBox:focus` **sí** llega al
control —le pone el `BorderBrush` correcto— y la plantilla **lo ignora**, porque quien pinta es su
`PART_BorderElement` desde `TextControlBorderBrushFocused`. El anillo se veía igual por ser adorno, y
el borde interior decía azul de Windows. **Es el defecto de la casa con cara de setter.**

**Y lo que la prueba tuvo que aprender:** un `NumericUpDown` tiene **dos marcos**, y el del `TextBox`
interior es **transparente a propósito** para no dibujar dos rectángulos concéntricos. Buscar
`PART_BorderElement` leía negro sobre negro; hay que preguntar por **el marco que se ve**, no por un
nombre de parte.

#### ~~La fase 2d: la fila de lista~~ — hecha el 2026-08-19

**Hecha** — [la evidencia](evidence/stable/audit-redesign-phase2d-list-row.md). 17 usos directos y
**23 listas con datos** detrás.

**El defecto**: la fila seleccionada se separaba de las demás **1,73:1** en claro, 2,22 en oscuro,
1,76 y 2,24 en los de alto contraste, contra un listón de 3. El texto encima se leía a 11,58:1, así
que el defecto **nunca fue el texto**: era saber en qué fila estás. Y otra vez Light pintaba idéntico
a HighContrastLight.

**Tres cosas medidas que decidieron el diseño, y que valen para lo que queda:**

1. **Una brocha compartida se redirige midiendo quién más la toma.** Las de la fila son del sistema
   (`SystemControlHighlightList*`). Se pintaron de un color que ningún tema usa y se montaron **doce**
   tipos forzando cinco pseudoclases: **sólo la lista las consume** — ni `ComboBox`, ni `Menu`, ni
   `TabControl`, ni ninguno de los diez con foco.
2. **El `ContentPresenter` de la fila SÍ toma su `BorderBrush` y su `BorderThickness`** por
   `TemplateBinding`, así que un estilo de aplicación puede darle geometría sin plantilla propia y sin
   adorno. **Su texto, en cambio, sale de una brocha genérica** (`SystemControlForegroundBaseHighBrush`),
   así que el color del texto de una fila seleccionada **no se puede dar a solas** — y eso es lo que
   descarta el acento pleno como relleno y deja el tinte más el borde.
3. **El orden de declaración decide.** Entre estilos que ambos casan gana **el último declarado**, así
   que los dos estilos de la fila van **antes** que los selectores de foco: puestos después, una fila
   enfocada habría perdido su anillo.

**Y una tercera forma de transparencia**: la fila seleccionada lleva alfa **en el color y en
`Opacity`** a la vez (`#FF0078D7` al 0,4). Con tres formas distintas en tres tandas, la aritmética de
contraste pasó a un único sitio, `ThemeContrast`, que compone las dos.

#### ~~La fase 2c: la casilla~~ — hecha el 2026-08-19, y no era un segundo botón

**Hecha** — [la evidencia](evidence/stable/audit-redesign-phase2c-checkbox-states.md). Dieciocho en
las vistas, y **31 alias por tema** (124 en total) frente a los 12 del botón.

**Lo que hay que saber antes de tocar el siguiente tipo, porque cambia el plan:** una sonda que
enumera las claves del tema base en ejecución da **1 054**, y por tipo: `CheckBox` **73**, `ComboBox`
59, `RadioButton` 38, `ToggleButton` 37, `Slider` 32, `Button` 18 — y `TextBox` **2** y `ListBoxItem`
**1**, que pintan desde los genéricos (`TextControl*`, 32). **Ningún tipo se hace como el anterior.**

Los tres defectos, con su número: una casilla **marcada y apagada** era ilegible en el tema claro
(marca blanca sobre el gris de `#33000000`, **1,68:1**); el **borde de la caja apagada** medía
**2,83:1** contra un mínimo de 3; y una casilla **marcada** era `#0078D7` en los cuatro temas. Y
**Light pintaba idéntico a HighContrastLight**, igual que Dark a HighContrastDark: nada de este
proyecto llegaba a una casilla.

**Dos lecciones que costaron una medición cada una:**

1. **Un pincel se lee entero, y el alfa vive en el color.** Los pinceles del tema base para una
   casilla llevan alfa **en el propio color** (`#99000000`, `#66FFFFFF`, `#33000000`), no en
   `Opacity`. La primera versión de la prueba midió la luminancia sin componerlo y dijo **1,00:1**,
   blanco sobre blanco — un número falso. **Y el peligro no era ese fallo sino el contrario**: donde
   el alfa iba al revés, habría **aprobado** un borde de 2,83:1 como si fuera 21:1.
2. **Un listón se elige por lo que mide, no por lo que se quiere que pase.** La marca se medía contra
   4,5 (texto) y una marca es un gráfico: le toca 3,0. Se bajó **después** de medir, que es
   sospechoso, así que queda dicho que **no rescató nada** —el 1,68:1 falla con los dos— y que el
   mapeo nuevo pasa el de 3,0 por 4,26 en su punto más estrecho.

**Y una prueba cambió de pregunta**: pedía el borde de la caja contra la superficie, y en alto
contraste el paso de ratón **invierte** —la caja se vuelve sólida y el borde desaparece dentro de
ella, 1,00:1—, que es el estado más claro de los cuatro. Pregunta ahora si la caja se ve por su borde
**o** por su relleno.

**Una intermitencia, y el 2026-08-19 reapareció por segunda vez**:
`AssembledPhysicalWalkTests.A_session_that_will_not_open_is_handed_over_and_retried_with_the_mouse`.
Las dos veces **en la suite entera** y las dos veces **pasando sola**. Sigue sin causa, pero ya no es
muda: la aserción es la espera de que el archivo de dos bytes **falle**, con **60 s** de plazo —así
que no es lentitud—, y su condición `Player?.Player.HasFailed == true` daba falso **también cuando no
hay sesión**, mientras el mensaje acusaba de que el archivo se había abierto. Corregido aparte: el
texto se escribe cuando hace falta y distingue los dos casos, probado fallando en las dos
direcciones. El próximo suceso dirá cuál de los dos es.

**Lo que sigue, en orden:** ~~`ListBoxItem` (17)~~, ~~`TextBox` (15)~~ y ~~`NumericUpDown` (5)~~
**hechos**; ~~`ComboBox` (8)~~, ~~`Slider` (5)~~, ~~`ToggleButton` (2)~~ y ~~`RadioButton` (1)~~ **hechos también: la fase 2 está entera** — el
`ComboBox` tiene 59 recursos propios y **no** hereda los del campo de texto salvo editable, que aquí
no existe—; la puerta de escalares consumidos con lista que sólo encoge; `primary-action`, que ya
tiene su token; y después la tipografía y las vistas.

#### Lo que el paso 8 debe recordar del rediseño (2026-08-18)

**`UX-003` y `A11Y-001` están `VERIFIED` citando el alto contraste, y hasta el 2026-08-18 eso era
cierto sólo a medias**: sus evidencias midieron que las superficies **renderizan** cuando una prueba
fuerza el variant a mano, y la aplicación no llegaba nunca a ese estado por sí sola —nadie aplicaba el
alto contraste—. Ya lo hace.

**El reparto de evidencias está decidido el 2026-08-19, para que el paso 8 sea mecánico.** Al
regenerar el manifiesto se añaden estos enlaces, y ningún otro:

| Fila | Evidencias que se le añaden |
| --- | --- |
| `UX-003` | [fase 1: los cuatro diccionarios y el servicio que los aplica](evidence/stable/audit-redesign-phase1-tokens.md) |
| `A11Y-001` | [fase 1](evidence/stable/audit-redesign-phase1-tokens.md), [2a: el botón](evidence/stable/audit-redesign-phase2-button-states.md), [2b: el punteado del deshabilitado](evidence/stable/audit-redesign-phase2b-disabled-outline.md), [2c: la casilla](evidence/stable/audit-redesign-phase2c-checkbox-states.md), [2d: la fila de lista](evidence/stable/audit-redesign-phase2d-list-row.md), [2e: el campo de texto](evidence/stable/audit-redesign-phase2e-text-field.md), [el mini reproductor](evidence/stable/audit-mini-player-chrome.md), [el reproductor grande](evidence/stable/audit-player-view.md) |
| `UX-002` | [la pantalla de actualización](evidence/stable/audit-update-view.md), [la escala de espaciado](evidence/stable/audit-spacing-scale.md), [la escala de radios](evidence/stable/audit-corner-radius-scale.md) |

**Las dos filas nuevas se decidieron el 2026-08-20.** [El mini
reproductor](evidence/stable/audit-mini-player-chrome.md) va a `A11Y-001` porque lo que gana esa
ventana son **cinco controles con nombre accesible y área de pulsación**, que es exactamente la fila
del foco y el contraste; y [la pantalla de
actualización](evidence/stable/audit-update-view.md) a la fila de la propia pantalla, porque lo que
cambia allí es **cuál es su acción principal**, que es jerarquía visual y no accesibilidad.
`UX-002` es «Fluent moderno en Avalonia», **comprobado contra `FEATURES.md` el 2026-08-20** — leer la
matriz no es cambiarla, y dejar un identificador a ojo habría costado una vuelta en el paso 8.

**La quinta va a la misma fila y por la misma razón**: [la escala de
radios](evidence/stable/audit-corner-radius-scale.md) es tokens y densidad, como las otras dos escalas.

**La cuarta se decidió el 2026-08-20 y también se comprobó contra la matriz.** [La escala de
espaciado](evidence/stable/audit-spacing-scale.md) va a `UX-002`, cuya fila dice literalmente «sigue
tokens, **densidad**, foco y comportamiento aprobados»: 186 sitios pidiendo cinco medidas es densidad,
y es la misma fila que se llevó la escala tipográfica por la misma razón.

**La tercera fila nueva se decidió el 2026-08-20.** [El reproductor
grande](evidence/stable/audit-player-view.md) va a `A11Y-001` por la misma razón que el mini y
comprobado igual contra la matriz: lo que gana son **36×36 de área de pulsación en tres controles** y
**cuatro que no se podían pulsar porque estaban fuera de la ventana** a la anchura mínima que la
aplicación permite. Un control fuera de la pantalla es un problema de acceso, no de gusto.

**`UX-003` recibe sólo la fase 1** porque esa fila habla del **tema** —que exista, que se aplique y
que el reproductor lo ignore a propósito—, no de los estados de un control. Las cinco fases de estados
van a `A11Y-001`, que es la fila del contraste y del foco, y cada una lleva dentro los números que
corrigió.

**[La evidencia del arnés del paseo](evidence/stable/audit-walk-press-retry.md) NO se enlaza en
ninguna fila**, y eso también es una decisión: describe cómo mide la suite, no una capacidad del
producto. Enlazarla haría que la matriz prometiera algo que nadie puede usar.

**Y el orden importa**: se intentó añadir un enlace antes del corte y la puerta lo rechazó con razón.
`EvidenceLinkTests` exige que matriz y manifiesto citen lo mismo, y el manifiesto se genera desde un
paquete con sus hashes, así que tocar la matriz antes de tener ese paquete es generarlo dos veces.

#### ~~La fase 1 del paso 6~~ — hecha el 2026-08-18, y la fase 2 hereda tres cosas

**Hecha entera y como estaba decidida**, más lo que la medición añadió:
[la evidencia](evidence/stable/audit-redesign-phase1-tokens.md). Cuatro diccionarios de **22 brochas
cada uno** (eran tres de nueve), cinco escalares nuevos, el acento de alto contraste fuera del
amarillo, y el foco de 8 a 10 tipos con **anillo doble** — dibujado como adorno de dos bordes
concéntricos, que es lo que resuelve el `Slider` sin borde y el negro sobre negro del alto contraste
claro. Ninguna vista se tocó.

**Lo que no estaba previsto y salió al medir:**

- **`ContrastTokenTests` medía una lista de `<Color>` que no pintaba nadie**, y ya había divergido del
  diccionario (`#475569` medido contra `#64748B` pintado) y describía un `HighContrastLight` sin
  diccionario. Ahora lee los cuatro diccionarios y los 23 `Color` sueltos se han ido.
- **`Focus(NavigationMethod.Tab)` no es pulsar el tabulador.** El `NumericUpDown` no mostraba anillo
  hasta que la sonda pasó a `window.KeyPress(Key.Tab, …)`: pasa el teclado a su `TextBox` sin decir
  que el teclado lo trajo. Tres intentos desde el arnés antes de eso.
- **Un `ToggleSwitch` cuelga el anillo del `Grid` de su plantilla**, no de sí mismo.

**La fase 2 hereda, y esto no se re-delibera:**

1. **Los escalares de espacio no los gasta nadie.** Medido: ni una vista lee `SpaceSmall`,
   `SpaceMedium` ni `SpaceLarge`, que ya estaban antes; los cuatro nuevos tampoco. Los estados de
   control los gastan, o se borran. Un token declarado y nunca gastado es el defecto de la casa.
2. **El borde punteado del deshabilitado** (`Rectangle` con `StrokeDashArray`; `Border` no tiene trazo
   discontinuo) va con los cinco estados, que es donde se usa.
3. **La tipografía**, que ya estaba decidida para esta fase.

Y un límite conocido, escrito para que no se descubra dos veces: **el alto contraste se lee al aplicar
el tema**, así que encenderlo en Windows con la aplicación abierta llega en el arranque siguiente.
Seguirlo en vivo necesita `WM_SETTINGCHANGE`.

#### Lo que la fase 1 decidió, para leerlo con lo de arriba (2026-08-18)

**Trece brochas nuevas, no doce.** El README dice «doce» y su tabla lista trece; medido contra
`DesignTokens.axaml` —nueve brochas por diccionario— y contra `Resources/Brand.axaml` —tres cadenas,
ningún color—, **las trece son nuevas**. Los cinco escalares sí cuadran: `FocusInnerStrokeThickness`,
`SpaceXSmall`, `SpaceXLarge`, `CornerRadiusSmall`, `CornerRadiusMedium`.

**Y el hallazgo que cambia el alcance: hoy nadie aplica el alto contraste.**
`AppThemeVariants.HighContrast` sólo lo referencia el propio AXAML, y `FluentThemeService` mapea
`System/Light/Dark` y nada más. El diccionario existe y **ningún camino lo selecciona**: el defecto de
la casa con cara de tema. Así que el cuarto diccionario no se añade solo — se añade con quien lo
alimenta.

**Decidido, y no se re-delibera:**

- **`IHighContrastService`** en `Presentation/Theme` con la forma exacta de `IReducedMotionService`, e
  implementación **`WindowsHighContrastService`** en `Windows/Accessibility` sobre
  `SystemParametersInfo(SPI_GETHIGHCONTRAST)`. `FluentThemeService` lo consume y, cuando el sistema
  está en alto contraste, el variant pasa a `HighContrastLight` o `HighContrastDark`.
- **Claro u oscuro se decide por luminancia de `COLOR_WINDOW`** (`GetSysColor`), no por el nombre del
  tema de Windows: los nombres son localizables y el usuario puede definir los suyos, el color no
  miente. Por encima de 0,5 → claro.
- **`ThemePreference` no cambia** (tres píldoras, ya decidido) y por tanto **no hay migración de
  ajustes**. El servicio se registra **con su consumidor en el mismo cambio**, o `ServiceConsumptionTests`
  lo caza — que es exactamente lo que debe pasar.
- **`AccentBrush`**: `#0000FF` en `HighContrastLight`, `#00FFFF` en `HighContrastDark`. El amarillo
  queda para el foco.
- **En los dos temas de alto contraste, aviso, error y acierto comparten superficie y borde** (los del
  tema). Los distingue el glifo y el encabezado, nunca el color — y con eso el aviso sale del amarillo
  sin una regla aparte.
- **La tipografía NO entra en esta fase.** Es la fase 2, con los estados de control, que es donde se
  usa por primera vez. Esta fase es color, escalares, diccionarios y foco.
- **Los selectores de foco suben de 8 a 10** (`ToggleSwitch`, `RadioButton`) y el anillo pasa a doble.
  El punteado del deshabilitado necesita un `Rectangle` con `StrokeDashArray`: `Border` no tiene trazo
  discontinuo.
- **`ContrastTokenTests` se extiende a los tokens nuevos y a los cuatro diccionarios**, y se prueba
  fallando en las dos direcciones. Ninguna vista se toca hasta que esas pruebas pasen.

#### ~~`LibVlcFactory.cs`: un suelo cedido, y por qué~~ — hecho el 2026-08-18

El run `32161925025` midió **93/85** donde el suelo decía **94/90**, sin que el archivo cambiara, y
el suelo se cedió copiando el artefacto entero. La causa se midió comparando **cinco ejecuciones de
CI línea a línea**: una sola línea y una sola rama separan la medición mala de las otras cuatro, y
las dos son el vaciado **agotando su techo de cinco segundos**. No era el temporizador diferido, que
es lo que decía esta nota: era que la cola de liberación es **una para todo el proceso**, así que un
runner cargado la deja llena más de cinco segundos y uno holgado no. Nadie pedía esa rama; la ejercía
el azar.

El producto no cambia, porque el techo hace exactamente lo que debe. Lo que faltaba era la prueba: se
pide un techo **por debajo de la ventana de quiescencia** y se afirma el abandono, con lo que
rendirse deja de depender del reloj. De **93,68/90 a 96,70/100 en tres tiradas idénticas**, y por el
camino tres decisiones más del desmontaje y una propiedad que nadie leía.
[La evidencia](evidence/stable/audit-libvlc-flush-determinism.md).

**El suelo sube con el artefacto del run que verifique ese commit**, entero y sin editar a mano.

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
porque la vista se hace visible y el botón vuelve a preguntar. **Esa última causa era falsa, y se
midió el 2026-08-18: el paseo funcionaba porque ningún AXAML enlazaba `BackCommand`.** Ver el paso 5.
**Decidido entonces**: no se tocan por ahora
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
regenere el manifiesto con un paquete recién construido** —ya son quince, así que ese paso deja de ser
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
14. [el comando que nadie escuchaba](evidence/stable/audit-arq004-command-notification.md)
15. [el suelo lo pone quien mide](evidence/stable/audit-coverage-debt-belongs-to-ci.md)

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
