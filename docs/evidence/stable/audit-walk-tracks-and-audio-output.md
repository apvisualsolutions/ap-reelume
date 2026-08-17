# Las pistas y la salida de sonido / The tracks and the audio output

Los cinco mandos que deciden **qué se oye** y **por dónde**, pulsados con el ratón sobre una sesión
real, con vídeo de verdad decodificando. / The five controls that decide what is heard and where it
goes, pressed with the mouse over a real session with real video decoding.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 96 | **101** |
| Pendientes / Pending | 32 | **27** |

```
The walk: 129 declared command controls in 128 identities; 101 pressed, 27 pending.
```

## El defecto: una casilla que nadie podía marcar / The defect: a box nobody could tick

El rojo, medido con el ratón: / The archived red, measured with the mouse:

```
RememberForSeriesToggle is on screen but cannot be pressed: visible=True, enabled=False.
```

`CanRememberForSeries` es `_seriesScopeKey is not null`, y la composición construía el selector con
**`seriesScopeKey: null` siempre**. Así que «Recordar para esta serie» estaba **deshabilitada en todos
los episodios que se hayan reproducido nunca**. / The composition built the selector with a null
series scope unconditionally, so "remember for this series" was disabled for every episode ever
played.

**Y lo que lo convierte en algo peor que un botón muerto**: el lado que **lee** las preferencias sí
pedía la serie —`episodeEntry is not null ? seriesId… : null`, doce líneas más arriba en el mismo
método—. La aplicación resolvía un ámbito de serie que **nada dentro de ella podía escribir**. Es la
asimetría de la casa vista desde el otro lado: no un servicio registrado que nadie resuelve, sino un
almacén que se lee y no se llena. / The reading side already asked for the series, twelve lines above
in the same method: the application resolved a series scope that nothing in it could ever write.

**La corrección es que las dos claves se calculan una vez** y las usan los dos lados, para que no
puedan volver a discrepar: / The fix computes both keys once, so the two sides cannot drift apart
again:

```csharp
var fileScopeKey = mediaFileId.Value.ToString("D");
var seriesScopeKey = episodeEntry is not null ? seriesId.Value.ToString("D") : null;
```

## Lo que la escena afirma, que no es «la casilla se marca» / What the scene asserts

Marcar la casilla y comprobar que queda marcada probaría que una casilla es una casilla. Lo que se
comprueba es **lo que significa**: tras marcarla, la elección de pista se guarda **bajo la serie** y
**no** bajo el archivo. / Ticking a box and checking it is ticked would prove a box is a box. What is
checked is what it means: after ticking it, the track choice is stored under the show and not under
the file.

```csharp
Assert.NotNull(await preferences.GetAsync(PreferenceScope.Series, showId…));
Assert.Null(await preferences.GetAsync(PreferenceScope.File, firstFile…));
```

## Los cuatro desplegables / The four drop-downs

**El efecto de un desplegable es que se abre.** Lo que se elige dentro cae en una raíz de ventana
emergente propia, que es otro nivel superior y no es asunto de esta ventana — la misma regla que el
filtro y el orden de la biblioteca. Cada uno se cierra con Escape antes del siguiente, porque un clic
al lado con una emergente abierta es un clic en la ventana de otro. / A drop-down's effect is that it
opens; what is chosen inside lands in a popup root of its own. Each is closed with Escape before the
next.

**Y las listas se afirman aparte**, porque abrir una lista vacía no diría nada de la lista: la muestra
lleva **dos pistas de audio** (inglés y español) y **una de subtítulos**, y la de subtítulos en
pantalla trae una más que el archivo — la entrada que los apaga. / The lists are asserted separately,
because opening an empty list would say nothing about the list.

La muestra la genera el arnés con ffmpeg y se cachea como las demás: dos `sine` de frecuencias
distintas mapeadas como dos pistas con su idioma declarado, y un `.srt` como tercera. / The sample is
generated with ffmpeg and cached like the others.

## Un hallazgo que no es de esta tanda, y se dice en voz alta / A finding that is not this batch's

Mientras el rojo estaba en pie, el desmontaje trajo un segundo fallo: / While the red stood, the
teardown brought a second failure:

```
System.ObjectDisposedException : Cannot access a disposed object.
Object name: 'ApSolutions.LocalMedia.Infrastructure.Playback.LibVlcMediaPlayerEngine'.
   at PlaybackSessionCoordinator.StopActiveSessionAsync
   at PlaybackSessionCoordinator.DisposeAsync
   at ApplicationHost.DisposeAsync
```

**Apagar con una sesión todavía activa** lleva al coordinador a parar un motor que el contenedor ya ha
soltado. El motor está registrado tres veces —él mismo, `IMediaPlayerEngine` y `IVideoFrameSource`— y
la última se resuelve **cuando un vídeo empieza a dibujarse**, así que entra en la lista de desechado
*después* del coordinador y sale de ella *antes*. / Shutting down with a session still active takes
the coordinator through an engine the container has already released: the engine is registered three
times and the last registration is resolved when a video starts drawing, so it is disposed before the
coordinator that stops it.

**Por qué no se corrige aquí:** la política de cierre real **sí para la reproducción**
(`StopPlayback: hasActivePlayback && !hidesToTray`), así que este camino se alcanza cuando el apagado
llega por otra vía — y **el apagado directo desde la ventana y la bandeja está aparcado por decisión
previa** hasta que el rediseño toque el ciclo de vida. Queda escrito con su medición en la cola, que
es lo contrario de esconderlo. La escena cierra su sesión como hacen todas las demás y como hace una
persona. / It is not fixed here because the real close policy does stop playback, and the direct
shutdown path is parked by an earlier decision. It is written down with its measurement instead.

## Las puertas / The gates

```
dotnet format --verify-no-changes --severity warn          # limpio / clean
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1   # 0 avisos / 0 warnings
eng/run-accessibility.ps1 -Mode Verify -Passes 2           # 109 + 109, 0 críticos / 0 critical
dotnet test tests/ApSolutions.LocalMedia.UiTests            # 448
dotnet test tests/ApSolutions.LocalMedia.ArchitectureTests  # 26
eng/check-walk-coverage.ps1                                # 101 pulsados, 27 pendientes / pressed, pending
```

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
./eng/run-accessibility.ps1 -Mode Verify -Passes 2
./eng/check-walk-coverage.ps1
```
