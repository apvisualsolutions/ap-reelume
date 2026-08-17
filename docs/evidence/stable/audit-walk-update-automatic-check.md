# El permiso para mirar, pulsado / The permission to look, pressed

El único de los cinco controles del actualizador que no necesita nada preparado, y el que decide si
esta aplicación abre una conexión que nadie le pidió. / The one of the updater's five controls that
needs nothing staged, and the one that decides whether this application opens a connection nobody
asked for.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 90 | **91** |
| Pendientes / Pending | 38 | **37** |

```
The walk: 129 declared command controls in 128 identities; 91 pressed, 37 pending.
```

## La sonda, y por qué no es la casilla / The probe, and why it is not the box

Lo que este control decide **tiene que sobrevivir a la ventana**: al siguiente arranque, lo que se lee
es el ajuste guardado y nada más. Así que la sonda es `IUpdateSettings.AutomaticCheckEnabled`
—`StoredUpdateSettings` no guarda copia en memoria: pregunta al almacén cada vez— y después se lee el
**archivo**. Afirmar sobre la casilla habría probado que una casilla conserva lo que se le hizo. / The
probe is the stored setting, and the file is read afterwards; asserting on the box would prove a box
keeps what was done to it.

**No se contacta con nada al pulsarlo.** El pase automático se dispara desde `ConfigureWindow`, que
esta escena no llama, así que lo medido aquí es la preferencia y sólo la preferencia. / Nothing is
contacted: the automatic pass runs from `ConfigureWindow`, which this scene does not call.

## Lo que queda de la 7a, investigado y no re-deliberable / What is left of 7a, investigated

Los otros cuatro —buscar, descargar, instalar y «Cancelar»— necesitan que la fuente y la descarga
dejen de salir a la red, y **hay tres cosas medidas que deciden cómo**: / The other four need the
source and the download to stop reaching the network, and three measured facts decide how:

1. **Ningún archivo de `src/` puede nombrar un host que el registro no declare.**
   `NetworkPrivacyTests.No_source_file_names_a_host_that_is_neither_declared_nor_handed_off` recorre
   el árbol buscando `https?://…`. Declarar un host de arnés como propósito de red **mentiría sobre lo
   que la aplicación conecta** y ensancharía `IsDeclaredHost`, que es en lo que confía el canario. Así
   que **la dirección la trae el manifiesto** de la carpeta de traspaso, y el código no nombra
   ninguna. / The source tree may name no undeclared host, so the address comes from the handover
   manifest and the code names none.
2. **`UpdatePolicy` exige `release.Sha256Signed`**, que es un **veredicto** que pone la fuente tras
   verificar minisign con la clave que el binario lleva dentro. Una fuente aislada no puede firmar sin
   que `UpdateSigningKey.PublicKey` dependa de la raíz, **y eso está prohibido**: sería mover una
   decisión de seguridad para poder probar. **La fuente del arnés afirma el veredicto**, igual que un
   doble en una unitaria, y esto se dice aquí en vez de callarse: en una ejecución aislada **la firma
   no se verifica porque no hay nada firmado**. Lo que sigue siendo real es lo que se conserva a
   propósito — el hash, el tamaño y el `.partial`, porque `VerifiedUpdateDownloader` se mantiene sobre
   un transporte local. Minisign se prueba donde ya se prueba: en sus unitarias, con sus vectores. /
   The policy requires a signed-checksums verdict that only the real provider can earn; the harness
   source asserts it, and that is stated rather than hidden. The hash, the size and the `.partial`
   stay real because the verified downloader is kept over a local transport.
3. **`VerifiedUpdateDownloader` ya admite su lista de anfitriones por parámetro** —«tests hand in
   their loopback server explicitly»—, así que el transporte local no obliga a tocar la allowlist del
   producto. / The downloader already takes its allowlist as a parameter, so the local transport
   forces no change to the product's.

Queda además el **lanzador**: `WindowsUpdateLauncher(OpenWithWindows)` entrega el paquete a Windows
con `Process.Start`. Aislado, anota el paquete que habría entregado — la misma forma que ya tienen las
cinco salidas de la regla. / The launcher hands the package to Windows; isolated, it writes down which
package it would have handed over.

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet test tests/ApSolutions.LocalMedia.AccessibilityTests -c Release -m:1 --settings eng/test.runsettings --logger trx
./eng/check-walk-coverage.ps1
```
