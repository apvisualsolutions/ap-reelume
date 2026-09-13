# Tareas de ingeniería

Este es el registro canónico de **lo que la matriz de alcance no puede llevar**: herramientas,
puertas, deuda, defectos y preguntas sin medir. El alcance —lo que el programa hace para quien lo
usa— vive en [FEATURES.md](FEATURES.md) y **no se repite aquí**: cuando una tarea pertenece a una
fila de esa matriz, aquí sólo se la nombra.

Está en español y sin pareja inglesa a propósito: es el cuaderno de trabajo, no un documento que
alguien de fuera lea. La regla del bilingüismo es para lo que ve quien usa el programa.

## La regla del orden, y es la que gobierna este fichero

**La más vieja arriba. Lo nuevo se añade por abajo.** Se toma la primera de la lista que no esté
`PARADA`; si la de arriba lo está, se sigue leyendo hacia abajo.

**Por qué, dicho por el propietario el 2026-09-13**: quien lee esto —una persona o un agente— lee de
arriba hacia abajo y no llega al final. Con lo nuevo arriba, lo viejo no se hace nunca y la lista
crece hasta el fin de los días mientras entran tareas que tampoco se acaban.

**Los identificadores se reparten por fecha de nacimiento y no se reutilizan nunca**, así que los
huecos de la lista de abiertas son las que ya se cerraron y siguen abajo con su número. Una fila que
apareciera con un número menor que la de encima sería una tarea colada en la cabecera para que se
hiciera antes, y eso es lo que la guarda impide.

**La antigüedad es el valor por defecto, no una cárcel.** El propietario puede poner una tarea delante
de su turno, y entonces **la fila no se mueve de sitio** —la posición es la fecha de nacimiento, que es
lo que se puede comprobar— sino que cambia de estado a `ADELANTADA` y dice **desde cuándo y por qué**.
Decidido el 2026-09-13: sin eso, el orden le quitaría el volante a quien decide, y una prioridad que
vive fuera del registro es una que nadie cuenta — que es el agujero que este fichero vino a tapar. La
guarda exige el motivo, así que un adelanto silencioso no existe.

Lo hace cumplir `TareasRegisterTests`, y comprueba lo que una persona olvida: que las fechas y los
números van en orden ascendente hacia abajo, que cada fila lleva identificador, fecha y estado, que
ningún identificador se repite entre las dos listas, y que una fila `PARADA` nombra qué la para.

## Estados

| Estado | Significado |
| --- | --- |
| `ABIERTA` | Nadie la ha empezado. |
| `ADELANTADA` | El propietario la puso delante de su turno. **Va antes que cualquier `ABIERTA`**, y la fila dice desde cuándo y por qué. |
| `EN CURSO` | Alguien está en ella ahora. |
| `PARADA` | Hay un bloqueo nombrado. Se salta y se sigue leyendo. |
| `DEL PROPIETARIO` | No se resuelve programando: dinero, una credencial, un aparato o una decisión suya. |
| `POR COMPROBAR` | Puede que ya esté hecha; hay que medirlo antes de gastar en ella. |

## Abiertas

