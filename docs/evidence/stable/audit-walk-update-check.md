# Buscar actualizaciones sin salir a la red / Checking for updates without reaching the network

La quinta salida de la regla de aislamiento y el segundo control del actualizador: preguntar si hay
una versión nueva. / The isolation rule's fifth exit and the updater's second control: asking whether
there is a newer version.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 91 | **92** |
| Pendientes / Pending | 37 | **36** |

```
The walk: 129 declared command controls in 128 identities; 92 pressed, 36 pending.
```

## Por qué la dirección no está en el código / Why the address is not in the code

Es la restricción que decidió la forma entera, y **no es una preferencia**:
`NetworkPrivacyTests.No_source_file_names_a_host_that_is_neither_declared_nor_handed_off` recorre
`src/` buscando cualquier cosa con forma de anfitrión y falla con la que el registro no declare. /
A test walks the source tree for anything shaped like a host and fails on one the registry does not
declare.

Y declarar uno de arnés **no era una salida**: `NetworkPurposeRegistry.Declared` responde a «¿qué abre
este proceso?», así que meter ahí un anfitrión que nadie contacta **mentiría en la dirección
peligrosa** y ensancharía `IsDeclaredHost`, que es en lo que confía el canario de red. / Declaring one
would lie in the dangerous direction and widen the check the canary trusts.

Así que la dirección es **dato**: vive en el manifiesto que la ejecución guarda bajo su propia raíz, y
el código no nombra ninguna. / So the address is data, in the manifest a run keeps under its own root.

## Lo que se afirma y no se gana, dicho en vez de callado / What is asserted rather than earned

`UpdatePolicy` exige `release.Sha256Signed`, que es **un veredicto**: la fuente real lo alcanza
verificando una firma minisign contra la clave que este binario lleva dentro. Una fuente de arnés no
puede alcanzarlo sin que `UpdateSigningKey.PublicKey` dependa de la raíz de datos, **y eso está
prohibido** — sería mover una decisión de seguridad para poder probar otra. / The policy requires a
verdict only the real provider can reach, and making the signing key depend on the data root is
forbidden.

**Entonces la fuente aislada lo afirma**, como un doble en una unitaria, y aquí queda escrito: en una
ejecución aislada **la firma no se verifica porque no hay nada firmado**. Minisign se prueba donde ya
se prueba, con sus propios vectores. / The isolated source asserts it, and in an isolated run the
signature is not verified because nothing is signed.

Lo que **no** se afirma es todo lo que la descarga demuestra: el hash, el tamaño y el `.partial` son
los del producto y se ejercitan de verdad — por eso el manifiesto **declara** hash y tamaño en vez de
calcularlos del archivo. Calcularlos allí convertiría la verificación en una tautología: comprobar el
archivo contra sí mismo. / The hash and the size are declared rather than computed from the file,
because computing them there would make the verification check the file against itself.

## Tres respuestas distintas, y no se confunden / Three different answers

| Carpeta de traspaso / Handover folder | Respuesta / Answer |
|---|---|
| Sin manifiesto / No manifest | No hay release — una respuesta / No release: an answer |
| Manifiesto ilegible / Unreadable manifest | Inalcanzable / Unreachable |
| Manifiesto de otra arquitectura / Another architecture | Rechazo con su motivo / A refusal with its reason |

La primera y la segunda se mantienen aparte por lo mismo que las mantiene aparte el actualizador real:
decir «estás al día» porque no se pudo averiguar es la peor forma de contestar. / The first two are
kept apart for the reason the real updater keeps them apart: reporting "up to date" because it could
not find out is the worst possible answer.

## Lo que la puerta cazó, y es lo peor que puede pasarle a una prueba / What the gate caught

**Una prueba se volvió ciega en vez de falsa**, que es la de las dos que no se ve:

```
CompositionDescriptorTests.The_update_source_the_application_builds_looks_where_the_changelogs_publish
Assert.IsType() Failure: Value is not the exact type
```

Esa prueba comprueba algo que importa —que la dirección a la que el actualizador **va a preguntar** es
la que los dos changelogs publican para que una persona la siga— y componía contra **una raíz propia**,
como el resto de su archivo. En el momento en que la fuente pasó a depender de la raíz, esa
composición dejó de construir el proveedor del que la prueba hablaba. / It checks that the address the
updater will ask is the one both changelogs publish, and it composed against a root of its own — so
the moment the source began to depend on the root, that composition stopped building the provider the
test was about.

Si hubiera fallado con un valor incorrecto, sería una prueba haciendo su trabajo. Falló porque **ya no
llegaba a lo que vigilaba**, y eso es exactamente lo que un cambio puede dejar sin ruido. / Failing on
a wrong value is a test working; failing because it no longer reaches what it watched is what a change
can leave silent.

**La regla que sale de aquí:** una prueba sobre lo que la aplicación **conecta** tiene que componer
como la ejecución que conecta. Y para que la ceguera no pueda volver, `IsolatedRunTests` afirma ahora
**las dos mitades** de esta elección, como ya hace con las otras. / A test about what the application
connects to has to compose as the run that connects, and both halves of the choice are now asserted
where the other exits' are.

**Y una del arnés, que costó una ejecución:** `eng/run-accessibility.ps1` corre con `--no-build`,
porque es como lo corre CI. Ejecutarla después de editar y **antes** de compilar mide el binario
anterior — el segundo rojo de esta tanda fue eso y no el código. / The accessibility gate runs with
`--no-build`, so running it after editing and before building measures the previous binary.

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet test tests/ApSolutions.LocalMedia.PackagingTests -c Release -m:1 --settings eng/test.runsettings --logger trx --filter "FullyQualifiedName~HandoffUpdateSource"
./eng/check-walk-coverage.ps1
```
