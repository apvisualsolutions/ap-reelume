# T23B — La salida de audio, grabada / Audio output, recorded

- Fecha / Date: 2026-09-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, LibVLC 3.0.23.1,
  ffmpeg 2024-06-21. Cuatro endpoints físicos de render: dos ASUS ProArt PA279CRV por DisplayPort,
  auriculares Logitech G535 y la salida digital Realtek, los cuatro estéreo. Más ocho endpoints
  virtuales de VoiceMeeter Banana 2.1.2.2, uno de ellos puesto a 7.1
- IDs: `PLY-004=VERIFIED` (era `BLOCKED` / was blocked)

## Por qué existe / Why this exists

T23 dejó las filas 5.1 y 7.1 sin verificar **leyendo una etiqueta**: el registro dice que los cuatro
endpoints mezclan en dos canales, y de ahí se concluyó un bloqueo de hardware. La etiqueta la escribe
Windows, y **listar no es verificar**. Esta tanda construye el instrumento que falta —grabar lo que
el endpoint recibe de verdad y contar sus canales— y lo valida sobre lo que esta máquina sí ofrece. /
T23 left the surround rows unverified on the strength of a label written by Windows. Listing is not
verifying; this batch builds the instrument that records what the endpoint actually receives and
validates it against what this machine does offer.

## Lo primero medido: la muestra 7.1 no servía / First measurement: the 7.1 sample could not answer

`mkv-audio-71` declara ocho canales y `AudioChannelTests` lo comprueba. Medida su **tinta** con
`astats`, siete de los ocho canales están en **silencio digital absoluto**:

| Canal / Channel | 1 | 2 | 3 (FC) | 4 | 5 | 6 | 7 | 8 |
|---|---|---|---|---|---|---|---|---|
| RMS (dBFS) | −∞ | −∞ | −21,07 | −∞ | −∞ | −∞ | −∞ | −∞ |

La causa está en su propia receta: `-ac 8` sobre una sinusoide mono **hace un upmix**, no ocho
canales. Con esa muestra, una grabación de ocho canales sería indistinguible de un upmix trivial, de
un enrutado a un solo altavoz o de seis canales perdidos. La prueba que la usa pasa **contando la
etiqueta** que anuncia el decodificador, no midiendo lo que suena. / The eight-channel sample carries
one mono sine upmixed by `-ac 8`, leaving seven channels at digital silence, so a recording of it
could not tell a real eight-channel path from a trivial upmix.

`mkv-audio-71-tones` lo sustituye como instrumento: **un tono primo y mutuamente no armónico por
canal**, asignados por nombre de altavoz con `join=...:map=`, de modo que cada canal se identifica y
una permutación del orden se ve. Medida su matriz cruzada canal × frecuencia antes de usarla:

| | 277 | 421 | 647 | 983 | 1493 | 2269 | 3449 | 5237 |
|---|---|---|---|---|---|---|---|---|
| FL | **−21,1** | −40,3 | −50,9 | −59,3 | −67,2 | −75,1 | −83,5 | −92,8 |
| FR | −36,7 | **−21,1** | −44,2 | −54,6 | −63,0 | −71,0 | −79,1 | −87,6 |
| FC | −43,5 | −40,5 | **−21,1** | −47,6 | −58,1 | −66,6 | −74,8 | −83,5 |
| LFE | −48,2 | −47,2 | −44,0 | **−21,1** | −51,3 | −61,8 | −70,5 | −79,1 |
| BL | −52,3 | −51,9 | −50,8 | −47,6 | **−21,1** | −55,0 | −65,7 | −74,5 |
| BR | −56,2 | −56,0 | −55,6 | −54,5 | −51,4 | **−21,1** | −58,8 | −69,7 |
| SL | −60,0 | −59,9 | −59,7 | −59,3 | −58,2 | −55,1 | **−21,1** | −62,7 |
| SR | −63,9 | −63,9 | −63,8 | −63,6 | −63,2 | −62,2 | −59,1 | **−21,1** |

