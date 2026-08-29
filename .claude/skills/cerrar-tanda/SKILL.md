---
name: cerrar-tanda
description: Cierra una tanda de trabajo siguiendo el ciclo de CLAUDE.md — puertas de las suites afectadas, un commit, push solo a la rama, CI verifica, y fast-forward a main solo con la conclusion leida. Usar cuando el trabajo esta terminado y hay que publicarlo.
disable-model-invocation: true
---

# Cerrar una tanda

El ciclo de `CLAUDE.md`, ejecutado en orden. **Cada paso tiene un fallo que ya ha ocurrido**, y por eso
está aquí en vez de en la memoria de alguien.

## 1. Las puertas, solo las suites afectadas

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet format --verify-no-changes --severity warn
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
dotnet test <suite> -c Release -m:1 --settings eng/test.runsettings
pwsh -NoProfile -File eng/verify-docs.ps1
```

**El SDK no está en el `PATH` y `dotnet` falla desde Git Bash**: estas órdenes van por PowerShell.

**«Suites afectadas» es quién LEE el archivo, no dónde vive.** Tocar el shell rompió una vez una
obligación de TMDB en `IntegrationTests`. Si has tocado una vista: `UiTests` y `AccessibilityTests`.
Si has tocado `docs/`: `DocumentationTests`. Si has tocado `eng/` o el manifiesto: también
`ArchitectureTests`.

**`PackagingTests` está roja en esta máquina y verde en CI** — artefactos caducados, medido con
`git stash`. No la persigas.

## 2. Un commit, y uno solo

**Agrupa los remates.** Dos commits empujados seguidos ponen dos runs solapados y el paso `Verify`
pasa de 33 a 55 minutos; el 2026-08-29 costó una hora por no esperar. Si ya hay un run corriendo,
**commitea en local y no publiques** hasta conocer su conclusión: si falla, su corrección va en el
mismo push.

## 3. Push solo a la rama

```bash
git push origin codex/ap-reelume-mvp-x64
```

`main` no dispara el flujo: recibe el mismo SHA por fast-forward, y un check pertenece al commit.

## 4. Vigilar CI con el guion, nunca con un bucle a mano

```powershell
pwsh -NoProfile -File eng/watch-ci.ps1 -Sha <sha>
```

Cubre los cinco desenlaces. Un bucle escrito en el momento pregunta por `status == "completed"` y
**calla en todo lo demás** — un push que no disparó el flujo, un `gh` con la sesión caducada, un run
encolado. Un vigía callado es indistinguible de un run que sigue. Un run tarda **55-80 minutos**.

## 5. Fast-forward solo con la conclusión leída

```bash
gh run view <id> --json conclusion   # SIEMPRE, antes de mover la referencia
git checkout main && git merge --ff-only <sha> && git push origin main
git checkout codex/ap-reelume-mvp-x64
```

**Este es el paso que `CLAUDE.md` señala como el riesgo principal**: sin leer la conclusión, un rojo
queda debajo del trabajo siguiente.

Las tres cifras que debe dar el verde:

```
Coverage gate: N short of 96/96, ratchet N, N measured under the bar   <- los dos numeros IGUALES
Coverage gate: 0 new file(s) and N watched file(s) are where they have to be
The walk: N declared ...; N pressed, 20 pending                        <- el trinquete NO sube
```

## 6. Cerrar los documentos

Changelog **en los dos idiomas**, `docs/NEXT-SESSION.{es,en}.md` con el SHA real, y evidencia en
`docs/evidence/stable/` si la tanda midió algo. Una evidencia nueva se enlaza **también** desde
`docs/FEATURES.md` y desde `docs/evidence/mvp/verification-manifest.json`, o `EvidenceLinkTests`
falla con «matrix has N, manifest has N-1».

**Antes de crear un archivo de evidencia, comprueba que el nombre no existe ya.** El 2026-08-29 se
sobrescribió uno de agosto por reutilizar su nombre.
