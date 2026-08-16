# La pantalla que nadie alcanza, pulsada / The screen nobody can reach, pressed

Los dos controles de la pantalla que aparece cuando la base de datos no abre. Es la única superficie
de la aplicación a la que **ninguna ruta lleva**: se construye sólo cuando el arranque contesta que
no. / The two controls of the screen that appears when the database will not open — the one surface
no route leads to.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 88 | **90** |
| Pendientes / Pending | 40 | **38** |
| Pruebas de accesibilidad / Accessibility tests | 102 | **104** |

```
The walk: 129 declared command controls in 128 identities; 90 pressed, 38 pending.
Accessibility Verify over 2 pass(es): 0 critical, 0 major, 0 minor.
```

## El rojo, y las dos premisas que confirmó de una vez / The red, and the two premises it settled

Una `library.db` con basura dentro basta: la negativa la produce la aplicación, no la prueba. Y el
montaje del paseo dice exactamente lo que encontró en su sitio. / A `library.db` with rubbish in it
is enough, and the walk's own mount says exactly what it found instead:

```
Assert.IsType() Failure: Value is not the exact type
Expected: typeof(ApSolutions.LocalMedia.Presentation.Shell.ShellView)
Actual:   typeof(ApSolutions.LocalMedia.Presentation.Recovery.DatabaseRecoveryView)
```

283 ms. Las dos cosas que la cola daba por investigadas quedan medidas con una sola ejecución:
sembrar la negativa es trivial, y `ShowShell()` no puede alcanzar esta pantalla porque afirma lo
contrario. / One run settles both: seeding the refusal is trivial, and the shell's mount cannot reach
this screen because it asserts the opposite.

## Lo que costó el montaje / What the mount cost

**Un cambio de tipo.** `ShellHost.Shell` pasa de `ShellView` a `Control`, y los cinco usos que tiene
son todos `GetVisualDescendants()`, que es de `Visual`. El modelo de vista queda opcional **detrás de
la misma propiedad**, así que los sesenta y siete `host.ViewModel` no se tocan y una escena sin shell
que lo pida recibe una frase en vez de un `NullReferenceException`. / One type change: five uses of
`Shell` only walk the visual tree, and the view model stays optional behind the property it always
had, so sixty-seven uses are untouched.

No hicieron falta ni una segunda clase de anfitrión ni una segunda versión de `PressAsync`. Lo que sí
salió, porque las dos formas de montar compartían todo menos su última línea, es un `Mount` común: la
diferencia entre las dos **es** cuál de los dos resultados afirma. / No second host class and no
second `PressAsync`; what came out is a shared mount, because the difference between the two is which
settled content each asserts.

## Las sondas / The probes

Las dos leen **lo que la ejecución anotó**, no la pantalla:

| Control | Sonda / Probe |
|---|---|
| Abrir la carpeta de copias | `open-folder <carpeta>` en el registro de traspaso / in the handover record |
| Salir | `exit` en el mismo registro / in the same record |

Se lee como **texto**, no como lista, y no es un detalle de estilo: una sonda se compara por valor, y
devolver un array nuevo en cada lectura haría que el clic de control —el que no debe cambiar nada—
pareciera cambiar algo. / Read as text rather than as a list, because a probe is compared by value
and a fresh array every read would make the control click look like it did the work.

La carpeta que se afirma es **la que dice la aplicación**: la escena no compone ninguna ruta. Siembra
una copia con el nombre que el producto busca y deja que `DatabaseStartup.FindLatestBackup` decida
cuál ofrecer. / The folder asserted is the application's answer; the scene composes no path.

## Lo que no apareció / What did not turn up

**Ningún defecto de producto.** Es el tercero de estos en once tandas, y los dos anteriores también
fueron tandas donde la corrección ya se había hecho por separado —aquí, las dos salidas del commit
anterior—. Los dos botones están dentro de la ventana, se habilitan, y hacen lo que dicen. / No
product defect: the third such batch, and like the other two the correction had already been made
separately.

Merece decirse por qué **no** apareció el defecto de la casa esta vez, porque el patrón está
presente: la fila de botones es un `StackPanel` horizontal, que es lo que ha escondido cuatro
controles fuera de la pantalla en cuatro tandas distintas. Aquí no muerde porque **no hay ningún dato
de anchura libre a su lado**: las dos rutas viven en el `StackPanel` vertical de arriba, que sí
reparte la anchura y las envuelve. La regla sigue siendo la misma, y su condición es la vecindad, no
la orientación. / The horizontal `StackPanel` is there but does not bite, because nothing of unbounded
width sits beside the buttons — the two paths live in the vertical panel above, which does give its
children a width.

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet test tests/ApSolutions.LocalMedia.AccessibilityTests -c Release -m:1 --settings eng/test.runsettings --logger trx
./eng/run-accessibility.ps1 -Mode Verify -Passes 2
./eng/check-walk-coverage.ps1
```