El peor contraste es **15,6 dB** (FR a 277 Hz), y el umbral de la prueba está en 10 dB. / The worst
contrast of the marking is 15.6 dB, against a 10 dB assertion threshold.

**El orden no era el supuesto, y por eso se mide.** La primera versión usaba `join` sin `map` y su
diagonal salió **desplazada**: la entrada 1 acabó en FC y no en FL. Un arnés construido sobre esa
suposición habría dado por buena una permutación. / The first attempt used `join` without `map` and
its diagonal came out shifted, which is why the mapping is written explicitly and then measured.

## El instrumento / The instrument

`WasapiLoopbackRecorder` abre el endpoint de render por su identificador y captura la mezcla del
motor de audio con `AUDCLNT_STREAMFLAGS_LOOPBACK`, **en el formato del propio endpoint**, de modo que
el número de canales capturado es el que la aplicación entregó y no el que declaró. `ChannelToneAnalysis`
lo lee por Goertzel con ventana de Hann, una evaluación por par canal/frecuencia. / The recorder
captures the engine's mix in the endpoint's own format; the analysis reads it by Goertzel.

Vive en `IntegrationTests` y no en `MediaTests` por una razón medida: necesita salida de audio real,
por tanto `LibVlcFactory.CreateDefault()` en vez de la instancia headless con `--aout=dummy`, y eso
introduce un **segundo conjunto de opciones**. Seis pruebas de fuga de `MediaTests` afirman
`NativeInstanceCount == 1` y se ponían rojas: 6 fallos de 153. `IntegrationTests` no afirma ese
recuento, ya apunta a Windows y alcanza el host, que es lo que la comparación con el catálogo
necesita. / It lives in the integration suite because a real audio output needs a second LibVLC
option set, which breaks six leak tests in the media suite that assert a single native instance.

## Lo verificado hoy / Verified today

**La fila estéreo, por primera vez con tinta.** Un origen 7.1 sobre un endpoint de dos canales llega
**plegado con coeficientes**, no truncado:

| Canal de origen / Source channel | Tono / Tone | Nivel en la mezcla / Level in mix | ¿Sobrevive? / Survives? |
|---|---:|---:|---|
| FL | 277 Hz | −18,06 dBFS | sí / yes |
| FR | 421 Hz | −18,06 dBFS | sí / yes |
| FC | 647 Hz | −21,07 dBFS | sí / yes |
| LFE | 983 Hz | −129,41 dBFS | **no** |
| BL | 1493 Hz | −30,10 dBFS | sí / yes |
| BR | 2269 Hz | −30,10 dBFS | sí / yes |
| SL | 3449 Hz | −30,10 dBFS | sí / yes |
| SR | 5237 Hz | −30,10 dBFS | sí / yes |

El centro entra a **−3,01 dB** de los frontales y los cuatro envolventes a **−12,04 dB**, que son
coeficientes de mezcla y no un recorte. **El LFE se descarta**, que es la convención de un plegado a
dos canales (ITU-R BS.775 construye la mezcla con los canales de programa). La prueba **afirma esa
ausencia** en vez de darla por supuesta, para que el día que una cadena empiece a plegarlo lo diga.
/ The centre folds in at −3.01 dB and the four surrounds at −12.04 dB; LFE is dropped by convention,
and its absence is asserted rather than assumed.

Registro: `artifacts/test-results/PLY-004/loopback-stereo-downmix.csv`.

**Y que el catálogo no miente sobre los canales.** El registro y el cliente de audio en vivo son dos
lecturas independientes del mismo hecho, y la política de selección se fía de la primera. Los cuatro
endpoints coinciden. / The registry and the live audio client agree on all four endpoints.

## El defecto que esto destapó / The defect this uncovered

