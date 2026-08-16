# La regla de aislamiento y los dos enlaces al tráiler / The isolation rule and the two trailer links

Una ejecución cuya raíz de datos **no** es la del perfil no escribe ni abre nada fuera de esa raíz.
Con eso, los dos `DetailsTrailerLinkAction` —el de la ficha de película y el de la ficha de serie—
dejan de ser incubribles, y por primera vez se comprueba **a dónde llevan**. / A run whose data root
is not the profile's writes and opens nothing outside that root, which is what makes both
provider-trailer links pressable — and their address assertable for the first time.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 53 | **55** |
| Pendientes / Pending | 75 | **73** |

## El rojo, archivado / The red, archived

La regla, escrita como prueba sobre la aplicación **ensamblada**: una ejecución con raíz propia
resuelve un lanzador que no habla con el shell. / The rule, written as a test against the assembled
application.

```
IsolatedRunTests.A_run_that_does_not_own_the_profile_writes_the_address_instead_of_opening_it [FAIL]
  Assert.IsNotType() Failure: Value is the exact type
  Expected: typeof(ApSolutions.LocalMedia.Windows.Metadata.ShellExternalLinkLauncher)
  Actual:   typeof(ApSolutions.LocalMedia.Windows.Metadata.ShellExternalLinkLauncher)
```

El tipo se afirma **antes** de lanzar nada, y ese orden es la medición misma: si esta ejecución
resolviera el lanzador que habla con el shell, la llamada siguiente abriría un navegador de verdad en
la máquina que está midiendo. Por eso el control llevaba desde el principio la nota «unreachable». /
The type is asserted before anything is launched, and that order is the measurement.

## Lo que cambió / What changed

**`IAppDataPaths.SystemHandoffDirectory`**, la segunda mitad de la regla que `StartupRegistrySubKey`
estrenó. Decide por la **raíz resuelta**: quien mueve sus datos con `AP_LOCALMEDIA_DATA_ROOT` sigue
siendo quien inicia sesión aquí, así que su navegador se abre igual; una raíz cualquiera recibe una
carpeta bajo ella donde dejar lo que habría entregado al sistema. Es `null` y no una carpeta que nadie
usa, porque la distinción no es **dónde** ocurre la entrega, sino **si** ocurre. / It decides by the
resolved root, and it is `null` rather than an unused folder because the distinction is whether the
handover happens at all.

**Una sola política, dos salidas.** Las negativas —sólo `https` y sin información de usuario— salen de
`ShellExternalLinkLauncher` a `ExternalLinkPolicy`, en el dominio, y las usan las dos salidas. No es orden por el orden: la salida aislada existe **para** poder afirmar sobre
lo que la otra habría abierto, y eso vale exactamente mientras las dos dejen pasar lo mismo. / One
policy, two exits: the isolated exit exists in order to assert on what the other would have opened,
which is worth something only while both let the same things through.

**La composición elige una vez, por la raíz.** El registro deja de nombrar un tipo y pasa a decidir:
`SystemHandoffDirectory` con valor construye el que anota, sin valor el que llama al shell. /
The composition decides once, by the root.

## Lo que la escena mide / What the scene measures

Dos fichas, dos claves distintas guardadas —`FilmTrailer` en la película, `ShowTrailer` en la serie—,
dos pulsaciones con el ratón, y el archivo leído después:

```
https://www.youtube.com/watch?v=FilmTrailer
https://www.youtube.com/watch?v=ShowTrailer
```

Cada ficha abrió **el tráiler de su propio título**, en el orden en que se pulsaron. La segunda línea
es lo que prueba que la ficha de serie es un control distinto y no el de la película otra vez: una
misma clave de recurso declarada en dos vistas son los dos controles que es. / Each card opened its
own title's trailer, in the order pressed; the second line is what proves the series card is a
control of its own.

Y lo que nadie comprobaba: hasta hoy, la afirmación sobre esa dirección se detenía en el modelo de
vista, porque la capa siguiente iba a un navegador real. Ahora se lee **al final del camino
ensamblado**: ficha → comando → política del enlace → salida. / Until today the assertion stopped at
the view model, because the layer past it went to a real browser.

## Lo que la puerta de cobertura encontró al mirar / What the coverage gate found

Al pasar las negativas a un archivo nuevo, la puerta las midió por primera vez y dio **7 de 8 ramas**.
La que faltaba era la comprobación de anfitrión vacío, que el lanzador llevaba desde el principio y a
la que **nunca había llegado nada**: exigido ya `https`, una dirección absoluta sin anfitrión no se
construye. Se midió con siete formas —`https://`, `https:`, `https:///`, `https:////`,
`https://:8080/`, `https://@/` y `https:///watch?v=…`— y **ninguna** llega a ser un `https` con
anfitrión vacío. Una guarda que no puede ejecutarse no es una defensa: es la apariencia de una. Se
retiró, y la medición se queda en su sitio como la razón por la que no está — con la condición
escrita: si algún día se admite un segundo esquema, la comprobación vuelve con él. / The gate measured
the refusals for the first time and found a guard nothing had ever reached; seven spellings of a
host-less https address were measured and none parses, so the guard was removed and the measurement
left in its place.

## El argumento de seguridad / The security argument

Esto mueve **dónde** se escribe una decisión, no **quién** puede tomarla: quien puede fijar la raíz de
datos ya podía escribir en `HKCU` y abrir un navegador. La superficie de red no cambia —
`NetworkPurposeRegistry` no gana ningún propósito ni ningún anfitrión, porque aquí no se conecta
nada — y lo que puede salir sigue siendo lo mismo, ahora en un solo sitio: `https`, sin información de
usuario, con anfitrión propio. / This moves where a decision is written, not who may make it; the
declared network surface is unchanged.

## Las puertas / The gates

`dotnet format --verify-no-changes --severity warn`, compilación con `-warnaserror` (0 avisos) y la
solución entera con cobertura: dominio (465), aplicación (223), arquitectura (26), integración (445),
interfaz (439), accesibilidad (93), medios (116), rendimiento (18), empaquetado (152) y documentación
(87) — **2 064 pruebas, ningún rojo**. `eng/check-walk-coverage.ps1`: **129 controles declarados en
128 identidades; 55 pulsados, 73 pendientes**. `eng/check-coverage.ps1`: los dos archivos nuevos al
**100 % de líneas y de ramas**, y los tres vigilados en su suelo. / All green.

Y una nota de método, porque costó una ejecución: la puerta de cobertura leyó primero los informes de
una verificación **anterior** y dijo «sin líneas instrumentables» de los dos archivos nuevos — un
verde que no medía nada. Los informes hay que dirigirlos a la carpeta que la puerta lee, y esperar a
que **todas** las suites terminen: a mitad de ejecución faltaban dos y el veredicto fue rojo por
ausencia. / The gate first read a previous run's reports and passed on files it had not measured.
