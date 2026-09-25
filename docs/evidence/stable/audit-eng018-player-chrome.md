# ENG-018 — Los controles del reproductor, como en cualquier reproductor / The player's controls, like any player's

- Fecha / Date: 2026-09-25
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `0e94b3c6`
- Entorno / Environment: Windows 11 x64, .NET según `global.json`, Avalonia 12.1.1, LibVLC 3.0.23
- IDs: `ENG-018` (cerrada aquí), `ENG-049` (nace aquí), [`ADR-0014`](../../adr/0014-the-chrome-goes-away-on-a-clock.md)

## Veredicto / Verdict

**Los controles se van solos a los tres segundos sin ratón ni teclado mientras la película avanza, y
el puntero con ellos; un clic en la imagen pausa, la rueda mueve el volumen y Esc retrocede una
capa.** Las cinco cosas que el propietario echaba en falta frente a cualquier reproductor, cada una
con su prueba vista fallar y su mutante visto morir. / **The chrome leaves by itself three seconds
after the last movement or key while the film plays, and the pointer with it; a click on the picture
pauses, the wheel moves the volume and Escape steps back one layer.** Each with its test seen red and
its mutant seen killed.

**Y el paseo encontró un defecto que habría llegado a una persona**: el reproductor oye las teclas
antes que nada de lo que lleva dentro, así que cerrar con Esc la lista de familias del estilo de
subtítulos cerraba también el engranaje entero. Corregido con su prueba. / **And the walk found a
defect a person would have hit**: closing the subtitle-family list with Escape also closed the whole
gear. Fixed, with its test.

## Lo que había / What there was

Medido en el código antes de tocarlo (`ShellViewModel.cs`, `ShellView.axaml.cs`, `PlayerView.axaml.cs`):

- Un movimiento del ratón revelaba los controles y **nada los volvía a ocultar** hasta pausar. Estaba
  escrito como decisión: un reloj no se podía probar sin esperar y el paseo competiría con él.
- El puntero no se ocultaba nunca (`Cursor` no aparecía en `src/`).
- Clic simple sobre la imagen y rueda: sin manejador.
- Esc sólo llamaba a volver al modo incrustado; con la película ya incrustada no hacía nada.

## Lo que se hizo / What was done

- **Reloj**: `ShellSurfaces.ChromeClock` (el `IClock` de la aplicación) y
  `ShellViewModel.ChromeIdleTimeout` (3 s; infinito lo apaga). Cada movimiento anota la hora y una
  sola espera calcula al despertar cuánto le falta. No oculta con panel o engranaje abiertos ni con
  el puntero sobre una superficie de controles, que la vista identifica por nombre.
- **Puntero**: `StandardCursorType.None` sobre el escenario mientras los controles no están. El tipo
  se leyó del ensamblado 12.1.1 porque el MCP de Avalonia no lo conocía.
- **Clic y rueda** en `PlayerView`, **sólo sobre la imagen**: el clic de un botón de la barra también
  sube hasta el reproductor. El paso de volumen pasa a vivir en `TransportControlsViewModel` y el
  teclado lo lee de ahí.
- **Esc** en `ShellViewModel.EscapeAsync`: engranaje, panel, modo de ventana, reproductor.

## Pruebas / Tests

| Suite | Resultado / Result |
| --- | --- |
| `UiTests` | 1.497 de 1.497, 1 m 3 s. La pasada anterior dio un rojo que no es de esto: el `Test Case Cleanup Failure` de `ENG-040`, en `HomeCardTests`, de 1 ms y con la traza entera dentro de Avalonia; la repetición del mismo binario salió limpia |
| `AccessibilityTests` | 155 de 155, 3 m 55 s |
| `ArchitectureTests` | 61 de 61, 8 m 22 s |
| `IntegrationTests` | 694 de 696, 2 omitidas (las de siempre), 7 m 38 s |

Nuevas: `ChromeIdleTests` (10), `ShellEscapeTests` (3), y en `PlayerViewInputTests` clic, rueda, clic
real con el ratón simulado, clic en la barra y Esc con un desplegable abierto. En el paseo, la escena
`The_tracks_and_the_audio_output_are_chosen_with_the_mouse` baja el reloj a 200 ms y comprueba con el
motor real que los controles se van solos; el resto del paseo lo pone en infinito.

