# Dónde retomar — 2026-09-20

> Se **sobrescribe** en cada cierre y no pasa de 80 líneas ni de 6 KB por idioma; lo mide
> `eng/check-handoff.ps1`. La historia hasta el 2026-09-19 está congelada en
> [NEXT-SESSION-HISTORY.es.md](NEXT-SESSION-HISTORY.es.md). **El árbol manda sobre este documento**:
> `git log --oneline -1`, `git log --oneline -1 main` y `gh run list --limit 3` antes que nada.

## Estado

`main` y `codex/ap-reelume-mvp-x64` quedaron en el mismo commit, con su CI leído en verde. Este
relevo va un commit por delante, por ser sólo documentación, y su CI no se espera.

## Lo que se hizo

· **El auditor de puertas sobre lo que trajo la adopción**, y trece comprobaciones pasaban sin
  detectar el defecto que debían detectar. Cada una tiene ahora su mutante, visto sobrevivir antes y
  morir después. Cerrada `ENG-038`: el recibo del acta guardaba mal la lista de saltos.
· **El fotograma del propio vídeo ya llega a la cuadrícula** (`LIB-021`, plan 2 de 3): la pasada
  corre al abrir la ventana y tras cada escaneo, y la tarjeta cambia su imagen en vez de rehacerse
  la cuadrícula, que es lo que la hacía peligrosa para el paseo.
· `CatalogItemViewModel` sale de la deuda de cobertura al 100/100 y el trinquete baja a **185**.
· Evidencias: `audit-adoption-gates.md` y `LIB021-cover-origins-frame.md`.

## Las trampas medidas

· **La puerta de cobertura SUMA las ramas de cada suite**, no toma «cubierta en cualquier sitio»:
  media rama aquí y media allá son dos de cuatro. Costó un rojo de CI.
· **La previsualización de suelos calla sobre un archivo nuevo sin commitear** (`ENG-016`), así que
  no avisó de que el archivo nuevo medía 100/50. Costó el otro rojo.
· En PowerShell, un `if` usado como valor desenrolla su salida: una lista vacía sale `null` y una de
  uno sale suelta. Era el defecto de `ENG-038`, y el mismo patrón está en el plugin común.

## Lo primero de la sesión siguiente

· `docs/TAREAS.md`, la primera abierta que no esté parada.
· El plan 3 de `LIB-021` —el ajuste del orden de portadas con su «Restaurar valores por defecto» y
  la excepción por título— cierra `ENG-003`, que lleva abierta desde el 2026-09-05.

## Lo que espera al propietario

· La sesión de IT publica hoy la **0.10.0** del sistema común; al avisar, toca migrar el registro de
  métricas a JSON Lines con `adopt-enrol` y repetir ping, `prove` y el doctor.
· `ENG-002` (una sesión con el Narrador) y lo demás suyo, como está en `docs/TAREAS.md`.

## Lo pendiente no está aquí

Se lee con `pwsh -NoProfile -File eng/list-pending.ps1` y en [TAREAS.md](TAREAS.md).
