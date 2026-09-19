# Dónde retomar — 2026-09-19

> Se **sobrescribe** en cada cierre y no pasa de 80 líneas ni de 6 KB por idioma; lo mide
> `eng/check-handoff.ps1`. La historia hasta el 2026-09-19 está congelada en
> [NEXT-SESSION-HISTORY.es.md](NEXT-SESSION-HISTORY.es.md). **El árbol manda sobre este documento**:
> `git log --oneline -1`, `git log --oneline -1 main` y `gh run list --limit 3` antes que nada.

## Estado

El repositorio adopta el sistema común de trabajo de la casa en la rama **local**
`codex/adopt-ap-smart-tech`, dos commits por delante de la rama de trabajo. **Sin push, sin PR y sin
fusionar**: el diff local espera la revisión del propietario. `main` y la rama de trabajo no se
tocaron y siguen como las dejó el cierre anterior, con su CI en verde.

## Lo que se hizo

· **Manifiesto** (`.claude/project-manifest.json`): backlog medido, nueve fases del cierre con su
  comando, recibo de CI y doce puertas con su caso que suena y su caso que calla.
· **`ENG-033`**: la marca del cierre vive en el directorio común de git y la miran las dos puertas
  del plugin. El guardián de push propio se retiró en el mismo commit, tras pasar su batería.
· **`ENG-034`**: `.gitignore` excluye el ajuste local, medido con la configuración global apagada.
· **`ENG-035`**: el relevo se sobrescribe, con tope y paridad; la historia, en `NEXT-SESSION-HISTORY`.
· **`ENG-036`**: la variable de las herramientas compartidas vive en el ajuste local.
· **`ENG-037`**: el acta mide las comprobaciones de IT por el fichero que dejan, no por el texto.
· Evidencia: `docs/evidence/stable/audit-adopt-common-system.md`.

## Las trampas medidas

· Congelar un documento con `git mv` y escribir otro en su sitio no es un renombre para git: el
  filtro de privacidad veía 16.000 líneas nuevas. Con `--find-copies-harder`, catorce.
· El filtro común toma las dos barras invertidas de una expresión regular por una ruta de red: el
  manifiesto suena en cada cierre que lo toque.

## Lo que espera al propietario

· Revisar el diff local de la rama de adopción y decidir si se fusiona.
· Retirar de la configuración global de git el patrón muerto escrito con barra invertida.
· Siete hallazgos del sistema común, para la sesión de IT; están en la evidencia.

## Lo pendiente no está aquí

Se lee con `pwsh -NoProfile -File eng/list-pending.ps1` y en [TAREAS.md](TAREAS.md).
