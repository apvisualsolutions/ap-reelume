# Un dueño para la instancia nativa / One owner for the native instance

Evidencia de **BUG-010**: el sondeo de medios tenía su propia instancia de LibVLC y su propia cola de
liberación, y ninguna de las dos era visible desde donde se cuentan. / Evidence for **BUG-010**: the
media probe kept its own LibVLC instance and its own release queue, and neither was visible from
where they are counted.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-11.

## El rojo / The red

`LibVlcFactory` declara —y `ApplicationHost` repite— que el proceso mantiene **exactamente una**
instancia nativa por juego de opciones, y `NativeInstanceCount` es quien lo afirma. Medido sobre
`src/`: / The factory states exactly one native instance per option set, and `NativeInstanceCount` is
what states it. Measured over `src/`:

```
src/…/Media/LibVlcMediaProbe.cs:77    Core.Initialize();
src/…/Media/LibVlcMediaProbe.cs:78    return new LibVLC(          ← mismas tres opciones
src/…/Playback/LibVlcFactory.cs:217   Core.Initialize();
src/…/Playback/LibVlcFactory.cs:218   instance = new LibVLC(options);
```

Dos instancias con **el mismo juego de opciones** en cualquier proceso que catalogue y reproduzca, y
el contador sólo ve la suya: no es que contara mal, es que **no podía ver** la segunda. Un contador
que no alcanza lo que cuenta es peor que no tener contador. / Two instances with the same options,
and the count could not see the second one.

La segunda mitad es la cola. La del sondeo desechaba el medio **sin guarda**:

```csharp
_ = DeferredMediaReleases.Dequeue();
pending.Media.Dispose();          // sin try/catch — LibVlcMediaProbe.cs:113-114
```

y su bandera `releaseWorkerScheduled` queda en `true` al salir por excepción, así que **una sola
liberación que falle acaba con el trabajador para siempre** y todo medio sondeado después se filtra
en silencio. La cola de la fábrica ya vive con esa guarda puesta y dice por qué. / The probe's drain
disposed without a guard and left its worker flag raised, so one failing release would end the worker
for good and leak every media probed after it. The factory's drain already carries that guard.

Las tres pruebas nuevas, contra el código anterior: / The three new rules, against the previous code:

```
Only_the_factory_constructs_the_native_instance [FAIL]
  The native LibVLC instance has exactly one owner, and these build their own:
  src/ApSolutions.LocalMedia.Infrastructure/Media/LibVlcMediaProbe.cs.
Only_the_factory_initialises_the_native_core [FAIL]
The_deferred_release_queue_has_one_implementation_and_it_survives_a_throwing_dispose [FAIL]
```

## La corrección / The fix

El sondeo recibe la fábrica y le pide el medio; la liberación es la suya. Desaparecen la instancia
propia, la cola, el trabajador y su bandera: **−67 líneas, +40**. La regla queda escrita como prueba
de arquitectura sobre `src/`, porque en tiempo de ejecución la segunda instancia es invisible —que
es exactamente el defecto. / The probe takes the factory and asks it for the media; the deferral is
the factory's. Its own instance, queue, worker and flag are gone. The rule is a source rule because
at runtime the second instance is invisible, which is the defect itself.

## Lo que la regla encontró y el plan no nombraba / What the rule found and the plan did not name

`LibVlcMediaPlayerEngine` guarda **una tercera** cola con el mismo `Dispose` sin guarda. No se
unifica en este commit, y la razón es de orden nativo, no de tiempo: su `DisposeAsync` **espera a su
propio drenaje antes de soltar el reproductor**, y ese orden es lo que impide que la destrucción
nativa se lleve el proceso por delante; moverlo a la cola compartida exige que la fábrica sepa vaciar
a petición. Queda en una lista **que sólo puede encoger**, con su nombre a la vista en cada
ejecución, igual que la lista de huérfanos de `ServiceConsumptionTests`. / The engine keeps a third
queue with the same unguarded dispose. Unifying it is not the same change — its `DisposeAsync` awaits
its own drain before releasing the player, and that order is what keeps the native teardown from
crashing — so it is named on a shrink-only list rather than quietly exempted.

Es el mismo hallazgo que la casa ya conoce con otro nombre: **el plan nombró un caso y la regla
encontró la clase**. / The plan named one case; the rule found the class.

## Verde / Green

| Puerta / Gate | Resultado / Result |
|---|---|
| `NativeInstanceOwnershipTests` | 3 de 3 / of 3 |
| `ApSolutions.LocalMedia.ArchitectureTests` | 23 de 23 / of 23 |
| `IncrementalScanTests` (sondeo real / real probe) | 5 de 5 / of 5 |
| `MediaProbeInstanceTests` | 1 de 1 / of 1 — el medio sondeado descansa en la cola de la fábrica |
| `eng/verify.ps1` completo / full | verde / green |
