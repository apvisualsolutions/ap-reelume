# La disposición de canales no la decide el reproductor, y por eso el control no hacía nada / The channel layout is not the player's to decide, which is why the control did nothing

`AudioOutputViewModel.SelectedLayout` llevaba desde su origen aceptando una elección que moría antes
de llegar al motor. La decisión registrada era **retirar el control**; el propietario pidió lo
contrario —«si quiero 7.1, ¿por qué iba a sonar estéreo?»— y esta tanda lo hace funcionar. / The
recorded decision was to withdraw the control; the owner asked for the opposite, and this batch makes
it work.

Fecha / Date: 2026-09-02. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## La pregunta estaba al revés / The question was the other way round

**Con un equipo 7.1 ya suena 7.1**, y eso estaba medido el día anterior: LibVLC negocia con WASAPI y
entrega las ocho pistas sin que nadie se lo pida. El botón «7.1» no podía dar nada que no estuviera
ya puesto. Lo que no se podía era **pedir menos** —el caso que el propietario nombró: un altavoz
roto—. / With 7.1 equipment it already sounds 7.1. What could not be done was asking for less.

## Seis vías, medidas / Six routes, measured

Sobre el endpoint de ocho canales, reproduciendo la muestra de ocho tonos y grabando por loopback: /
On the eight-channel endpoint, playing the eight-tone sample and recording by loopback:

| Vía / Route | Resultado / Result |
|---|---|
| `MediaPlayer.SetChannel` (la única API en caliente) | **Nada.** Los ocho tonos idénticos, −18 dB |
| `--stereo-mode=1` | **Nada** |
| `--stereo-mode=7` (mono) | Cambia, pero da **mono**: los ocho a −36 dB |
| `--audio-filter=mono` | **Nada** |
| `--audio-channels=2` | **No existe**: la instancia no arranca |
| `--aout=waveout` / `directsound` | No llegan al endpoint: silencio |

La enumeración completa de la única API de canales es `Stereo, RStereo, Left, Right, Dolbys, Error`
— leída del ensamblado, no de la memoria—. **No hay 5.1 ni 7.1 en ella.** / Read off the assembly.

## Lo que sí lo decide / What does decide it

El formato del propio endpoint, que es un ajuste de Windows. Escrito con `IPolicyConfig`, la misma
interfaz que llama el panel de sonido: / The endpoint's own format, a Windows setting:

```
identity: administrator=False
before: 8ch @ 48000
SetDeviceFormat(2ch) -> hr=0x00000000
after set: 2ch @ 48000
recorded=2ch  FL=-18.2 FR=-18.2 FC=-21.2 LFE=-105.2 BL=-30.2 BR=-30.3 SL=-30.4 SR=-30.7
restore(8ch) -> hr=0x00000000
```

**Sin privilegios de administrador**, y el plegado es el de la convención: centro a −3,0 dB, los
cuatro surround a −12 dB, **LFE descartado**. / Without administrator rights, and the fold is the
conventional one.

## Las tres cosas que costó aprender / The three things it cost to learn

**1. El formato se lee antes de escribirlo.** Construir uno a mano contestó
`AUDCLNT_E_UNSUPPORTED_FORMAT`: el controlador toma 24 bits y la conjetura ofrecía 32. / Building one
by hand was refused.

**2. El orden de las dos operaciones es el arreglo.** Escribir el formato invalida el cliente de
audio de todos los programas —`AUDCLNT_E_DEVICE_INVALIDATED`, documentado por Microsoft— y la
recuperación de LibVLC **descarta el dispositivo elegido y cae al predeterminado**:

```c
if (unlikely(hr == AUDCLNT_E_DEVICE_INVALIDATED ||
             hr == AUDCLNT_E_RESOURCES_INVALIDATED))
    DeviceSelect(aout, NULL);
```

Eso explicó una medición que parecía decir «el sonido se pierde»: **no se perdía, se iba a otro
dispositivo**, mientras la grabación escuchaba donde ya no sonaba. Por eso la disposición se escribe
**antes** de enrutar, y el enrutado es lo que devuelve el sonido al sitio elegido. / The sound was not
lost; it moved.

**3. Lo que se ofrece lo dice el controlador, en PCM entero.** Preguntar con el formato del mezclador
—que va en coma flotante— hizo que un endpoint de ocho canales contestara «sólo estéreo»: en modo
exclusivo el controlador quiere PCM. Y preguntar al catálogo en vez de al controlador habría hecho
del control **una puerta de un solo sentido**, porque el catálogo lee la disposición actual: quien
bajara a estéreo no habría podido volver a subir. / Asking the catalogue would have made it a one-way
door.

## Lo que la interfaz dice, y cuándo / What the interface says, and when

- **Antes de elegir**: que esto cambia un ajuste de Windows y afecta a todos los programas del
  equipo. Antes y no después, que es la diferencia entre cambiar algo del sistema y cambiarlo a
  espaldas de alguien.
- **Al aplicarse**: que la disposición está puesta en Windows y los demás programas también sonarán
  así.
- **Al rechazarse**: que ese dispositivo no la admite, y que hay que elegir otra salida.
- **Donde no se puede escribir**: que los tres valores son un indicador. Una elección que no se puede
  cumplir no se ofrece.

## Lo que queda dicho por escrito / What is stated rather than discovered later

**`IPolicyConfig` no está documentada por Microsoft**, y se usa a sabiendas porque no hay equivalente
documentado — las seis alternativas están medidas arriba. `WindowsAudioEndpointConfiguratorTests`
existe para que el día que Windows la cambie **se vea**, en vez de dejar un control que calladamente
deja de hacer nada, que es el defecto que esta tanda vino a quitar. / Accepted knowingly, with a test
whose job is to fail loudly.
