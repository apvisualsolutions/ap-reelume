---
name: cerrar-tanda
description: Cierra una tanda de trabajo entera — verificar que nada quedó roto, un commit, push solo a la rama, CI verifica, fast-forward a main con la conclusion leida, documentos en dos idiomas, decisiones abiertas cerradas, tareas de fondo al dia, y el prompt de la siguiente sesion listo para copiar. Usar cuando el trabajo esta terminado y hay que publicarlo y dejarlo recogido.
---

# Cerrar una tanda

El ciclo de `CLAUDE.md`, ejecutado en orden, y lo que hay que dejar recogido después. **Cada paso
tiene un fallo que ya ha ocurrido**, y por eso está aquí en vez de en la memoria de alguien.

**No se cierra a medias.** Los pasos 1 a 6 publican; los pasos 7 a 10 son los que hacen que la
sesión siguiente no empiece preguntando. Saltarse los segundos deja el trabajo hecho y el contexto
perdido, que es la forma cara de terminar.

> **Este archivo llevó `disable-model-invocation: true` y se quitó el 2026-08-31, midiendo lo que
> protegía: nada.** La idea era que mover `main` fuese siempre un acto explícito de una persona. Pero
> `main` se mueve con `git branch -f` y `git push`, que son órdenes normales — ese mismo día se movió
> **seis veces** sin pasar por aquí. Lo único que la marca impedía era **leer este documento**, así
> que el cierre se hizo sin la lista de trampas en vez de sin el poder de cerrar: el peor de los dos
> resultados. Una guarda que se declara y no guarda de nada es el defecto que este repositorio
> persigue con nombre propio. **Lo que de verdad protege el paso peligroso está en el paso 6**, que
> exige leer la conclusión antes de mover la referencia; y lo que limita a un agente son los permisos
> de herramientas, no una línea de frontmatter. Si alguien vuelve a ponerla, que sea midiendo qué
> impide.

## 1. Que nada quedó roto: las puertas, solo las suites afectadas

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
`git stash`. No la persigas… **salvo si la tanda toca el manifiesto**, y entonces es al revés: ya
estás pagando el ciclo de empaquetado, y esa suite es la única que mide lo que cambiaste. Con
`package-x64.ps1` + `verify-package.ps1 -Mode Verify` + `package-arm64.ps1` dio **194 de 194** el
2026-08-31. Los «30 rojos» son **artefactos ausentes**, no esta máquina — y uno de ellos llevaba
nueve días señalando un artefacto ARM64 anterior a un cambio del manifiesto.

**Y si la tanda subió cobertura, medir en local ANTES de empujar sale gratis y ahorra 45 minutos.**
La fusión de CI se reproduce aquí: `gh run download <id> -n test-results` y `reportgenerator` con los
argumentos de la puerta. **Lo que NO funciona es correr `eng/check-coverage.ps1` entero contra ese
artefacto**: sus nombres de archivo llegan sin el `src/` inicial, la puerta los busca con
`EndsWith('src/…')` y **ninguno casa**, así que anuncia «PASS (no instrumentable lines)» sobre los
430. Un falso verde. Se lee el `Cobertura.xml` fusionado a mano; medido el 2026-08-31, sus cifras
coincidieron **exactas** con las de CI.

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
encolado. Un vigía callado es indistinguible de un run que sigue.

**Un run tarda 49-57 minutos cuando va bien, y uno de siete llegó a 86.** Esta línea dijo «55-80» hasta el 2026-08-31, y después «42-53» hasta el 2026-09-03, copiada cada vez de una época
anterior y nunca vuelta a medir, mientras `CLAUDE.md` ya llevaba la cifra corregida sobre doce runs.
Es el defecto de la casa aplicado a un número: **una cifra que nadie vuelve a medir acaba
justificando la decisión equivocada.** Si vuelves a citarla, mídela.

## 5. Leer la conclusión — y saber cuáles de esos rojos NO son tuyos

```powershell
gh run view <id> --json conclusion   # SIEMPRE, antes de mover la referencia
gh run view <id> --log-failed        # y qué falló exactamente, sin suponer
```

**Tres rojos conocidos que no significan que tu trabajo esté mal**, y los tres han costado tiempo por
diagnosticarlos desde cero:

