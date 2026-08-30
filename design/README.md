# AP Reelume — paquete de diseño

Biblioteca de medios local para Windows 11 (C# + Avalonia 12.1.1, LibVLC, SQLite). Sin cuentas, sin telemetría por defecto, sin streaming. GPL-3.0-or-later.

**Estado.** El rediseño de las 53 vistas está **implementado y verificado** en el repositorio (`PRD-006`, matriz de paridad cerrada el 2026-08-24). Este proyecto queda como fuente de diseño viva: el prototipo navegable, cada vista por separado, y **una única propuesta abierta: Cursos (CRS)**.

## Archivos

| Archivo | Qué es |
| --- | --- |
| `AP Reelume.dc.html` | Prototipo navegable completo: las 53 vistas del censo + el área de Cursos propuesta. El panel **Demostración** recorre 28 estados |
| `vistas/` | **57 archivos, una vista cada uno.** Abren el prototipo directamente en esa vista (misma fuente, cero divergencia) |
| `Propuesta de diseño - AP Reelume.dc.html` | El sistema: tokens en los 4 temas con ratios, tipografía, los 5 estados de cada control, vista por vista, activos de instalación, compatibilidad AXAML |
| `Catálogo de elementos - AP Reelume.dc.html` | Cada elemento en sus estados y las 4 animaciones, ordenado por elemento |
| `Cadenas nuevas - AP Reelume.dc.html` | Las claves del rediseño (ya en el árbol) + **las 41 claves de Cursos, propuestas** |
| `PROMPT.md` | Orden de trabajo para implementar Cursos en el repositorio |
| `github.md` | Asociación al repositorio y recibo de sincronización |
| `support.js`, `doc-page.js`, `vistas/support.js` | Runtime de los prototipos. No forman parte del diseño |

Todo se abre en un navegador; nada necesita servidor.

## Las vistas por separado (`vistas/`)

Cada archivo lleva el nombre del `.axaml` que le corresponde y abre el prototipo ya situado en esa vista con el estado que la hace visible. Las 53 del censo (`docs/design/SURFACES.es.md`, medición 2026-08-24) + 4 de Cursos.

Equivalencias que conviene saber:

- `LifecycleSettingsView` abre **Privacidad**: bandeja e inicio con Windows viven ahí en el prototipo.
- `PlayerVersionsView` y `VersionSwitchDialog` abren el diálogo de cambio de versión (la misma superficie en el prototipo).
- `LooseFileBanner` abre el diálogo de archivo suelto («Solo reproducir, sin añadir»), que es donde el prototipo modela esa sesión.
- `UnavailableBadge` abre la Biblioteca con la raíz USB desconectada, para que la insignia exista.
- `DiagnosticsPreviewView` abre Privacidad con diagnósticos y previsualización activados (la forma condicional).
- Las vistas de Inicio (`ResumeHeroView`, `InProgressRailView`, `RecentlyAddedRailView`, `RecommendationsRailView`) abren Inicio: son sus cuatro bloques.

## Cursos (CRS) — la propuesta abierta

**Problema.** Carpetas de videocursos y tutoriales que se abandonan: al volver semanas después no se sabe por cuál lección ibas ni qué contaba la anterior. El hilo se pierde y el curso muere.

**Principio.** El hilo lo guarda la aplicación, no la memoria: última lección, minuto exacto y fecha, más un resumen de lo último visto para recuperar contexto. Nada que apuntar.

### Reglas de diseño

1. **Un curso es una carpeta que el usuario marca**, nunca una identificación de proveedor: no hay nada que consultar (cero red, coherente con PRI-001). Orden de lecciones = orden del nombre de archivo (`01 …`, `02 …`); subcarpetas = módulos.
2. **El progreso es por lección** y reutiliza la maquinaria existente: guardado exacto (PLY-008), identidad por archivo que sobrevive a mover/renombrar (LIB-009), estados ○ ◐ ● (PLY-009).
3. **El hilo** = lección actual + minuto + fecha + las 2 últimas lecciones vistas («Lo último que viste»). Es la pieza que responde a «¿por dónde iba?» sin obligar a re-ver nada.
4. Nada se copia, mueve ni renombra; la carpeta NO se convierte en raíz de escaneo.
5. Cursos no pasa por la bandeja de revisión ni por duplicados: no hay candidatos que revisar.

### Vistas nuevas (4 `.axaml`) y reuso

| Vista | Qué es | Referencia |
| --- | --- | --- |
| `CoursesView` | Cuadrícula de cursos: progreso, restante, última vez, CTA «Continuar · M2·L06». Vacío en positivo | `vistas/CoursesView.dc.html` |
| `CourseDetailsView` | Ficha: cabecera con carpeta y progreso, módulos con lecciones, y el **panel del hilo** (Dónde lo dejaste + resumen + CTA) | `vistas/CourseDetailsView.dc.html` |
| `LessonRowView` | Fila de lección: glifo ○◐●, número, título, estado, barra parcial, reproducir, marcar vista (espejo de `EpisodeRowView`) | `vistas/LessonRowView.dc.html` |
| `LessonsPanelView` | Panel «Lecciones» en la columna del reproductor (320 px): módulos y lecciones con la actual resaltada. **Ausente** salvo en sesión de lección | `vistas/LessonsPanelView.dc.html` |

Reuso sin vista nueva: `NextEpisodeOverlay` gana la variante «Siguiente lección»; `AddRootDialogView` (modo contenido) gana la opción «Curso (carpeta de lecciones)»; `WatchStatusControl` sirve tal cual; el menú del riel gana su bloque (Todos · Con hilo pendiente · Terminados · Marcar carpeta).

### Modelo de datos (SQLite, migración no destructiva)

- `courses(id, root_path, title, marked_at, last_opened_at)` — el título sale del nombre de la carpeta, editable con el editor protegido existente.
- `lessons(course_id, file_identity, module, sort_key, title, duration)` — `file_identity` es la identidad de LIB-009: mover o renombrar el archivo conserva el progreso.
- El progreso por lección **es** el progreso existente (PLY-008): no se inventa un segundo almacén. «Vista» respeta el umbral configurable y la marca manual gana, como en PLY-009.

### Identificadores propuestos (para `FEATURES.md`)

| ID | Función |
| --- | --- |
| CRS-001 | Marcar carpeta como curso; lecciones por nombre de archivo, módulos por subcarpeta; sin red |
| CRS-002 | El hilo: última lección + minuto + fecha + resumen; «Retomar el hilo» como acción primaria |
| CRS-003 | `CoursesView` y `CourseDetailsView` con estados vacío/parcial/terminado |
| CRS-004 | Panel Lecciones del reproductor + «Siguiente lección» al terminar una |
| CRS-005 | Marcar/desmarcar lección como vista, con identidad que sobrevive a mover el archivo |

**Estado: propuesta.** El registro canónico de alcance es `docs/FEATURES.md` y ahí no existe CRS: la decisión de alcance va antes que el código (UX-007 «listas personalizadas» quedó DEFERRED — esta propuesta es más acotada y resuelve un dolor concreto, pero la decisión es del dueño).

### Restricciones heredadas que CRS debe cumplir

Bilingüe por `DynamicResource` (las 41 claves están en `Cadenas nuevas`); nombre accesible + prueba + línea en el paseo por cada control nuevo, en el mismo cambio; solo tokens de `DesignTokens.axaml` (ningún color literal); filas de acciones en `WrapPanel`; el panel del reproductor a 320 px fijos y todo superpuesto con alineación y `MaxWidth`/`MaxHeight`; ausente ≠ deshabilitado (el panel Lecciones y el carril de recomendaciones son **ausentes** cuando no aplican); glifos literales ○ ◐ ● con `AutomationProperties`; movimiento por `MotionDuration`.

## Cambios de esta pasada en el prototipo

- Área **Cursos** completa (nav, cuadrícula, ficha con hilo, panel del reproductor, opción del diálogo de añadir, estado de demostración «Reproductor · lección de curso»).
- **Carril de recomendaciones** en Inicio (censo: `RecommendationsRailView`): local, explicable, con el porqué en cada tarjeta; apagado = ausente.
- Sección de Ajustes **Recomendaciones** (censo: `RecommendationSettingsView`); el umbral de visto vive ahí, como lo modela el árbol (WP-2).
- Prop `view` en el prototipo: abre cualquier vista del censo directamente (lo usan los archivos de `vistas/`).

## Pendiente

1. **Activos de instalación (35 archivos)** — sigue bloqueado en el original vectorial de la marca (detalle en `Propuesta de diseño`).
2. **Decisión de alcance CRS** — filas en `FEATURES.md` antes de escribir código.
3. `LooseFileBanner` — el defecto que impide verla al abrir desde el Explorador sigue registrado en el repositorio (`eng/walk-pending.txt`).
