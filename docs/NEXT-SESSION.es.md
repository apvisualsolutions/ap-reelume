# Dónde retomar — 2026-09-20 (cierre, segunda tanda)

> Se **sobrescribe** en cada cierre y no pasa de 80 líneas ni de 6 KB por idioma; lo mide
> `eng/check-handoff.ps1`. La historia hasta el 2026-09-19 está congelada en
> [NEXT-SESSION-HISTORY.es.md](NEXT-SESSION-HISTORY.es.md). **El árbol manda sobre este documento**:
> `git log --oneline -1`, `git log --oneline -1 main` y `gh run list --limit 3` antes que nada.

## Estado

`main` y la rama quedaron al día, en el mismo commit. Se lee con `git log --oneline -1 main` — el
SHA no se escribe aquí a propósito, porque el commit que lo escribe lo cambia.

Cuatro runs hoy, **los cuatro verdes**, con cada conclusión leída dos veces: por el vigía y por
`gh run list --commit`. Cada fast-forward se hizo con la conclusión leída, nunca sobre una suposición.

## Lo que se hizo

· **El rojo heredado está cerrado.** El suelo de `CompositionRoot.cs` pasó de 90/64 a 90/65, copiado
  del artefacto `coverage-debt` del run que correspondía a HEAD. Una línea de diff; el trinquete
  sigue en 185.
· **`ENG-015`**: la guarda del trinquete del paseo ya deniega lo que toca una fila y deja pasar lo
  que sólo toca un comentario `##`. Su batería queda versionada al lado.
· **`ENG-016`**: la previsualización de cobertura ve los ficheros nuevos preparados y sin seguir,
  medido por efecto contra un repositorio de mentira.
· **`ENG-045`** nació y se cerró el mismo día: vive en IT como regla R12, no aquí.
· `CLAUDE.md` puesta al día con las tres.

## Las trampas medidas

· **El relevo se equivocaba en dos cosas, y el árbol ganó las dos.** `main` estaba en `ENG-044` y no
  en `ENG-042`; y los runs rojos eran **dos**, no uno, con el artefacto bueno en el segundo — el run
  cuyo árbol coincidía con HEAD.
· **`grep -c $'\r'` volvió a mentir**, en los dos lados de una comparación, igual que el 2026-08-29.
  Lo cazó un `diff` normal diciendo `1,402c1,402` y una diferencia de tamaño de 402 bytes exactos.
  Se cuenta con `tr -cd '\r' | wc -c`. Es ya la regla R12 del guardián de IT.
· **`git log -S` no ve un cambio de cifra** — cuenta apariciones de una cadena, y la cadena sigue
  ahí. Contesta `-G`. La historia entera de un trinquete se leía como «nunca se tocó».
· **Un mutante salió idéntico al original** y dio 12 de 12 sin medir nada, porque el patrón del `sed`
  no casó. El mutante se compara con el original antes de creérselo.

## Lo primero de la sesión siguiente

· **`gate-auditor` NO se lanzó sobre las pruebas que añadió `ENG-016`.** Es el primer paso, en una
  copia aislada, antes de trabajo nuevo — y su copia pone roja `EvidenceLinkTests` mientras exista.
· **Dos comprobaciones que sólo puede hacer una sesión NUEVA**: si llegan por fin las herramientas
  del cajón de memoria de este proyecto —IT lo guardaba bajo tres formas de ruta y sólo había
  reenganchado una— y el marcador de cierre más el doctor contra el plugin **0.10.1**, publicado hoy.
  Esta sesión corrió con la 0.10.0 y no podía ver ninguna de las dos.
· Después, `ENG-017`, la primera fila tomable: nada comprueba que el escalador compile su shader una
  sola vez por película, y borrar la guarda deja 1.446 pruebas en verde.

## Lo que espera al propietario

· **`ENG-002`** — una sesión con el lector de pantalla real, para saber si lee peor los encabezados
  en mayúsculas. Nadie lo contesta de memoria, y decide si se gasta en un mecanismo que una decisión
  anterior descartó por coste.
· **`ENG-005`** — un ejecutable de diagnóstico pequeño que **abre una ventana**, así que se pide
  cuando no esté trabajando. Hoy nada del árbol monta el anfitrión real de Windows.
· Bloqueado por algo que no es código: **`PRD-002`** pide el certificado comercial de firma y
  **`PRD-003`** una máquina ARM64, que CI presta gratis pero no contesta entera desde un anfitrión x64.

## Lo pendiente no está aquí

Lo contesta `pwsh -NoProfile -File eng/list-pending.ps1`: **24 abiertos de 75**, 21 de ellos trabajo
y 3 decisiones en pie de no construir algo. El alcance vive en `FEATURES.md`; las faenas, puertas,
deuda y preguntas sin medir en `TAREAS.md`, la más vieja arriba — hoy entró `ENG-046`.