| Id | Nacida | Estado | Qué cambia | Detalle |
| --- | --- | --- | --- | --- |
| `ENG-001` | 2026-09-05 | `ABIERTA` | Tres pruebas de vídeo llevan meses saltándose en las dos máquinas, así que hay formatos que nadie comprueba. | CI instala el paquete reducido de `ffmpeg` y hay que pasarlo al completo. Toca los dos flujos. El propietario pidió expresamente que no se olvide. |
| `ENG-002` | 2026-09-05 | `ABIERTA` | Si un lector de pantalla lee peor los encabezados en mayúsculas, quien use el programa con uno lo nota. | Pregunta **sin medir**, y no la contesta nadie de memoria: se mide con un lector real antes de gastar en un mecanismo que una decisión anterior descartó por coste. Hasta entonces no es un defecto conocido, es una duda sin datos. |
| `ENG-003` | 2026-09-05 | `ABIERTA` | El orden de portadas que el `ADR-0009` decidió no existe, y **no tiene fila en la matriz**, así que hoy no lo cuenta nadie. | La tuya gana, luego la del proveedor, y si no hay ninguna se saca un fotograma. Se cambia en un ajuste general y se salta en un título concreto. Medido el 2026-09-13: cero apariciones de `CoverOrigin`, `CoverOrder` o `CoverPolicy` en `src/`. Lo primero es **darle su fila**, porque es alcance y no una faena. |
| `ENG-005` | 2026-09-12 | `ABIERTA` | Nada del árbol monta el anfitrión real de Windows, así que qué motor gráfico elige la aplicación que se distribuye no lo establece ninguna puerta. | El paseo «físico» también es headless. `PLY-016` lo necesitará el día que su cadena llegue a la tarjeta. Decidido que sea un ejecutable de diagnóstico pequeño; **abre una ventana**, así que se pide al propietario cuando no esté trabajando. |
| `ENG-009` | 2026-09-13 | `ABIERTA` | Usar la herramienta que el repositorio recomienda rompe una puerta. | Correr `gate-auditor` en un worktree pone roja `EvidenceLinkTests`, que trata sus copias como documentos del proyecto. Hay que excluir `.claude/worktrees/` del barrido. Costó una decisión de método el 2026-09-13: la auditoría se corrió sin worktree por esto. |
| `ENG-010` | 2026-09-13 | `ABIERTA` | Dos mandos de Ajustes no gobiernan nada: quien los mueva no cambia nada y nada se lo dice. | El grupo de escaneo tiene dos mandos que son campos sin almacén y sin ningún lector en `src/`. Se clasificaron fuera de la lista de grupos de opciones, así que el día que alguien los cablee nace un grupo que la puerta no ve. |
| `ENG-011` | 2026-09-13 | `ABIERTA` | La velocidad de reproducción se olvida al cerrar, y quien la cambie tiene que volver a ponerla cada vez. | `ControlPlayback` la guarda en memoria y no en la preferencia. Encontrado de paso al construir el engranaje. |
| `ENG-013` | 2026-09-13 | `ADELANTADA` | **Es lo que desbloquea publicar.** Adelantada el 2026-09-13 por el propietario: eligió construir primero el escalador y dejar esto para la tanda siguiente, así que va antes que las `ABIERTA` que tiene encima. El paquete lleva un componente contagioso dentro de un programa propietario, y eso es un incumplimiento. | Compilar LibVLC y sus dependencias sin `--enable-gpl`, en las dos arquitecturas, y mantener esa compilación. Es infraestructura permanente, no una renuncia de formatos: la biblioteca que descodifica es permisiva por defecto y ningún decodificador está entre las piezas contagiosas. `ADR-0013` §5. Habilita además el kit de vídeo RTX de NVIDIA, que corre dentro del proceso. |
| `ENG-014` | 2026-09-13 | `ABIERTA` | Tres afirmaciones de los documentos sobre esos complementos son falsas o incompletas, así que quien empiece `ENG-013` empieza con datos malos. | Medido en los binarios: `libswscale_plugin.dll` **también** lleva `--enable-gpl`, en x64 y en ARM64, y no lo nombra ningún documento — son dos y no uno. `libx26410b_plugin.dll` tiene **cero** ocurrencias de esa cadena, mientras cuatro documentos lo presentan como «el ejemplo más claro». Y la medición del 2026-09-13 **no dejó evidencia archivada**: ni comando, ni salida, ni los nombres de los «tres complementos de control» que la frase cita. |
| `ENG-015` | 2026-09-13 | `ABIERTA` | La cabecera de un trinquete se contradice con el trinquete, así que engaña a quien la lea. | `eng/walk-pending.txt` dice «the ratchet is 20» y «the twenty below» mientras `eng/check-walk-coverage.ps1` dice 23. El texto se quedó en la subida del 2026-09-02. Cuidado: hay un hook que rechaza escribir ese fichero, y `CLAUDE.md` escribe el número también. |
| `ENG-016` | 2026-09-13 | `ABIERTA` | La previsualización de suelos de cobertura da un falso silencio, y por ahí se cuela un rojo de CI. | `eng/preview-coverage-floors.ps1:204` busca archivos nuevos con `git diff --diff-filter=A`, que sólo nombra los que están **en un commit**. Medido el 2026-09-13: con tres ficheros nuevos sin commitear dijo «nada se queda corto» y dos de los tres estaban por debajo de 96/96. Hay que ensanchar la búsqueda, no estrecharla. |
| `ENG-017` | 2026-09-13 | `ABIERTA` | Nada comprueba que el escalador compile su shader una sola vez por película. | Borrar la guarda lo compila en cada fotograma —unas 173.000 en dos horas— y las 1.446 pruebas de interfaz siguen verdes. Lo encontró `gate-auditor`. Lo que necesita es una costura para contar las compilaciones, no un comentario más alto. |

