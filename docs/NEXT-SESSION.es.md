# Dónde retomar — 2026-09-25 (cierre de la tarde)

> Se **sobrescribe** en cada cierre y no pasa de 80 líneas ni de 6 KB por idioma; lo mide
> `eng/check-handoff.ps1`. La historia hasta el 2026-09-19 está congelada en
> [NEXT-SESSION-HISTORY.es.md](NEXT-SESSION-HISTORY.es.md). **El árbol manda sobre este documento**:
> `git log --oneline -1`, `git log --oneline -1 main` y `gh run list --limit 3` antes que nada.

## Estado

`main` y la rama quedaron en el mismo commit, el último con CI verde, leído dos veces: por el vigía y
por `gh run list --commit`. Se comprueba con `git log --oneline -1 main`; el SHA no se escribe aquí.
Por delante sólo va este relevo, que es documentación y cuyo CI no se esperó: hay que mirarlo antes
de avanzar `main`.

Los dos runs anteriores de hoy salieron **rojos sólo por la cobertura**, y por una mejora: pasaron las
once suites y la puerta pidió subir dos suelos. El tercero, con los suelos copiados del artefacto,
salió verde: 184 de 184 en la deuda y 23 pendientes en el paseo.

## Lo que se hizo

· **`ENG-020`**: cerrar la aplicación después de ver un vídeo ya no termina en una excepción. El
  anfitrión suelta el icono de la bandeja antes de la primera espera del cierre, y liberarlo desde
  otro hilo lo encarga al suyo en vez de lanzar. Dos rojos archivados y dos mutantes muertos.
· **La auditoría de las pruebas de `ENG-018`**: `gate-auditor` encontró dos puertas ciegas y cuatro
  débiles. Las seis están corregidas y cada una mata el mutante que antes la burlaba. La peor era la
  escena de reloj del paseo, que pasaba con el reloj desconectado.
· Suben dos suelos de cobertura: la bandeja a 97/64 y el montaje de la ventana a 90/66.
· Nace **`ENG-051`**.

## Las trampas medidas

· **El cierre sólo cambia de hilo después de reproducir algo**: el reproductor espera a que reposen
  sus medios, y ninguna prueba cedía. La prueba del anfitrión provoca esa espera y exige que ocurra.
· **`isolation: worktree` no funciona con el repositorio en el NAS**: git la rechaza por propietario.
  La copia se hace a mano en el scratchpad y se usa con `git -c safe.directory=*`, sin tocar la
  configuración global. Detalle en el cajón del proyecto.
· **En la herramienta Bash, `dotnet` no encuentra el SDK**: un bucle de mutantes dio cinco salidas
  vacías. Las pruebas se lanzan desde PowerShell.
· **La previsualización de cobertura la tumba `ENG-051`**, así que no anunció las dos mejoras; las
  confirmó CI con un rojo.

## Lo primero de la sesión siguiente

· Mirar el CI de este relevo y, si está verde, avanzar `main`.
· La primera fila tomable de `TAREAS.md`: **`ENG-022`**, el reescalador guiado por bordes. Es diseño:
  medir las opciones y su coste con `UpscaleCostPolicy` antes de escribir el shader.

## Lo que espera al propietario

· **Probar a mano el cierre tras ver un vídeo** (`ENG-020`): el código de salida 82 no sale de este
  árbol y sólo lo contesta el anfitrión real.
· **`ENG-049`** — el vídeo cambia de tamaño al aparecer y ocultarse los controles; se decide viéndolo.
· **Probar a mano `ENG-018`**, **`ENG-002`** (el Narrador) y **`ENG-005`** (ventana de diagnóstico).
· Bloqueado por algo que no es código: **`PRD-002`** (certificado de firma) y **`PRD-003`** (ARM64).

## Lo pendiente no está aquí

Lo contesta `pwsh -NoProfile -File eng/list-pending.ps1`: **24 abiertos de 75**. El alcance vive en
`FEATURES.md`; las faenas, puertas, deuda y preguntas sin medir en `TAREAS.md`, la más vieja arriba.
Hoy se cerró `ENG-020` y entró `ENG-051`.
