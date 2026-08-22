# Los veintiún mensajes de la actualización ya cumplían, y el reproductor ofrece elegir entre una sola versión / The update's twenty-one messages already complied, and the player offers a choice between one version

Medición del tramo 8 de la §4, **sin escribir código**. / §4's eighth tranche, measured without writing code.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## La mitad de la fila que ya estaba hecha / The half of the row already done

La fila pide «23 mensajes en cuatro gramáticas». Contados el 2026-08-22: **14 estados** (`UpdateStatus*`
sin el nombre accesible), **7 rechazos** (`UpdateRefused*`) y el aviso de confirmación. Y las cuatro
gramáticas están escritas en cada uno — qué ha pasado, qué se conserva, qué **no** se ha hecho, y qué
se puede hacer: / Counted, and the four grammars are already in each one:

> «La descarga se ha cortado. Lo que llegó se conserva y volver a intentarlo continúa desde ahí.»
>
> «El paquete había cambiado desde que se comprobó. Se ha descartado sin instalarlo.»
>
> «No se ha podido preguntar. **Eso no quiere decir que estés al día**, sino que no se ha sabido.»

## Y el mapa de los diez rechazos, que cierra / And the ten-rejection map, which closes

`DetailKey` se construye **concatenando el nombre del enum** —`$"UpdateRefused{rejection}"`—, que es la
forma de la que este repositorio ya ha sacado defectos: una clave compuesta sin puerta que compruebe
que existe. `UpdateRejection` tiene **diez** valores y hay **siete** cadenas, así que parecía haber
tres huecos. Medido uno por uno, **no hay ninguno**: / A key built by concatenation, with ten values
and seven strings — and measured, no hole.

| Valor / Value | Cómo llega a la pantalla / How it reaches the screen |
| --- | --- |
| `None` | No es un rechazo. |
| `NoReleaseAvailable` | Es `UpdateStatusUpToDate` **a propósito**: no tener nada más nuevo y haber preguntado a una fuente que no publicó nada son la misma noticia, y el código lo dice donde lo decide. |
| `NotNewer` | Igual que la anterior, y por la misma razón. |
| `InsecureDownload`, `UnusableHash`, `UnsignedChecksums`, `WrongRuntime`, `UndeclaredSize`, `IncompleteSummary` | Los seis de la lista cerrada: `UpdateStatusUnusableRelease` **con su detalle nombrado**. |
| `UndeclaredHost` | **No sale de una comprobación**: sólo lo emite `VerifiedUpdateDownloader` durante la descarga, y llega por el `catch (UpdateRefusedException)`, que sí gasta su cadena. |

Ese último es el que parecía el defecto —una cadena escrita en dos idiomas y ausente de la lista del
`switch`— y **no lo es**: la lista no lo nombra porque una comprobación no puede producirlo. Es la
décima alarma falsa que se apaga midiendo en vez de deduciendo. / The one that looked like the defect
is not one, and measuring is what said so.

**`UpdateView` cierra sin tocarla**, como `PrivacySettingsView` en el tramo 5. / It closes untouched.

## ⚠ El reproductor ofrece elegir entre una sola versión / The player offers a choice between one version

Lo que **no** está hecho está en `PlayerView`, y es más grande de lo que la fila sugiere.

`PlaybackDiagnosticsPolicy.RecoveryActionsFor` decide las acciones **por motivo**, que es lo que la §4
pide y ya se cumple. `ChooseAnotherVersion` es la más ofrecida de las tres: la dan **cinco de los siete
códigos** —todos menos `EngineUnavailable` y `None`—. / The domain offers it for five of the seven
codes.

Y se decide **por el código de fallo**, sin mirar si existe otra versión. `PlayerViewModel` no conoce
`PlayerVersionsViewModel`, así que: / And it is decided by the failure code, without looking at whether
another version exists:

```csharp
public bool CanChooseAnotherVersion => _recoveryActions.Contains(PlaybackRecoveryAction.ChooseAnotherVersion);
// vs, en el shell / vs, in the shell:
public bool HasPlayerVersions => Player?.Versions is { HasAlternatives: true };
```

**En el caso más común —un archivo que no tiene otras versiones— la pantalla de fallo dice «Elige otra
versión del mismo contenido en la ficha de versiones» a quien sólo tiene una.** Y lo dice como texto,
sin nada que pulsar. / In the commonest case the screen says "choose another version" to somebody who
has one.

Comparado con sus dos hermanas, las tres condiciones no se parecen — y las dos que sí miran si la
acción se puede ejecutar son las que tienen botón: / The two that check whether the action can be
carried out are the two with a button:

```csharp
CanRetry            => acción del dominio  &&  MediaPath no está vacío
CanOpenExternally   => acción del dominio  &&  hay lanzador  &&  MediaPath no está vacío
CanChooseAnotherVersion => acción del dominio          ← y nada más
```

## Lo que la pieza siguiente tiene que hacer, ya medido / What the next piece has to do, already measured

1. **`CanChooseAnotherVersion` pasa a exigir que existan alternativas.** `player` se construye en
   `CompositionRoot` **antes** que `versions`, así que la vía es un `Func<bool>` capturado en diferido,
   igual que `_externalLauncher` es un puerto opcional del constructor.
2. **El `TextBlock` pasa a `Button`** — el único cambio de tipo del paquete —, y su clave cambia de
   frase a etiqueta en los dos idiomas. La clave **se conserva**, para no dejar una cadena huérfana.
3. **El destino no es «la ficha de versiones»**: cuando hay alternativas, `PlayerVersionsView` **ya
   está en la misma pantalla**, porque `HasPlayerVersions` sólo pide que las haya. La frase manda a
   otro sitio a hacer algo que está debajo.
4. **Y cuesta una escena de paseo**: un botón nuevo entra en `eng/walk-pending.txt`, que está en **0 y
   no vuelve a subir**, así que hace falta una escena con un fallo de reproducción **y** un grupo de
   versiones. El paseo tiene las dos cosas por separado y ninguna junta.

## Y una asimetría sin coste, anotada y no tocada / And a costless asymmetry, noted and not touched

`PlaybackFailureCode` tiene **siete** valores y la vista pinta **seis**. El séptimo,
`UnsupportedCapability`, no llega nunca al bloque de fallo: lo produce `VideoOutputPolicy` como
`UnsupportedReason` de una decisión de vídeo —Dolby Vision, que **sí se reproduce** tone-mapped—, y eso
la interfaz **sí lo dice**, por `VideoStatusViewModel.IsUnsupportedFormat`, que `VideoStatusOverlay`
pinta. Comprobado antes de acusar. `RecoveryActionsFor` lo contempla igual, lo que es una rama que el
bloque de fallo no puede tomar; sin coste, y aquí queda escrito. / Checked before accusing: the
interface does say it, by another route.
