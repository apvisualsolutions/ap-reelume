# Hoja de ruta

Qué hace AP Reelume hoy, qué hará después y qué ha decidido no hacer. La versión inglesa está en
[README.en.md](README.en.md). El registro canónico del alcance es
[FEATURES.md](../FEATURES.md); esto es su lectura en prosa.

## Las tres versiones

| Versión | Qué significa |
|---|---|
| `MVP` | Aplicación x64 instalable y útil para validar una colección real. Puerta aprobada el 2026-08-05. |
| `STABLE` | Primera publicación pública completa, incluida ARM64. Es donde estamos. |
| `POST_STABLE` | Mejoras que no bloquean la primera publicación estable. |

## Dónde estamos

El MVP cataloga, identifica, reproduce y recuerda dónde se quedó, en español y en inglés, sin cuenta
y sin enviar nada a ninguna parte. Se distribuye como MSIX x64 y como ZIP independiente, ambos con
hash publicado y compilación reproducible.

De los 46 compromisos del MVP: **44 verificados**, **1 fuera de alcance por decisión** y
**1 bloqueado** por hardware o entorno que este equipo no tiene. Ninguno queda informalmente
pendiente: cada bloqueo dice quién lo tiene y qué lo desbloquearía.
[release-readiness.md](../evidence/mvp/release-readiness.md) los detalla.

El Product Owner aprobó la puerta MVP el **2026-08-05** con ese bloqueo declarado. Aprobar la puerta
no lo resuelve: `PLY-004` sigue bloqueado con la misma condición, y los riesgos que el MVP deja
abiertos se heredan en `STABLE` en vez de cerrarse. Con la aprobación arranca la Parte B.

## Lo que viene: `STABLE`

| ID | Qué falta |
|---|---|
| `PRD-003` | Paridad ARM64. La compilación y el paquete nativo ya están hechos y verificados; falta certificar la reproducción en una máquina Windows 11 ARM64, que no hay. Bloquea la publicación estable. [T42](../evidence/stable/T42-arm64.md) |
| `REL-001` | Microsoft Store como distribución principal, con su certificación. Lleva dos deudas conocidas del MVP: justificar ante la Store la capacidad restringida `unvirtualizedResources` —sin ella el paquete borra la biblioteca al desinstalarse— y decidir cuándo firmar, porque el certificado comercial cambiará la identidad del paquete. |
| `REL-004` | Comprobación formal de marca, dominios y Store para el nombre público. |

De esa lista ya están hechos `REL-003` y `PLY-013`. El actualizador independiente comprueba, resume
en los dos idiomas, descarga a una carpeta aparte comprobando hash y tamaño, y no entrega nada a
Windows sin una confirmación que nombra la versión que estaba en pantalla; la Store mantiene su
propio canal. [T44](../evidence/stable/T44-updater.md) Y la detección automática de segmentos
compara localmente los episodios de cada serie, cumple cada umbral aprobado sobre un corpus
retenido y nunca pisa una marca manual ni una corrección humana.
[T43](../evidence/stable/T43-segment-detection.md)

## Lo que se hará después: `POST_STABLE`

| ID | Qué es |
|---|---|
| `UX-007` | Listas personalizadas. El modelo actual admite añadirlas sin migración destructiva. |
| `PLY-015` | Dolby Vision y passthrough Dolby/DTS. Requiere una evaluación técnica, legal y de demanda que no se ha hecho. |

## Lo que esta versión **no** hace

Esto no es una lista de tareas pendientes: son decisiones. Cambiarlas exige actualizar primero la
especificación y la matriz, en ambos idiomas.

- **No hay cuentas ni sesión remota.** Una persona, un PC. No hay registro, ni contraseña, ni perfil.
- **No hay sincronización entre equipos ni nube.** Lo que ve la aplicación está en su disco.
- **No reproduce varios vídeos a la vez.** Hay una sesión de reproducción, y una sola.
- **No es una plataforma de cursos**, y desde `ADR-0006` la frase está acotada, no borrada. Lo que
  sigue fuera es lo que la motivaba: no hay matrículas, ni certificados, ni cuestionarios, ni rachas,
  ni estadísticas de estudio, ni porcentaje de formación completada, ni nada que hable con una
  plataforma. Lo que entra (`CRS-001`…`CRS-005`) es lo que la aplicación ya hace con una serie:
  reconocer lo que hay en el disco, ordenarlo, reproducirlo en orden y recordar por dónde iba.
- **No gestiona vídeos más allá de catalogarlos.** No convierte, no recorta y no exporta vídeo. El
  renombrado seguro es la única operación que toca los archivos, y previsualiza antes de hacer nada.
- **No hay notas ni marcadores personales en la línea de tiempo** (`UX-008`). Las marcas de
  introducción y créditos existen (`PLY-012`), pero son de la serie, no un cuaderno personal.
- **No hay listas personalizadas todavía** (`UX-007`, pospuesto).
- **No hay Dolby Vision ni passthrough de audio** (`PLY-015`, fuera de alcance).
- **No hay macOS ni Linux.** El núcleo está desacoplado y no referencia APIs de Windows ni de
  Avalonia (`PRD-004`), de modo que portarlo sería posible; no está planificado.

## Cómo cambia esta hoja de ruta

Una funcionalidad sólo pasa a `VERIFIED` cuando su evidencia está enlazada en la matriz. Un cambio de
alcance —añadir algo de la lista de arriba, o quitar algo de la de abajo— se registra primero como
decisión en un [ADR](../adr) y después en la matriz, en español e inglés.