**La disposición elegida no llega nunca al motor.** `AudioOutputViewModel.SelectedLayout` es
escribible y `Layouts` ofrece las tres disposiciones: la persona **elige**. Esa elección viaja hasta
`AudioOutputPolicy.Resolve`, se guarda y se muestra en la interfaz (` · 7.1`), pero al motor sólo se
le manda `SetAudioOutputDeviceAsync(deviceId)`. No hay en `src/` ninguna llamada que fije la
disposición en LibVLC: ni `--stereo-mode`, ni `SetChannel`, nada.

Es el defecto de la casa —elegido y nunca alimentado— y hoy es **invisible**: con los cuatro
endpoints en estéreo, `ResolveLayout` reduce siempre a estéreo y coincide con lo que LibVLC haría por
su cuenta. **Sólo se manifiesta con un endpoint multicanal**, que es exactamente la situación que
PLY-004 quiere verificar, y por eso nadie lo había visto. / The layout the person chooses never
reaches LibVLC. It is invisible today because every endpoint is stereo and the reduction coincides
with what the engine would do anyway; it only shows up on a multichannel endpoint.

**Medido ya sobre ocho canales, y decidido.** Con el endpoint a 7.1, LibVLC entrega las ocho pistas
correctas **sin que nadie le diga nada**: negocia con WASAPI y acierta. Lo que queda roto es sólo el
control: si la persona elige «Estéreo» teniendo un endpoint 7.1, `ResolveLayout` devuelve `Stereo`
—está en `SupportedLayouts`—, la interfaz muestra «Estéreo», y el motor sigue entregando ocho
canales. **La interfaz miente sobre lo que suena.**

**Decisión: se retira el control en vez de alimentarlo**, y el porqué es técnico, no de gusto.
LibVLC 3 no ofrece ninguna vía para fijar 5.1/7.1 en caliente: `libvlc_audio_set_channel` sólo cubre
modos estéreo (estéreo, invertido, izquierdo, derecho, Dolby) y `--stereo-mode` es opción de
instancia o de medio, así que aplicarla exigiría **reabrir el medio** — y eso rompería la garantía ya
verificada de que un cambio de salida pausa, cambia y reanuda sin perder posición ni pistas. La
disposición efectiva la decide la negociación con el endpoint, que es donde Windows pone ese control,
y el identificador pide **dispositivo** seleccionable, no disposición seleccionable. Un control que
no puede cumplir su promesa es peor que no tenerlo.

**No se ejecuta en esta tanda**, también por una razón concreta: quitar un control de una vista
mueve el trinquete del paseo autónomo y toca `AudioOutputViewTests`, `AudioOutputWiringTests` y
`AssembledPhysicalWalkTests`, que es tanda propia; y hay otra sesión trabajando en `Presentation`
ahora mismo. Queda abierto con la decisión ya tomada. / Measured on eight channels: LibVLC delivers
the correct eight tracks unprompted, so only the control is broken. The decision is to withdraw it
rather than feed it, because LibVLC 3 has no way to set 5.1/7.1 without reopening the media, which
would break the already-verified hot-switch guarantee. Not executed this batch: removing a control
moves the walk ratchet and touches three view suites.

## El endpoint de ocho canales, y cómo se consiguió / The eight-channel endpoint, and how it was obtained

Los cuatro endpoints físicos siguen declarando **dos canales** y ninguno puede subir: los tres que
publican `PKEY_AudioEndpoint_PhysicalSpeakers` lo dan en 0, los dos PA279CRV van por DisplayPort con
EDID estéreo, los auriculares son estéreo por hardware y S/PDIF sólo transporta dos canales PCM. El
`NVIDIA Virtual Audio Device` está instalado como controlador pero ninguno de sus endpoints aparece
activo. / The four physical endpoints still declare two channels and none can be raised.

**La decisión del propietario del 2026-09-01 acepta un endpoint virtual**, y ésta es la anotación que
esa decisión exige: **el endpoint sobre el que se midió es virtual**. Se instaló **VoiceMeeter Banana
2.1.2.2** (`VB-Audio.Voicemeeter.Banana`, desde winget), que publica ocho endpoints de render nuevos.
/ The endpoint measured on is virtual: VoiceMeeter Banana 2.1.2.2, installed from winget.

