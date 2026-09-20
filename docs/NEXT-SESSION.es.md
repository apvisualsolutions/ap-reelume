# Dónde retomar — 2026-09-20 (tarde)

> Se **sobrescribe** en cada cierre y no pasa de 80 líneas ni de 6 KB por idioma; lo mide
> `eng/check-handoff.ps1`. La historia hasta el 2026-09-19 está congelada en
> [NEXT-SESSION-HISTORY.es.md](NEXT-SESSION-HISTORY.es.md). **El árbol manda sobre este documento**:
> `git log --oneline -1`, `git log --oneline -1 main` y `gh run list --limit 3` antes que nada.

## Estado

`main` quedó al día con el commit de `ENG-009`, con su CI leído en verde antes del fast-forward.

**La rama va por delante con `ENG-044` empujado y su CI corriendo, sin leer.** Se empujó a la rama y
no a `main`, que es donde un rojo no molesta. **Y se espera que ese run salga rojo**, con la causa
exacta ya medida aquí: ver la primera viñeta de abajo. `main` no se mueve hasta leerlo.

## Lo que se hizo

· **`ENG-009` cerrada.** El barrido de documentos deja de leer las copias de otras sesiones, así que
  correr `gate-auditor` en un worktree ya no pone roja `EvidenceLinkTests`. Era la **tercera** vez que
  se escribía esa exclusión a mano, con dos redacciones distintas; la regla vive ahora en
  `RepositoryLayout.IsInsideAnotherCheckout`, con sus dos lecciones dentro.
· **`ENG-043` cerrada por decisión del propietario**: lo publicado se acepta, sin renombrar y sin
  tocar el historial, coherente con `ENG-032`. El porqué está escrito en la fila para que no se
  reabra.
· **`ENG-044` abierta y medio construida** — nació midiendo `ENG-010`, que es su síntoma. Ver abajo.
· `LIB-003` bajada de `VERIFIED` a `IMPLEMENTED`, con su bloqueo declarado en el manifiesto.

## Las trampas medidas

· **`LIB-003` prometía vigilancia continua y no se encendía nunca.** Lo único que la activaba era
  `ScanPolicy.Continuous`, y **nada de `src/` lo asignaba**: las tres vías de alta dan
  `Startup | Manual` o `Manual`, y ninguna pantalla ofrece la elección. Control negativo hecho: el
  mismo grep sí encuentra las otras dos banderas.
· **Por qué ninguna puerta lo vio: las pruebas se dan a sí mismas la bandera.** Doce sitios en
  `tests/` la ponen y cero en `src/`. Todas miden que la vigilancia funciona **cuando está
  encendida**; ninguna que llegue a encenderse. La guarda que falta no es otra prueba del vigilante.
· **Una prueba que ya existía corrigió el diseño**, y es lo que más valió de la tanda.
  `A_manual_root_is_not_watched_behind_its_owners_back` obligó a que el ajuste alcance sólo a las
  raíces con `Startup`: `DeclareCourseFolder` da `Manual` a secas **a propósito**, porque el diálogo
  promete no tocar el resto del disco.
· **El vigía de CI exige el SHA de cuarenta caracteres.** Se armó una vez con un SHA inventado a
  partir del corto; `gh` contesta `[]` y se lee igual que «aún no hay run». Se resuelve con
  `git rev-parse HEAD`, nunca a mano.
· **Una fila nueva en `docs/TAREAS.md` va en su sitio por identificador, no al final de las hechas.**
  `TareasRegisterTests` lo exige y avisó.

## Lo primero de la sesión siguiente

· **Empujar el commit local de `ENG-044` y leer su CI.** Todas las puertas locales pasaron —formato,
  compilación, `Domain` 879, `Application` 393, `Architecture` 55, `Documentation` 118 e
  `Integration` 683—, pero sólo CI verifica de verdad. **Y ese run va a salir rojo con «1 improved»,
  medido aquí antes de empujar**: `RootWatchCoordinator.cs` sube de **96/89 a 97/90** porque las
  pruebas nuevas lo recorren. No es un defecto: se descarga el artefacto `coverage-debt` de ese run
  y se sube su fila en el mismo cambio, sin tocar el fichero a mano. Ningún archivo nuevo se queda
  corto.
· **Terminar `ENG-044`, que cierra también `ENG-010`**: que los dos mandos de Ajustes gobiernen el
  ajuste y se guarden, la guarda de ensamblado —una raíz creada por las vías reales acaba vigilada—,
  releer `audit-wp2-assembly.md`, y decidir el intervalo (el mando dice 30 y el código usa 15).
· **`ENG-041` antes de subir cobertura**: dos reglas escritas sobre cómo cuenta esa puerta se
  contradicen, y la fila dice cómo medirlo.

## Lo que espera al propietario

· **`ENG-042`**: la comprobación de lenguaje señala 31 sitios en ficheros vivos, **todos anteriores a
  esta tanda** —el único que introdujo se corrigió antes de commitear—. Hay que decidir si en los
  documentos legales del motor de vídeo el término del fabricante es deliberado antes del barrido.
· **`ENG-002`** (una sesión con el Narrador) y **`ENG-005`** (abrir una ventana cuando no esté
  trabajando).
· La sesión de IT publicará la versión nueva del sistema común; al avisar, actualizar y repetir sus
  comprobaciones, con los nombres que estén en el ajuste local.
· La sesión de IT migrará este repositorio al NAS cuando se le diga «listo para migrar». Medido hoy:
  **cero worktrees vivos**, y las dos carcasas vacías ya retiradas.

## Lo pendiente no está aquí

Se lee con `pwsh -NoProfile -File eng/list-pending.ps1` y en [TAREAS.md](TAREAS.md). Hoy: 25 abiertas
de 75.
