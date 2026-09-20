# Dónde retomar — 2026-09-20

> Se **sobrescribe** en cada cierre y no pasa de 80 líneas ni de 6 KB por idioma; lo mide
> `eng/check-handoff.ps1`. La historia hasta el 2026-09-19 está congelada en
> [NEXT-SESSION-HISTORY.es.md](NEXT-SESSION-HISTORY.es.md). **El árbol manda sobre este documento**:
> `git log --oneline -1`, `git log --oneline -1 main` y `gh run list --limit 3` antes que nada.

## Estado

`main` y `codex/ap-reelume-mvp-x64` quedaron al día, y cada fast-forward se hizo con su CI leído en
verde. Este relevo va un commit por delante, por ser sólo documentación, y su CI no se espera.

## Lo que se hizo

· **`LIB-021` entero, y con él `ENG-003`**, abierta desde el 2026-09-05. El orden de portadas se
  cambia en una sección propia de Ajustes —dos botones sobre una lista, con su «Restaurar valores por
  defecto»— y un título puede saltárselo desde su editor, con una fila de cuatro opciones.
· **Migración 25** (`cover_order`), que guarda **el orden entero** y no el origen que gana: así mover
  el orden general más tarde no cambia lo que ese título tenía dicho.
· `MetadataFieldChanges.CoverOrder` lleva **dos centinelas** —`null` no toca la excepción, la lista
  vacía la quita—, que es el hueco que `PersonalCover` todavía tiene.
· Trinquete de deuda **185 → 186** por la vista nueva. El suelo de `MetadataEditorViewModel.cs`
  **sube** a 95 por mejora, y el de `CatalogRepository.cs` **no bajó**: se cubrió la rama que la
  columna nueva traía.
· Enmienda al `ADR-0009` y evidencia `LIB021-cover-order-setting.md`.

## Las trampas medidas

· **Una migración mueve CINCO afirmaciones del esquema, no tres.** Las tres escritas —conteo, máximo
  y lista de nombres— dejaron una cuarta prueba roja: `Migration_is_idempotent_...` cuenta **una copia
  de seguridad por migración** y vuelve a contar el historial.
· **Un desplegable es un control que el paseo autónomo no puede pulsar**, porque nada dentro de un
  popup lo alcanza, y su trinquete sólo encoge. Siete puertas rechazaron ese control desde siete
  sitios distintos y **ninguna hubo que aflojarla**; la forma buena es la fila de opciones con radios
  que la lista de dispositivos de audio ya usaba.
· **Un `Test Case Cleanup Failure` de `UiTests` no es del código**: es el arnés. Tumbó un commit que
  sólo tocaba los dos relevos. Tercera aparición, ya con ficha (`ENG-040`).

## Lo primero de la sesión siguiente

· `docs/TAREAS.md`, la primera abierta que no esté parada. `ENG-002` y `ENG-005` piden al propietario
  (un lector de pantalla real, y abrir una ventana), así que la primera tomable es **`ENG-009`**:
  correr `gate-auditor` en un worktree pone roja `EvidenceLinkTests`, que trata sus copias como
  documentos del proyecto. Hay que excluir `.claude/worktrees/` del barrido.
· **`ENG-041` antes de subir cobertura**: dos reglas escritas sobre cómo cuenta esa puerta se
  contradicen, y la fila dice cómo medirlo.

## Lo que espera al propietario

· La sesión de IT publica la **0.10.0** del sistema común; al avisar, actualizar el plugin, comprobar
  que responde, migrar su registro de métricas a JSON Lines y repetir sus comprobaciones. Los nombres
  de esas herramientas **no se escriben aquí**: este repositorio es público.
· `ENG-002` (una sesión con el Narrador), `ENG-005` (abrir una ventana cuando no esté trabajando) y
  `ENG-042` (si «complemento» es deliberado en los documentos legales del motor o es una desviación).

## Lo pendiente no está aquí

Se lee con `pwsh -NoProfile -File eng/list-pending.ps1` y en [TAREAS.md](TAREAS.md).
