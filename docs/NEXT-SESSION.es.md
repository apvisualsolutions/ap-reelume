# Dónde retomar — 2026-09-19

> Se **sobrescribe** en cada cierre y no pasa de 80 líneas ni de 6 KB por idioma; lo mide
> `eng/check-handoff.ps1`. La historia hasta el 2026-09-19 está congelada en
> [NEXT-SESSION-HISTORY.es.md](NEXT-SESSION-HISTORY.es.md). **El árbol manda sobre este documento**:
> `git log --oneline -1`, `git log --oneline -1 main` y `gh run list --limit 3` antes que nada.

## Estado

`main` y `codex/ap-reelume-mvp-x64` quedaron en el mismo commit, con su CI leído en verde. Este
relevo va un commit por delante, por ser sólo documentación, y su CI no se espera.

## Lo que se hizo

· **El repositorio adoptó el sistema común de trabajo de la casa** (piloto de la sesión de IT). El
  manifiesto está en `.claude/project-manifest.json`, con doce puertas probadas en sus dos casos.
· Cerradas `ENG-033` a `ENG-037`: la marca del cierre, en el directorio común de git; el ajuste local
  lo ignora `.gitignore`; el relevo se sobrescribe con tope; la variable de las herramientas
  compartidas vive en el ajuste local; y el acta mide por efecto.
· Se retiró el hook propio `pre-push-closing.sh`. Lo sustituyen las dos puertas del plugin y la fila
  del paso 0 del acta. **Este cierre es el primero con el sistema común.**
· Evidencia: `docs/evidence/stable/audit-adopt-common-system.md`.

## Las trampas medidas

· Congelar un documento con `git mv` y escribir otro en su sitio no es un renombre para git: el
  filtro de privacidad veía 16.000 líneas nuevas. Con `--find-copies-harder`, catorce.
· El filtro común toma las dos barras invertidas de una expresión regular por una ruta de red: el
  manifiesto suena en cada cierre que lo toque. Escribirlas en prosa también lo hace sonar.

## Lo primero de la sesión siguiente

· **El auditor de puertas** sobre las pruebas nuevas de la adopción: `HandoffLimitsTests`, la
  batería del acta y `gate-probe.ps1`. No se lanzó antes de cerrar.
· Luego `docs/TAREAS.md`, la primera abierta que no esté parada.

## Lo que espera al propietario

· Siete hallazgos del sistema común, en manos de la sesión de IT, que los recibió y leyó.
· `ENG-002` (una sesión con el Narrador) y lo demás suyo, como estaba en `docs/TAREAS.md`.

## Lo pendiente no está aquí

Se lee con `pwsh -NoProfile -File eng/list-pending.ps1` y en [TAREAS.md](TAREAS.md).