## Mutantes / Mutants

Cada uno aplicado sobre una copia comparada con el original antes de creer el resultado, y el árbol
comprobado idéntico después:

| Mutante | Lo mata |
| --- | --- |
| Un panel abierto deja de retener | `ShellEscapeTests` y dos de `PlayerPanelColumnTests` |
| El engranaje abierto deja de retener | `The_open_gear_keeps_the_chrome_as_well` |
| El puntero sobre un control deja de retener | `A_pointer_resting_on_the_controls_…` |
| La barra deja de contar como control | la misma |
| El movimiento no reinicia la cuenta | `Every_movement_starts_the_count_again` |
| Una espera vieja decide sobre la sesión nueva | `A_wait_that_outlives_its_session_…` y `A_paused_film_…` |
| El tiempo infinito se ignora | `An_infinite_timeout_turns_the_clock_off` |
| El puntero invertido | `The_pointer_hides_over_the_picture_…` |
| El clic cuenta en cualquier sitio | `A_click_on_the_bar_is_not_a_click_on_the_picture` |
| Esc no cierra el engranaje | `Escape_closes_the_gear_first_…` |
| La rueda sube en los dos sentidos | `The_wheel_over_the_picture_…` |
| El reloj no se conecta en la aplicación montada | la escena del paseo, con el motor real |

Dos mutantes se escribieron primero como condición constante y **no compilaban** (`CS0162`); se
rehicieron con una condición que compila antes de contarlos. Un tercero chocó con `CA1822`. Un rojo
de compilación no es un mutante muerto.

## El paseo, y lo que costó / The walk, and what it cost

La primera pasada completa dio **5 rojos de 155**, y ninguno era del reproductor: el paseo demuestra
cada pulsación con un clic **al lado** que no debe cambiar nada, y ese clic caía ahora sobre la
imagen y pausaba. El arnés ya trataba botones y listas como ocupados; ahora trata también la imagen,
preguntando **qué elemento hay bajo el punto** —lo mismo que pregunta la aplicación— y no por
geometría, porque las tarjetas que flotan sobre la imagen desde fuera del reproductor se llevan su
clic. Dos trampas medidas por el camino: el panel raíz del reproductor cuelga de un presentador de
contenido y no del reproductor, y en el mini reproductor la prueba de impacto contestaba «nada» al
elegir el punto y «VideoSurface» al llegar el clic, así que «nada» dentro de un reproductor cuenta
como imagen. En esa ventana el único sitio que no hace nada es el hueco entre botones, que se añadió
como último candidato para no mover los puntos de ninguna otra escena.

## La cobertura, sin una vuelta de más / Coverage, without an extra round

`preview-coverage-floors` leyó `ShellView.axaml.cs` **mejorando de 98/76 a 98/80**, y la puerta de
CI falla igual ante un suelo que se queda corto que ante uno que se queda largo. Empujar así era un
rojo sabido, y copiar un suelo de un run que aún no existía no se puede. Las ramas que faltaban se
leyeron del Cobertura línea a línea: seis eran guardas que nada podía tomar —dos comprobaciones de
nulo en un manejador que sólo se engancha a un modelo que existe, dos `FindControl` sobre controles
del propio marcado y una pregunta al coordinador que el modelo ya contesta— y se quitaron; tres se
podían tomar y se cubrieron en `ShellViewEdgeTests` —quitar el contexto, cambiar de modo sin ventana
y cerrar el reproductor mientras se espera un cambio de modo, que es una carrera real—. **100/97
sobre `UiTests` sola**, el archivo sale de la lista copiando el artefacto `coverage-debt` del run
`35537409707` sin su fila, y el trinquete baja de 185 a 184.

## Pendiente / Open

`ENG-049`: en la ventana, revelar y ocultar cambia el tamaño del vídeo, y ahora pasa más a menudo.
Pide que el propietario lo vea antes de decidir si esos controles deben flotar.
