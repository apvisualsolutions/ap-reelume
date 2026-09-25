# Dónde retomar — 2026-09-25 (cierre)

> Se **sobrescribe** en cada cierre y no pasa de 80 líneas ni de 6 KB por idioma; lo mide
> `eng/check-handoff.ps1`. La historia hasta el 2026-09-19 está congelada en
> [NEXT-SESSION-HISTORY.es.md](NEXT-SESSION-HISTORY.es.md). **El árbol manda sobre este documento**:
> `git log --oneline -1`, `git log --oneline -1 main` y `gh run list --limit 3` antes que nada.

## Estado

`main` y la rama quedaron al día, en el mismo commit. Se lee con `git log --oneline -1 main` — el
SHA no se escribe aquí a propósito, porque el commit que lo escribe lo cambia.

Dos runs hoy: el primero **rojo** por cobertura y el segundo **verde**, leído dos veces —por el vigía
y por `gh run list --commit`—. El fast-forward se hizo sobre el verde, con 184/184 en la deuda y 23
pendientes en el paseo.

## Lo que se hizo

· **`ENG-018`**: los controles del reproductor se van solos a los 3 s mientras la película avanza,
  el puntero con ellos, un clic en la imagen pausa, la rueda mueve el volumen y Esc retrocede una
  capa. La decisión que lo impedía se reabrió en `ADR-0014`, con el reloj como puerto y el paseo en
  infinito salvo una escena que lo baja a 200 ms. Doce mutantes muertos.
· **`ShellView.axaml.cs` sale de la deuda de cobertura** (100/97) y el trinquete baja a 184.
· `ENG-002` y `ENG-005` pasan a `DEL PROPIETARIO`: el relevo anterior saltaba la regla del orden, y
  `ENG-018` era la más vieja tomable.

## Las trampas medidas

· **Un gesto nuevo convierte la imagen en un mando para el paseo**: su «clic al lado» la pausaba y
  tumbó cinco escenas. Lo resuelve una prueba de impacto con dos matices, escritos en el cajón.
· **Esc tunelizado cerraba el engranaje al cerrar un desplegable.** La corrección por el origen del
  evento no servía: la tecla nace en lo que tiene el foco. La prueba buena la lanza sobre el reproductor.
· **La previsualización de cobertura no ve un archivo que estaba en el listón y cae**: así llegó el
  rojo de `PlayerView.axaml.cs` a CI (`ENG-050`).
· Un mutante de condición constante no compila aquí (`CS0162`); un rojo de compilación no es un
  mutante muerto.

## Lo primero de la sesión siguiente

· **`gate-auditor` NO se lanzó sobre las pruebas de esta tanda** (`ChromeIdleTests`,
  `ShellEscapeTests`, `ShellViewEdgeTests`, las nuevas de `PlayerViewInputTests` y la escena del
  paseo). Primer paso, en una copia aislada; su copia pone roja `EvidenceLinkTests` mientras exista.
· Después, la primera fila tomable de `TAREAS.md`: **`ENG-020`**, cerrar la aplicación lanza una
  excepción y sale con código 82.

## Lo que espera al propietario

· **`ENG-049`** — con la película en la ventana, el vídeo cambia de tamaño cada vez que los
  controles aparecen o se van. Se mide con él delante antes de decidir si deben flotar.
· **Probar a mano `ENG-018`**: nadie abrió la aplicación para no quitarle la pantalla.
· **`ENG-002`** (una sesión con el Narrador) y **`ENG-005`** (una ventana de diagnóstico cuando no
  esté trabajando).
· Bloqueado por algo que no es código: **`PRD-002`** (certificado comercial de firma) y **`PRD-003`**
  (ARM64 entero).

## Lo pendiente no está aquí

Lo contesta `pwsh -NoProfile -File eng/list-pending.ps1`: **24 abiertos de 75**, 21 de ellos trabajo
y 3 decisiones en pie de no construir algo. El alcance vive en `FEATURES.md`; las faenas, puertas,
deuda y preguntas sin medir en `TAREAS.md`, la más vieja arriba — hoy entraron `ENG-049` y `ENG-050`.