## Lo que vive en la matriz de alcance, y por qué no está aquí

Estas siguen abiertas, pero tienen su fila con criterio y evidencia. **Aquí sólo se las nombra**, o
volveríamos a tener la misma tarea en dos sitios:

- **`PRD-006`** (`IMPLEMENTED`) — la paridad con el prototipo: los 42 defectos en el orden del
  propietario, la rejilla fluida, el ritmo vertical de la tarjeta y lo demás que se midió contra el
  prototipo. Se bajó de `VERIFIED` a `IMPLEMENTED` y le faltan comprobaciones.
- **`PLY-016`** (`IN_PROGRESS`) — el indicador en pantalla, el interruptor por vídeo con memoria por
  serie o curso, la superresolución del fabricante y AMD.
- **`PLY-018`** (`IN_PROGRESS`) — la cifra de coste por fotograma que su criterio promete.
- **`CRS-006`** (`DESIGN_APPROVED`) — la banda de imagen de la tarjeta de curso, sacada de la primera
  lección y no de la que se esté viendo.
- **`PRD-002`**, **`PRD-003`**, **`REL-001`**, **`REL-004`** — las cuatro que no se resuelven
  programando: el certificado comercial de firma, la máquina ARM64, la ficha de la tienda y la
  comprobación de marca del nombre público.

## Hechas

Se quedan con su identificador y su fecha de nacimiento, en el mismo orden, para que se vea qué salió
de dónde y para que ningún número se reutilice. Lo que hicieron está contado en el changelog.

| Id | Nacida | Cerrada | Qué era |
| --- | --- | --- | --- |
| `ENG-004` | 2026-09-11 | 2026-09-13 | «La cifra de duración de un run», decisión del propietario que estuvo abierta. Zanjada: un dato que siempre va a estar desfasado no se guarda, se mide cuando alguien lo necesita. Vive en `eng/measure-ci-time.ps1`. |
| `ENG-006` | 2026-09-12 | 2026-09-12 | El primer eslabón de `PLY-016`: el intercambio de `UYVY` a `YUY2` al subir la textura. Evidencia `PLY16-uyvy-to-yuy2.md`. |
| `ENG-007` | 2026-09-12 | 2026-09-12 | El engranaje del reproductor con su maqueta aprobada, y el ajuste de imagen dentro. |
| `ENG-008` | 2026-09-12 | 2026-09-13 | El botón «Restaurar valores por defecto» en todos los grupos de opciones, por la misma clave de traducción. Salieron **doce** y no catorce, contados al escribir la lista. `UX-010`. |
| `ENG-012` | 2026-09-13 | 2026-09-13 | El escalador propio de `PLY-016`, que ya dibuja: la rampa de un borde ampliado baja de 4 píxeles a 2. Evidencia `PLY16-portable-upscaler.md`. |