1. **«N improved» en la puerta de cobertura**, y **es un rojo que casi siempre se pudo evitar**. La
   puerta falla igual ante un suelo corto y ante uno largo, así que en cuanto un archivo mejora exige
   subir su suelo. La corrección es copiar `eng/coverage-debt.txt` del artefacto `coverage-debt` de un
   run, **nunca editar la lista a mano**.
   **Lo que evita la vuelta perdida es medirlo ANTES de empujar**: los suelos que van a subir se
   reproducen aquí y entran en el mismo commit, y el artefacto sirve para confirmar. Un archivo sube
   por dos vías y la segunda se olvida — porque le añades una rama cubierta, o porque **pruebas
   nuevas lo recorren de paso**. El 2026-08-31 se midió uno en 83,33 % y CI dijo 83, y aun así se
   empujó el rojo: el número estaba en la mano. **Sólo son dos vueltas de verdad** cuando lo que sube
   es uno de los siete archivos que dependen de hardware que el runner no tiene.
2. ~~**`MarkerEditorViewModel` pidiendo que le suban el suelo.**~~ **CERRADO el 2026-08-31 y ya no
   pasa**: el archivo está en **100/100** y fuera de la lista. Se deja escrito porque la forma del
   defecto vuelve, no el archivo. Oscilaba —79, 79, 79, 81, 79, y otra vez 81 tumbando un run que
   sólo tocaba un `.md`— porque unas ramas **las cubría sólo el paseo**, que llega a ese estado unos
   runs sí y otros no. **La regla que queda: ante una cifra que baila, mide QUÉ RAMA se cubre por
   accidente antes de tolerar el baile.** Se compara suite por suite —`UiTests` y
   `AccessibilityTests` por separado, rama a rama—, y la corrección es una **prueba**, nunca una
   banda de tolerancia. Y ojo: la segunda vez la respuesta fue **cero ramas del paseo**; lo que
   quedaba eran guardas que nada determinista tomaba y una propiedad pública que no leía nadie.
3. **Una prueba del paseo que falla sola.** Antes de llamarlo intermitencia, **compruébalo con un
   dato**: `git diff --name-only <sha-verde> <sha-rojo> -- src tests`. Si no cambió nada ahí, el
   mismo código dio dos resultados y es intermitencia; relanza. Si cambió algo, es tuyo. Y un
   reintento que pasa **no demuestra que no haya defecto**: si se repite, es una carrera real.

**`PerformanceTests` en el runner alquilado no bloquea** — el propio flujo archiva su fallo y lo dice
en el log.

## 6. Fast-forward solo con la conclusión leída

```bash
git merge-base --is-ancestor <main-actual> <sha>   # que es fast-forward de verdad
git branch -f main <sha> && git push origin main
```

`git branch -f` en vez de `git checkout main && git merge --ff-only`: no cambia de rama, no toca el
árbol de trabajo y no deja a nadie en `main` por descuido.

**`main` solo recibe el SHA que CI verificó.** Si tienes commits locales por delante de ese SHA, se
quedan en la rama esperando su propio verde. Este es el paso que `CLAUDE.md` señala como el riesgo
principal: sin leer la conclusión, un rojo queda debajo del trabajo siguiente.

Las tres cifras que debe dar el verde:

```
Coverage gate: N short of 96/96, ratchet N, N measured under the bar   <- los dos numeros IGUALES,
                                                                          y SIN "N improved"
Coverage gate: N new file(s) ... are where they have to be             <- lo que importa es la frase
                                                                          final, no que N sea 0
The walk: N declared ...; N pressed, 20 pending                        <- el trinquete NO sube
```

**La segunda cifra no tiene que ser 0.** Decía «0 new file(s)» aquí y es engañoso: el 2026-08-31 un
verde legítimo dijo **14**, porque la tanda añadía catorce archivos que llegaban al listón. Lo que se
lee es «are where they have to be».

**El hook de post-push suena también en el fast-forward** y pide un vigía. Es deliberado —
distinguirlo pedía adivinar la rama de destino, y una guarda que se equivoca callando es peor.

**Y desde el 2026-09-02 ya no es un falso positivo: armarlo ahí sirve.** Esta línea decía que lo
era, y el cambio del vigía la dejó falsa. `main` sigue sin disparar el flujo, pero `watch-ci.ps1`
busca por **commit** y no por rama, así que encuentra el run que la rama de trabajo ya produjo y
devuelve **su conclusión**. Medido con `3cdeeb3`, que llegó a `main` por fast-forward:
`gh run list --commit` contesta `success` en `codex/ap-reelume-mvp-x64`. Es una segunda lectura del
verde que autorizó el fast-forward, no un aviso que ignorar.

