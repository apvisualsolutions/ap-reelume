# Dónde retomar — 2026-09-25 (cierre de la noche)

> Se **sobrescribe** en cada cierre y no pasa de 80 líneas ni de 6 KB por idioma; lo mide
> `eng/check-handoff.ps1`. La historia hasta el 2026-09-19 está congelada en
> [NEXT-SESSION-HISTORY.es.md](NEXT-SESSION-HISTORY.es.md). **El árbol manda sobre este documento**:
> `git log --oneline -1`, `git log --oneline -1 main` y `gh run list --limit 3` antes que nada.

## Estado

`main` y la rama quedaron en el mismo commit, el último con CI verde, leído por el vigía y por
`gh run list --commit`. Se comprueba con `git log --oneline -1 main`; el SHA no se escribe aquí. Por
delante sólo va este relevo, que es documentación y cuyo CI no se esperó: hay que mirarlo antes de
avanzar `main`.

El run del commit de `ENG-022` salió **rojo por una prueba del paseo que no era suya**: su único
cambio en `src/` eran comentarios, la prueba pasa tres de tres aquí y pasó en el run siguiente. Está
anotado como tercera aparición de `ENG-026`, y ese run siguiente salió verde entero.

## Lo que se hizo

· **`ENG-022`, cerrada como decidida y sin construir.** Siete familias medidas contra la verdad
  sintética, con un arnés que reproduce exactas las cifras del reproductor. El mejor candidato, un
  núcleo orientado a lo largo del canto, quita el escalón (0 contra 16) con la misma rampa, pero se
  queda en 34,9 % contra 35,7 % y cuesta seis veces más por software. `EdgeDirectedUpscaleCandidateTests`
  vigila la decisión; `gate-auditor` encontró una banda demasiado ancha, ya estrechada.
· **Cayó una afirmación escrita**: no había un techo del 13 % sin escalón. Corregida en el código, en
  la evidencia de `PLY-016` y en el backlog; la evidencia nueva se enlaza desde la matriz.
· Nacen **`ENG-052`** y **`ENG-053`** (un rojo de los pósters bajo cobertura, sin mensaje capturado), y `ENG-026` gana su tercera aparición.

## Las trampas medidas

· **Acotar a muestras ya remuestreadas no quita el timbre**: el anillo ya está dentro del rango.
  Hay que acotar a los texeles que dio el decodificador.
· **Un cero de variación es el instrumento**: el primer barrido dio cifras idénticas con tres fuerzas
  porque una sustitución de texto no casó por los finales de línea y el shader no leía el parámetro.
· **`gh run view --log-failed` no devuelve nada mientras el run sigue**: el flujo tiene un solo
  trabajo, así que el fallo de un paso sólo se lee al terminar.
· **Un mensaje de commit no se pasa por tubería desde PowerShell**: `git commit -F -` con un
  here-string lo tomó como ruta. Se escribe a un archivo del scratchpad.

## Lo primero de la sesión siguiente

· Mirar el CI de este relevo y, si está verde, avanzar `main`.
· La primera fila tomable de `TAREAS.md`: **`ENG-023`**, el difuminado de la curva de tono. Pide una
  decisión técnica antes del código: qué pasa cuando el reescalado está apagado, porque el difuminado
  tiene que ir después del escalado y hoy sólo el shader corre ahí.

## Lo que espera al propietario

· **Probar a mano el cierre tras ver un vídeo** (`ENG-020`): el código de salida 82 no sale de este
  árbol y sólo lo contesta el anfitrión real.
· **`ENG-049`** — el vídeo cambia de tamaño al aparecer y ocultarse los controles; se decide viéndolo.
· **Probar a mano `ENG-018`**, **`ENG-002`** (el Narrador) y **`ENG-005`** (ventana de diagnóstico).
· Bloqueado por algo que no es código: **`PRD-002`** (certificado de firma) y **`PRD-003`** (ARM64).

## Lo pendiente no está aquí

Lo contesta `pwsh -NoProfile -File eng/list-pending.ps1`: **24 abiertos de 75**. El alcance vive en
`FEATURES.md`; las faenas, puertas, deuda y preguntas sin medir en `TAREAS.md`, la más vieja arriba.
Hoy se cerró `ENG-022` y entraron `ENG-052` y `ENG-053`.
