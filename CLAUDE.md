# AP Reelume — guía para agentes

Biblioteca de medios local para Windows 11: cataloga y reproduce vídeos que ya están en el disco de
quien la usa. Sin cuentas, sin telemetría, sin servidor. `GPL-3.0-or-later`.

Este archivo es para un agente que llega al repositorio sin contexto. Está en español porque el
proyecto se piensa en español y se publica en dos idiomas; el código, los commits y los nombres de
prueba van en inglés.

## Antes de tocar nada, lee esto en este orden

1. [docs/FEATURES.md](docs/FEATURES.md) — el registro **canónico** del alcance: qué existe, en qué
   estado y con qué evidencia. Si algo contradice esta guía, manda la matriz.
2. [docs/NEXT-SESSION.es.md](docs/NEXT-SESSION.es.md) — dónde se retomó por última vez.
3. [CONTRIBUTING.md](CONTRIBUTING.md) — el ciclo de trabajo, que no es opcional.
4. [docs/legal/LEGAL.es.md](docs/legal/LEGAL.es.md) — lo que está resuelto y lo que sigue abierto.

## Arranque

El SDK no está en el `PATH` del sistema:

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet --version   # la versión que fija global.json
```

Toda ejecución de pruebas sobre la solución lleva `-m:1 --settings eng/test.runsettings`. Sin eso,
las suites que tocan SQLite y LibVLC compiten entre sí y producen rojos que no son del código.

## El ciclo, en corto

**Rojo archivado → corrección mínima → verde con las puertas → evidencia → changelog en dos idiomas
→ un commit.**

Las puertas, todas, antes de cada commit:

```powershell
dotnet format --verify-no-changes --severity warn
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
dotnet test <suite afectada> -c Release -m:1 --settings eng/test.runsettings
pwsh -NoProfile -File eng/verify-docs.ps1
```

`eng/verify.ps1` las corre todas más el empaquetado y la puerta de cobertura.

**Quien verifica de verdad es CI, y por eso el orden cambió el 2026-08-18.** CI corre ese mismo
`verify.ps1` **y además** `run-accessibility.ps1 -Passes 2`, `run-recovery.ps1 -Passes 2` y
`check-walk-coverage.ps1`: es un superconjunto estricto de lo que puede correrse aquí, y las carreras
que sólo aparecen en la segunda pasada nunca las vio una ejecución local. Correr `verify.ps1` entero
antes de cada push era hacer dos veces el mismo trabajo —media hora de la máquina de quien programa,
por commit— para obtener una garantía **más débil** que la que llega después.

El orden es ahora:

1. Durante el trabajo, **las suites afectadas** en local. Segundos, y es donde se atrapa lo evidente.
2. `git add -A` → **commit** → push **sólo a la rama**.
3. **CI verifica**, una vez: desde el 2026-08-18 `main` no dispara el flujo, porque recibe el mismo
   SHA por fast-forward y un check pertenece al commit, no a la referencia.
4. **Con CI en verde**, el fast-forward a `main` — que es instantáneo y no vuelve a verificar.

Así `main` no recibe nunca un commit sin verificar, y lo garantiza el paso 3 en lugar de una
verificación local más floja. Lo que se cede está medido: un fallo se conoce unos cuarenta minutos
más tarde, en la rama, donde no molesta. **Lo que exige es mirar CI del commit anterior antes de
avanzar `main`**, o un rojo queda debajo del trabajo siguiente.

`eng/verify.ps1` sigue siendo la herramienta cuando hace falta la respuesta completa aquí y ahora —
un cambio del empaquetado, o una duda que CI tardaría en contestar—. Deja de ser un peaje por commit.

Medir antes de corregir. «Funciona» no es evidencia; un número lo es. La evidencia vive en
`docs/evidence/`, y la de auditorías se acumula en `docs/evidence/stable/`.

## Arquitectura, en una pantalla

Cinco capas, dependencias hacia dentro:

- `Domain` — políticas puras, sin E/S. Casi todas las **decisiones de seguridad** viven aquí:
  `RenamePolicy`, `UpdatePolicy`, `DiagnosticsAllowlist`, `MediaFileExtensions`.
- `Application` — casos de uso y puertos (`IMetadataProvider`, `IUpdateSource`, `ISettingsStore`).
- `Infrastructure` — los adaptadores, y por tanto **toda la superficie de ataque real**: SQLite,
  sistema de archivos, LibVLC, TMDB, actualizador, backup/ZIP.
- `Presentation` — Avalonia (AXAML y ViewModels), sin dependencias de Windows.
- `Windows` — el anfitrión: `Program.cs` y `CompositionRoot.cs`, único sitio con `Process.Start` y
  con la construcción de `HttpClient`.

## Las cinco reglas que este repositorio hace cumplir con pruebas

No son estilo: hay una puerta que falla si las rompes.

1. **Licencia por archivo.** Todo fuente nuevo lleva `SPDX-License-Identifier: GPL-3.0-or-later`. Lo
   exige `IDE0073` desde `.editorconfig`, así que lo caza `dotnet format`.
2. **Red declarada.** Ninguna conexión fuera de `NetworkPurposeRegistry`. Una prueba recorre `src/`
   buscando hosts no declarados y falla; otra levanta un proceso hijo y escucha si abre algo.
3. **Diagnóstico por lista blanca.** `DiagnosticsAllowlist` es una lista **cerrada** de campos. No se
   filtra lo malo, se permite lo bueno: un filtro tiene que imaginar de antemano cada cosa que puede
   salir mal.
4. **Bilingüismo.** Cadenas visibles y documentos públicos, en los dos idiomas.
   `BilingualHeadingTests` compara la estructura de ambos.
5. **Nada personal en el árbol.** Ni rutas de una máquina concreta, ni nombres de la biblioteca de
   nadie. `RepositoryPrivacyTests` lo mide.

## El defecto característico de este proyecto

**Registrado y nunca alimentado**: un servicio que se registra en el contenedor y que nada resuelve,
o una vista que se construye y a la que nadie llega. Una auditoría encontró 32 de estos de golpe. Hay
pruebas de arquitectura que exigen que cada servicio registrado tenga al menos una resolución fuera
de su propio registro. Si añades un registro, añade también quien lo consume, y compruébalo.

## Trampas conocidas

- **LibVLC** decodifica en proceso y con código nativo: es el mayor riesgo residual y está asumido y
  documentado. No lo empeores pasándole rutas sin filtrar por la lista de extensiones aprobadas.
- **El actualizador** verifica en un orden que importa: firma minisign sobre las notas **antes** de
  extraer el hash, allowlist de host en **cada** salto de redirección, y el archivo vive como
  `.partial` hasta que su hash y su tamaño coinciden. No reordenes esos pasos.
- **La caché de TMDB** tiene un techo duro de retención de 180 días porque sus términos lo exigen. No
  lo subas.
- Los `.axaml` admiten un comentario XML antes del elemento raíz, pero **no** una declaración
  `<?xml?>` después.

## Qué no se hace aquí

Servidor, cuentas, streaming, telemetría, sincronización en la nube. La
[hoja de ruta](docs/roadmap/README.es.md) lo dice con nombres. Una propuesta en esa dirección se
rechaza aunque esté bien implementada.