## 7. Cerrar los documentos

Changelog **en los dos idiomas**, `docs/NEXT-SESSION.{es,en}.md` con el SHA real, y evidencia en
`docs/evidence/stable/` si la tanda midió algo. Una evidencia nueva se enlaza **también** desde
`docs/FEATURES.md` y desde `docs/evidence/mvp/verification-manifest.json`, o `EvidenceLinkTests`
falla con «matrix has N, manifest has N-1».

**Antes de crear un archivo de evidencia, comprueba que el nombre no existe ya.** El 2026-08-29 se
sobrescribió uno de agosto por reutilizar su nombre.

**Y si la tanda cambió el estado de una funcionalidad, la matriz manda sobre la prosa.** El
2026-08-31 la hoja de ruta decía «44 verificados» y la matriz daba 43: la prosa contaba un
`IMPLEMENTED` como hecho. Se corrige la prosa, nunca al revés.

## 8. Cerrar las decisiones que quedaron abiertas

**Una decisión que vive solo en el chat es una que la sesión siguiente vuelve a discutir o, peor,
contradice sin enterarse.** Barre la tanda buscándolas y escribe cada una donde manda:

- **De diseño o alcance** → un ADR. Si cambia uno aceptado, va como **enmienda fechada** con su
  motivo, no reescribiendo la decisión original, y la decisión enmendada lleva un puntero a ella.
  **Lee el ADR entero antes de escribirla**: el 2026-08-31 la enmienda iba a decir que superseía la
  decisión 2 y resultó ser la 3, y que lo medido seguía siendo válido en vez de quedar descartado.
- **De prioridad o de publicación** → la [hoja de ruta](../../../docs/roadmap/README.es.md), en los
  dos idiomas.
- **Lo que es del propietario y no tuyo** —copia visible, dinero, hardware, alcance— se deja
  **nombrado como suyo** con una recomendación y listo para un sí o un no. No se decide por él, y
  tampoco se le devuelve la pregunta en crudo: se le da la opción recomendada y el motivo.

## 9. Poner al día las tareas de fondo

Las que creaste con `spawn_task` llevan **el estado del mundo cuando las escribiste**. Si la tanda
cerró una decisión que su prompt daba por abierta, ese prompt **le va a pedir al propietario algo ya
decidido**. Se reemplaza: `spawn_task` con el texto nuevo primero, y después `dismiss_task` con el
id viejo. El 2026-08-31 hubo que reemplazar la misma tarea dos veces por esto.

Y las que la tanda haya completado se retiran con `dismiss_task`, no se dejan colgando.

## 10. Preparar la siguiente sesión, y darle el prompt

`pwsh -NoProfile -File eng/list-pending.ps1` contesta qué queda abierto, por versión, separando lo
que es trabajo de lo que son decisiones en pie. Es el punto de partida del relevo, y no se escribe a
mano: el 2026-08-31 una lista hecha a ojo perdió **ocho filas en silencio** porque el patrón pedía
tres mayúsculas y `UX` tiene dos.

El relevo (`NEXT-SESSION`) tiene que llevar, además del trabajo hecho:

- **si `main` y la rama coinciden, y si no, por qué** — pero **NO el SHA**. Escribirlo es un bucle:
  el commit que lo escribe lo cambia, así que nace caduco. El 2026-08-31 este bloque mintió sobre él
  **dos veces en una tarde**, la segunda al «corregirlo» poniendo el SHA de entonces. Lo que se pone
  es cómo leerlo —`git log --oneline -1 main`— y lo que sí aguanta: que quedaron al día, y que cada
  fast-forward se hizo con CI en verde;
- **las decisiones tomadas y NO ejecutadas**, que es lo que nadie puede deducir del diff;
- **lo que está bloqueado por algo que no es código** —hardware, una firma, una respuesta de un
  tercero—, porque eso no se resuelve programando y conviene que se vea pronto;
- **las trampas medidas** que costaron tiempo esta vez.

**Y el prompt de la sesión siguiente se escribe EN EL CHAT, para copiar y pegar. Nunca como archivo
adjunto ni como ruta a un documento.** Debe abrir mandando leer el árbol —`git log --oneline -1` y
`gh run list --limit 3`— antes que ninguna otra cosa, porque **el árbol manda sobre el relevo**: un
relevo se escribe una vez y el árbol cambia después.
