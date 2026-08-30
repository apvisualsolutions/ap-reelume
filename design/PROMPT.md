# Orden de trabajo — implementar Cursos (CRS) en `apvisualsolutions/ap-reelume`

Rama de partida: `codex/ap-reelume-mvp-x64`. El rediseño de las 53 vistas ya está implementado y verificado (PRD-006); **no rehagas nada de eso**. Este encargo es solo el área de Cursos, y es una **propuesta**: el paso 1 decide si se hace.

## Paso 0 — contexto

1. Lee `design/README.md` (sección «Cursos (CRS)») — reglas, modelo de datos, vistas, restricciones.
2. Abre en un navegador `design/vistas/CoursesView.dc.html`, `CourseDetailsView.dc.html`, `LessonRowView.dc.html` y `LessonsPanelView.dc.html` — son la referencia visual exacta, en los cuatro temas (panel Demostración).
3. Las cadenas están en `design/Cadenas nuevas - AP Reelume.dc.html`, sección «Cursos (CRS)»: 41 claves con su texto definitivo en los dos idiomas.

## Paso 1 — decisión de alcance (bloqueante)

`docs/FEATURES.md` es el registro canónico y CRS no existe en él. Añade las filas CRS-001…CRS-005 (texto en el README) con estado `DESIGN_APPROVED`, objetivo `POST_STABLE` salvo que el dueño diga otra cosa, y criterio de aceptación por fila. **Si la decisión de alcance no está aprobada, para aquí y deja las filas propuestas en un commit propio.**

## Paso 2 — orden de implementación

Cada tramo con su commit y sus pruebas; ningún control sin nombre accesible ni cadena fuera de los dos archivos.

1. **Cadenas**: las 41 claves en `Strings.es.axaml` y `Strings.en.axaml` en el mismo cambio (`BilingualHeadingTests` lo exige).
2. **Modelo**: migración SQLite no destructiva — `courses` y `lessons` (`file_identity` = identidad LIB-009). El progreso por lección reutiliza el almacén de PLY-008; nada nuevo que respaldar salvo las dos tablas (entran en la copia DAT-002).
3. **Marcado**: opción «Curso (carpeta de lecciones)» en el diálogo de añadir contenido + caso de uso que recorre la carpeta, ordena por nombre de archivo y agrupa por subcarpeta. Sin red, sin candidatos, sin bandeja.
4. **`CoursesView`**: cuadrícula con progreso, restante (`{0} h {1} min`), última vez y CTA «Continuar · M·L»; vacío en positivo con la acción de marcar. Entrada de navegación «Cursos» con su menú (Todos · Con hilo pendiente · Terminados · Marcar carpeta…).
5. **`CourseDetailsView` + `LessonRowView`**: cabecera (carpeta en mono, progreso, «Curso terminado» si aplica), módulos con lecciones (glifo ○◐●, barra parcial, reproducir, marcar/desmarcar), y el panel del hilo: «Dónde lo dejaste» + minuto + fecha, «Lo último que viste» (2 lecciones), CTA «Retomar el hilo».
6. **Reproductor**: botón y panel «Lecciones» (320 px, **ausente** salvo `kind == lesson`), lección actual resaltada; al terminar una lección, la cuenta atrás existente ofrece «Siguiente lección» (misma cancelación por tres entradas de PLY-011, revalidación del archivo en cero).
7. **Pruebas**: nombre accesible por control nuevo + línea en el paseo, bilingüe, y una prueba del hilo: ver a medias, cerrar a la fuerza, reabrir → «Retomar el hilo» apunta a la misma lección dentro de ±5 s.

## Reglas duras (rompen pruebas si se ignoran)

1. Toda cadena visible por `DynamicResource`, en los dos archivos o en ninguno.
2. `Content` y `AutomationProperties.Name` comparten clave: reescribir una etiqueta es renombrar el control.
3. Solo tokens de `Theme/DesignTokens.axaml`; los cuatro temas + movimiento reducido (`MotionDuration`).
4. Filas de acciones en `WrapPanel`; paneles superpuestos con alineación y `MaxWidth` **y** `MaxHeight`; la columna del reproductor es de 320 px fijos.
5. Ausente ≠ deshabilitado: el panel Lecciones y el carril de recomendaciones **no existen** cuando no aplican; no se ponen en gris.

## Verificación final

Captura la aplicación real junto a `design/vistas/CoursesView.dc.html`, `CourseDetailsView.dc.html` y `LessonsPanelView.dc.html` en claro y oscuro (el patrón de la matriz de paridad de PRD-006) y añade el changelog bilingüe del tramo.