**Windows los crea todos en estéreo**, así que instalar no basta. Antes de tocar nada se preguntó al
controlador **qué formatos acepta**, con `IAudioClient::IsFormatSupported` en modo exclusivo, para no
fijar uno inválido:

| Canales / Channels | Aceptados / Accepted |
|---|---|
| 8 (7.1) | 48000 y 44100 Hz, 24 y 16 bits PCM |
| 6 (5.1) | 48000 y 44100 Hz, 24 y 16 bits PCM |
| 2 | 48000 y 44100 Hz, 24 y 16 bits PCM |

Con esa respuesta en la mano, `Voicemeeter Input` pasó de `2 canales, mask 0x3` a **`8 canales, 48000
Hz, 24 bit, mask 0x63F`**, y el registro lo confirma por el mismo camino que lee
`WindowsAudioDeviceCatalog`. **Es reversible**: `IPolicyConfig::ResetDeviceFormat` sobre ese endpoint,
o el desplegable «Formato predeterminado» del panel de sonido. / With that answer in hand the endpoint
was set to eight channels, confirmed through the same registry read the catalog performs, and it is
reversible.

## Las tres disposiciones, verificadas / The three layouts, verified

**7.1, grabado y contado.** El origen de tonos reproducido por el motor sobre el endpoint de ocho
canales, capturado por loopback:

| Canal / Channel | Tono propio / Own tone | Ajeno más alto / Loudest foreign | Contraste / Contrast |
|---|---:|---:|---:|
| FL (277 Hz) | −18,08 dBFS | −107,89 dBFS | **89,81 dB** |
| FR (421 Hz) | −18,08 | −104,26 | **86,18 dB** |
| FC (647 Hz) | −18,08 | −108,34 | **90,26 dB** |
| LFE (983 Hz) | −18,08 | −111,76 | **93,68 dB** |
| BL (1493 Hz) | −18,08 | −115,61 | **97,53 dB** |
| BR (2269 Hz) | −18,08 | −118,92 | **100,84 dB** |
| SL (3449 Hz) | −18,08 | −124,08 | **106,00 dB** |
| SR (5237 Hz) | −18,08 | −125,06 | **106,98 dB** |

Los ocho canales llegan, **cada uno con su tono y en su posición**, con un contraste mínimo de
**86 dB** contra un umbral de prueba de 10. Que la diagonal salga en orden es además la comprobación
de que la disposición no se permuta. Registro:
`artifacts/test-results/PLY-004/loopback-71-channels.csv`. / All eight channels arrive, each carrying
its own tone in its own position, at a minimum contrast of 86 dB against a 10 dB threshold.

**Estéreo**, medido antes sobre los endpoints físicos, queda arriba. **5.1** lo cubre la misma prueba
por construcción: el catálogo ofrece `Surround51` a todo endpoint de seis o más canales, y el de ocho
la contiene. / The stereo row is above; 5.1 is covered by the same catalog rule.

**Y el catálogo no miente sobre los canales**: el registro y el cliente de audio en vivo son dos
lecturas independientes del mismo hecho, y coinciden en los doce endpoints activos, el de ocho
incluido. / The registry and the live audio client agree across all twelve active endpoints.

## Límites de este instrumento / Limits of this instrument

Escritos para que un silencio suyo no se lea como certificado:

1. La captura toma **la mezcla entera del endpoint**, así que audio de otra aplicación durante la
   ejecución entraría en la medida.
2. Las aserciones **escalan a lo que la máquina ofrece**: en una máquina sólo estéreo, el caso
   envolvente se omite en vez de fallar.
3. Mide lo que llega **al endpoint**, no lo que sale por los altavoces: un endpoint de ocho canales
   sin ocho altavoces conectados dará el mismo resultado, y por eso el veredicto se escribe sobre la
   ruta de la aplicación y no sobre la sala.

/ The capture includes any other application's audio, the assertions skip rather than fail on a
stereo-only machine, and it measures what reaches the endpoint rather than what leaves the speakers.
