# Diseño de AP Reelume / AP Reelume Design

- Estado / Status: `APPROVED_DESIGN`
- Fecha / Date: 2026-08-01
- Nombre de producto / Product name: **AP Reelume**
- Presentación completa / Full presentation: **AP Reelume by AP Solutions**
- Firma editorial / Publisher signature: **by AP Solutions**
- Plataforma inicial / Initial platform: Windows 11
- Documentos relacionados / Related documents: [`docs/FEATURES.md`](../../FEATURES.md), [`ADR-0001 — nombre público`](../../adr/0001-public-product-name.md)

Este documento contiene primero la especificación completa en español y después su traducción inglesa. Ambas partes describen el mismo producto y deben actualizarse juntas.

This document contains the complete Spanish specification first and its English translation second. Both parts describe the same product and must be updated together.

---

# Parte I — Especificación en español

## 1. Visión y resultado esperado

**AP Reelume by AP Solutions** es una biblioteca local de películas y series para una sola persona en un único PC. Cataloga vídeos existentes sin copiarlos, identifica automáticamente películas y episodios, reproduce formatos variados y recuerda la posición exacta. No requiere cuenta ni sincronización.

La primera entrega será un MVP x64 instalable y realmente utilizable. La primera versión pública estable añadirá los requisitos de publicación, ARM64 y detección automática de introducciones/créditos que figuran como bloqueantes en la matriz. El producto será gratuito, de código abierto y funcionará sin un servidor propio obligatorio.

### Indicadores de éxito

1. Una carpeta real con nombres heterogéneos produce un catálogo correcto y separa los casos dudosos.
2. Una sesión interrumpida reanuda dentro de ±5 segundos del último punto persistido.
3. Una biblioteca de 10.000 archivos sigue siendo navegable y buscable sin bloquear la interfaz.
4. Desconectar un USB o NAS no elimina metadatos ni progreso.
5. Restaurar una copia en rutas diferentes recupera los datos personales y permite reasignar las raíces.
6. Todas las acciones esenciales funcionan con teclado y Narrator sin defectos críticos.
7. Ninguna versión se declara terminada sin actualizar la matriz de funcionalidades y enlazar evidencia.

## 2. Alcance

### Incluido

- Películas y series; no vídeos genéricos, cursos ni colecciones arbitrarias.
- Carpetas en discos internos, USB y recursos UNC/NAS, usando siempre la ubicación original.
- Escaneo al añadir una raíz, al iniciar y bajo demanda; vigilancia continua cuando sea fiable.
- Identificación local por nombre/carpeta y enriquecimiento con TMDB.
- Revisión de coincidencias dudosas, duplicados, correcciones manuales y renombrado seguro opcional.
- Reproductor integrado, apertura externa alternativa, progreso, estados y siguiente episodio.
- Favoritos, ver más tarde, valoración y recomendaciones calculadas localmente.
- Copia/restauración local, exportación/importación y actualización del programa.
- Interfaz Fluent moderna, accesible e internacionalizable.

### Excluido del MVP

- Cuentas, perfiles, sincronización y almacenamiento en la nube.
- Copiar o mover vídeos a una estructura administrada por la aplicación.
- Reproducir varios vídeos simultáneamente.
- Marcadores, notas o capturas personales en la línea de tiempo.
- Listas personalizadas; quedan en `POST_STABLE`.
- Dolby Vision y passthrough Dolby/DTS; requieren una nueva evaluación.
- macOS/Linux; la arquitectura evita cerrarlos, pero no son entregables actuales.

## 3. Decisiones tecnológicas

| Área | Decisión | Motivo |
|---|---|---|
| Lenguaje | C# sobre .NET 10 LTS | Rendimiento, tipado, ecosistema y soporte activo hasta 2028. |
| Interfaz | Avalonia 12.1 con XAML y MVVM | Fluent moderno, accesibilidad y ruta multiplataforma sin sacrificar Windows-first. |
| Vídeo | LibVLCSharp 3 estable + LibVLC de VideoLAN | Amplia matriz de formatos, aceleración, HDR y control mediante API. |
| Datos | SQLite, WAL, migraciones y FTS5 | Archivo local robusto, transaccional, portable y buscable. |
| Metadatos | Adaptador TMDB con caché | Datos ricos en español y proveedor reemplazable. |
| Empaquetado | MSIX para Store; artefactos independientes en GitHub | Instalación/actualización segura con coste obligatorio cero. |
| Licencia | GPL-3.0-or-later | Preserva la naturaleza abierta del producto y es compatible con el diseño basado en componentes libres, sujeto a auditoría final. |

Las versiones se fijarán en archivos de dependencias y se actualizarán mediante decisiones revisadas. LibVLC queda detrás de `IMediaPlayerEngine`; TMDB detrás de `IMetadataProvider`; SQLite detrás de repositorios. Ninguna de estas elecciones puede filtrarse al dominio.

La credencial de lectura de TMDB no se almacena en el repositorio. Las compilaciones oficiales la inyectan desde secretos de CI en un recurso de aplicación recuperable pero de alcance limitado; las compilaciones locales aceptan un token proporcionado por el desarrollador. La aplicación controla límites, atribuye a TMDB y permite funcionar sin proveedor. No se introduce un proxy propio en el MVP.

## 4. Arquitectura

### Regla de dependencias

```text
Presentation (Avalonia/MVVM)
            ↓
Application (casos de uso y orquestación)
            ↓
Domain (entidades, valores, políticas e interfaces)
            ↑
Infrastructure (SQLite, archivos, TMDB, LibVLC, Windows)
```

`Presentation`, `Infrastructure` y el host de Windows dependen de contratos internos; el dominio no los conoce. Los eventos de aplicación comunican cambios largos —escaneo, coincidencias y sesión de reproducción— sin exponer hilos ni objetos de frameworks.

### Módulos

1. **Catalog**: películas, series, temporadas, episodios, versiones de archivo y consultas.
2. **Discovery**: raíces, escaneos, vigilancia, firmas y disponibilidad.
3. **Identification**: analizadores de nombres, candidatos, confianza y revisión.
4. **Metadata**: TMDB, caché, idioma, imágenes y campos bloqueados manualmente.
5. **Playback**: motor, pistas, salida, HDR, velocidad, volumen, ventanas y errores.
6. **Continuity**: posición, estado, historial mínimo, umbral y siguiente episodio.
7. **Personalization**: favoritos, ver más tarde, valoraciones y recomendaciones locales.
8. **Backup**: copias rotatorias, exportación, importación, integridad y reasignación.
9. **WindowsIntegration**: MSIX, bandeja, inicio, teclas multimedia, “Abrir con…” y Mica.

Cada módulo expone comandos/consultas estrechos y no permite que una vista acceda directamente a SQLite, TMDB o el sistema de archivos.

## 5. Modelo de datos

### Entidades principales

- `LibraryRoot`: ruta normalizada, tipo local/USB/UNC, disponibilidad, política de escaneo y último resultado.
- `Title`: identidad común para película o serie, proveedor externo, títulos localizados, arte y campos bloqueados.
- `Season` y `Episode`: orden, números estándar/absolutos/especiales y metadatos localizados.
- `MediaFile`: ruta, identidad del volumen, ID del archivo cuando existe, firma ligera, tamaño, duración, pistas, códecs y disponibilidad.
- `MediaVersion`: vincula uno o más archivos con una película o episodio y determina la versión preferida.
- `MatchCandidate`: contenido candidato, puntuación, señales, explicación y estado de revisión.
- `WatchState`: contenido, posición, duración observada, estado, umbral, fechas y modificación manual.
- `PlaybackPreference`: idiomas, subtítulos, velocidad, refuerzo de volumen y salida con ámbitos global/serie/archivo.
- `PersonalState`: favorito, ver más tarde y valoración.
- `IntroMarker`: rangos manuales o detectados, origen, confianza y corrección del usuario.
- `RenameOperation`: vista previa, origen/destino, conflictos, resultado y datos de deshacer.

### Identidad y movimiento

En NTFS se usa el identificador del archivo y volumen cuando está disponible. Para USB/NAS o sistemas sin identidad estable se calcula una firma ligera con tamaño, duración, metadatos técnicos y muestras acotadas de bytes. Nunca se calcula el hash completo de todos los vídeos durante un escaneo normal.

Una coincidencia exacta recupera la entidad anterior. Una coincidencia probable solicita confirmación antes de fusionar. Una nueva ruta no elimina el registro anterior hasta terminar la reconciliación.

### Persistencia

- SQLite utiliza WAL, claves foráneas e índices explícitos.
- FTS5 indexa títulos, alternativos, reparto y géneros; no rutas privadas.
- Los cambios de esquema tienen migraciones hacia delante y copia previa automática.
- La caché de imágenes se guarda separada y puede regenerarse.
- Los datos personales y bloqueos manuales se incluyen siempre en exportación.

## 6. Descubrimiento e identificación

### Flujo de escaneo

1. Enumerar de forma cancelable y limitada, registrando errores por raíz.
2. Filtrar extensiones admitidas sin abrir archivos innecesariamente.
3. Comparar tamaño, fecha e identidad con el índice anterior.
4. Inspeccionar solo archivos nuevos o modificados mediante el adaptador de sonda multimedia.
5. Analizar carpeta y nombre con reglas como `S01E02`, `1x02`, `Cap.803`, temporadas escritas, año y etiquetas entre corchetes.
6. Generar candidatos locales; consultar TMDB solo cuando aporte valor y haya red.
7. Combinar señales de título, temporada, episodio, año y duración en una puntuación explicable.
8. Aplicar el resultado: ≥90 % automático; 60–89 % revisión sugerida; <60 % pendiente.
9. Reconciliar ausentes, movimientos y duplicados sin borrar datos.

El patrón compacto `Cap.803` se interpreta como temporada 8, episodio 3 cuando el contexto lo respalda. `Cap.800` se considera dudoso/especial y va a revisión. Dos archivos `5x10` se agrupan como versiones del mismo episodio.

### Vigilancia

Las raíces locales usan eventos con consolidación para evitar tormentas. USB/UNC emplean vigilancia cuando funciona y escaneo de respaldo al iniciar/manual. Las operaciones se limitan por raíz para no saturar NAS. Una raíz inaccesible conserva su contenido y muestra un error accionable.

### Metadatos

Se prioriza español y se configura un idioma alternativo. La caché contiene respuestas normalizadas, fecha y versión del proveedor. Campos editados/bloqueados no se sobrescriben. La atribución requerida aparece en Acerca de/Créditos.

## 7. Reproducción y continuidad

### Inicio de sesión

1. Resolver la versión preferida disponible.
2. Validar ruta y permisos sin eliminar el elemento si falla.
3. Preparar LibVLC con aceleración y preferencias aplicables.
4. Si existe progreso válido, ofrecer reanudar o empezar desde cero.
5. Aplicar audio, subtítulos, velocidad, volumen y salida.

### Guardado

La posición se guarda cada cinco segundos y también en pausa, búsqueda, cambio de modo, cambio de archivo y cierre. La escritura es atómica e incluye duración observada y versión. Se ignoran posiciones triviales cercanas al inicio y se limita la posición a un rango válido.

Un contenido pasa a “en curso” tras un avance significativo y a “visto” al alcanzar el 90 % por defecto. El umbral es configurable y el usuario puede marcar visto/no visto. Las modificaciones manuales prevalecen hasta que el usuario las revierta.

### Cambio de versión

Si las duraciones coinciden dentro de una tolerancia segura, se conserva el segundo. Si difieren pero parecen ediciones compatibles, se usa proporción. Diferencias grandes o estructuras distintas requieren confirmación. El progreso pertenece al contenido, pero conserva el archivo de origen para auditoría.

### Controles

- Reproducir/pausar, búsqueda, saltos configurables, volumen y silencio.
- Velocidad de reproducción.
- Refuerzo superior al 100 % con limitador de picos y advertencia visual.
- Pistas de audio y subtítulos internos/externos SRT, ASS y VTT.
- Preferencias globales con sustitución por serie o archivo.
- HDR10, conversión a SDR y aceleración con indicador/fallback.
- Estéreo y 5.1/7.1 con dispositivo seleccionable.
- Pantalla completa y mini reproductor; solo una sesión activa.
- Teclas multimedia y atajos configurables.

El reproductor externo es una salida de emergencia; la app no afirma guardar su posición exacta. Al terminar un episodio muestra una cuenta atrás cancelable y reproduce el siguiente disponible. Las marcas manuales de introducción/créditos están en el MVP; la detección automática bloquea la publicación estable según su criterio en la matriz.

## 8. Interfaz y accesibilidad

La dirección aprobada es Fluent moderno sobre Avalonia: superficies Mica/acrílicas donde Windows las permita, azul sereno, jerarquía legible y densidad de escritorio. Inicio usa un patrón híbrido: reanudación prominente, elementos en curso y acceso inmediato a biblioteca.

Vistas principales:

1. Incorporación inicial de carpetas y permisos.
2. Inicio.
3. Biblioteca con búsqueda, filtros y orden.
4. Ficha de película.
5. Ficha de serie/temporada/episodios.
6. Reproductor.
7. Bandeja de revisión y duplicados.
8. Editor de metadatos/renombrado.
9. Copias y restauración.
10. Ajustes, privacidad, créditos y actualizaciones.

El tema sigue Windows, con selección clara/oscura manual; el reproductor permanece oscuro. La interfaz inicial se traduce al español y todo texto se obtiene de recursos. La documentación se mantiene en español e inglés.

La accesibilidad exige navegación completa por teclado, foco visible, nombres/roles/estados para lectores de pantalla, Narrator, escalado de texto, alto contraste, reducción de movimiento, áreas táctiles razonables y personalización de subtítulos. Ningún color es la única señal de estado.

## 9. Recomendaciones y datos personales

Favoritos, ver más tarde y valoración se guardan localmente y entran en las copias. Las recomendaciones usan únicamente datos locales, muestran una explicación simple y pueden desactivarse. No se crea un perfil remoto ni se envía historial. Las listas personalizadas se añaden después de la primera estable sin alterar la semántica de `PersonalState`.

## 10. Fallos y recuperación

| Fallo | Comportamiento | Recuperación |
|---|---|---|
| USB/NAS desconectado | Mantener catálogo y marcar no disponible | Revalidar al reconectar/escanear |
| Acceso denegado | Continuar otras raíces y mostrar ruta/acción | Reintentar tras corregir permisos |
| TMDB caído/límite | Usar caché y mantener candidatos locales | Reintentos espaciados y manual |
| Archivo corrupto | Mantener entidad; mostrar diagnóstico | Otra versión, reintento o externo |
| Motor multimedia falla | Persistir último punto y liberar sesión | Reiniciar motor o abrir externo |
| Cierre inesperado | Perder como máximo el intervalo reciente | WAL y comprobación al iniciar |
| Migración falla | No sustituir la base válida | Restaurar copia previa y abortar actualización |
| Base dañada | No sobrescribir respaldo | Reparación guiada o restauración |
| Conflicto de renombrado | No ejecutar lote parcial | Corregir previsualización y reintentar |

Los trabajos largos publican progreso, aceptan cancelación y dejan un resultado por elemento. Un fallo individual no invalida un lote completo salvo que la consistencia transaccional lo requiera.

## 11. Copias y restauración

- Copias rotatorias locales después de cambios relevantes y antes de migraciones.
- Exportación ZIP manual con manifiesto versionado, base consistente y preferencias.
- No incluye vídeos ni caché de imágenes descargables.
- Importación primero valida versión, integridad y espacio.
- Si las raíces no existen, un asistente permite mapear antigua→nueva y muestra una simulación.
- Nunca se reemplaza la base activa hasta que la importación se valida completamente.

## 12. Privacidad y seguridad

La aplicación es offline-first. Sin consentimiento, las únicas conexiones son metadatos y actualización solicitados. Los diagnósticos son opt-in; se construyen desde una lista permitida y excluyen rutas, nombres completos, títulos, biblioteca e historial.

Las rutas se normalizan y toda operación de renombrado verifica que origen/destino sigan dentro de la raíz seleccionada. No se ejecutan comandos construidos desde nombres de archivos. Las credenciales NAS pertenecen a Windows y no se almacenan. Los tokens y claves de compilación permanecen en secretos de CI. Dependencias se fijan, analizan y publican en un SBOM.

## 13. Rendimiento

Presupuestos para el hardware mínimo que soporte oficialmente Windows 11:

- La ventana útil aparece en menos de 3 s con una base caliente de 10.000 archivos.
- La búsqueda local devuelve el primer conjunto visible en menos de 150 ms.
- Desplazamiento de biblioteca mantiene 60 FPS en hardware de referencia.
- Ningún escaneo bloquea el hilo de interfaz más de 50 ms.
- El consumo en bandeja permanece inactivo salvo eventos/intervalos configurados.
- El escaneo incremental evita volver a inspeccionar archivos sin cambios.

Los presupuestos se miden con un generador de catálogo de 10.000 elementos y una colección física representativa. Las cifras pueden endurecerse, pero no relajarse sin una decisión registrada.

## 14. Estrategia de pruebas

1. **Unitarias**: nombres, puntuación, identidades, duplicados, estados, progreso, preferencias y reglas de renombrado.
2. **Propiedades/fuzzing**: nombres Unicode/ruidosos, rutas largas, fechas y entradas malformadas.
3. **Integración**: SQLite/migraciones, sistema de archivos falso y real temporal, TMDB simulado, copias/importación y procesos de reproducción simulados.
4. **Contrato**: cada adaptador se prueba contra su interfaz para permitir reemplazo.
5. **Multimedia real**: matriz legal de archivos pequeños con contenedores, códecs, pistas, subtítulos, HDR/SDR y errores.
6. **Interfaz**: componentes, navegación, regresión visual, temas, escalado y localización.
7. **Accesibilidad**: automatización más revisión con teclado/Narrator/alto contraste.
8. **Rendimiento**: 10.000 elementos, NAS lento simulado y reproducción mientras se escanea.
9. **Recuperación**: cierre forzado, unidad retirada, base dañada, migración fallida y actualización interrumpida.
10. **Empaquetado**: instalación limpia, actualización, downgrade rechazado, reparación y desinstalación en x64; ARM64 antes de estable.

Los archivos de vídeo del usuario no se incorporan al repositorio ni a artefactos. Las muestras de prueba deben tener licencia redistribuible o generarse durante la prueba.

## 15. Descomposición de ejecución

Este documento es la especificación maestra del producto, no una orden para implementar todos los subsistemas de una sola vez. La ejecución se divide en incrementos verticales que terminan con software demostrable, pruebas y actualización documental:

1. **Fundación**: solución, límites de arquitectura, CI, localización, tokens visuales, base de datos y contratos.
2. **Biblioteca local**: raíces, escaneo, sonda, identidad, búsqueda, disponibilidad y rendimiento con 10.000 archivos.
3. **Identificación**: analizadores, confianza, TMDB, caché, revisión, duplicados, edición y renombrado.
4. **Reproducción**: LibVLC, pistas, ventanas, HDR/audio y compatibilidad de formatos.
5. **Continuidad**: progreso, estados, cambio de versión, siguiente episodio y marcadores manuales.
6. **Experiencia completa**: Inicio, datos personales, recomendaciones, accesibilidad, bandeja y “Abrir con…”.
7. **Resiliencia y entrega**: copias, importación, privacidad, diagnósticos, actualización y empaquetado x64.
8. **Publicación estable**: ARM64, Store, validación final de marca para AP Reelume, detección automática de segmentos y auditorías finales.

Cada incremento obtiene un plan detallado y puede producir subespecificaciones técnicas cuando una interfaz externa lo exija. No se comienza el siguiente si el actual no cumple sus criterios o si la matriz queda desactualizada.

## 16. Riesgos y mitigaciones

| Riesgo | Impacto | Mitigación y punto de decisión |
|---|---|---|
| Integración Avalonia/LibVLC no cumple superposición, HDR o mini reproductor | Alto | Prototipo técnico temprano con MKV/AVI/HDR y controles superpuestos; `IMediaPlayerEngine` permite sustituir el motor. |
| Token público de TMDB se extrae o agota | Medio | Token de solo lectura inyectado, límites/caché, rotación y modo sin proveedor; reevaluar proxy solo si el uso real lo exige. |
| Eventos de NAS se pierden o duplican | Medio | Escaneo incremental de respaldo, reconciliación idempotente y límites por raíz. |
| Renombrado sobre red falla a mitad | Alto | Prevalidación completa, log por operación, ejecución conservadora y recuperación guiada; nunca simular atomicidad inexistente. |
| Detección automática de segmentos no alcanza calidad | Alto para estable | Corpus y umbral se definen antes de implementarla; un cambio del bloqueo requiere aprobación explícita del alcance. |
| ARM64 carece de una dependencia nativa equivalente | Alto para estable | Verificación de paquetes en la fundación y compilación ARM64 temprana aunque la certificación final sea posterior. |
| Alcance del producto retrasa valor usable | Alto | Incrementos verticales, MVP x64 como límite y funciones posteriores identificadas en matriz. |
| Obligaciones de licencias de códecs/dependencias | Alto | Auditoría, SBOM, avisos y revisión del artefacto exacto antes de cada publicación. |

## 17. Entregas

### MVP x64

Incluye catálogo, detección/revisión, TMDB, duplicados, edición, renombrado seguro, reproductor, progreso, siguiente episodio, marcadores manuales, datos personales, recomendaciones, accesibilidad, copias, bandeja configurable y documentación. Se distribuye como MSIX de prueba y artefacto GitHub con advertencia SmartScreen documentada.

### Primera versión estable

Exige ARM64, Microsoft Store, actualizador con resumen, detección automática de introducciones/créditos validada, comprobación jurídica y de disponibilidad final de **AP Reelume by AP Solutions**, auditorías de accesibilidad/privacidad/licencias y documentación pública completa. La decisión del nombre ya está registrada en ADR-0001, pero la comprobación formal sigue siendo una puerta de publicación. Ninguna función bloqueante puede quedar simplemente “pendiente”: debe estar `VERIFIED` o existir una decisión aprobada que cambie formalmente el alcance.

### Después de estable

Listas personalizadas y otras mejoras priorizadas por uso real. Dolby Vision y passthrough no están comprometidos hasta completar una nueva evaluación.

## 18. Distribución, actualizaciones y costes

El presupuesto obligatorio es cero. Microsoft Store/MSIX es la ruta pública principal y aporta firma/actualización. GitHub aloja código, documentación, CI y compilaciones independientes. Estas últimas pueden mostrar SmartScreen mientras no exista firma de pago; se publican hashes y una explicación visible.

El actualizador independiente descarga en segundo plano, verifica integridad, muestra resumen bilingüe y requiere confirmación. Las migraciones se ejecutan después de crear una copia y antes de reemplazar la instalación activa cuando el empaquetado lo permita.

## 19. Documentación y trazabilidad

Documentos obligatorios, siempre en español e inglés:

- Especificación funcional/técnica.
- `docs/FEATURES.md` como matriz canónica.
- Roadmap por versiones.
- ADR para decisiones arquitectónicas materiales.
- Changelog por versión.
- Manual de usuario y solución de problemas.
- Guía de desarrollo, contribución y publicación.
- Política de privacidad, licencias de terceros y SBOM.

Cada requisito usa ID estable. Un cambio comienza en especificación/matriz, continúa en plan/issue, llega a código/pruebas y termina con evidencia enlazada. La CI valida enlaces, formato y presencia de ambas lenguas.

## 20. Decisiones explícitas para evitar ambigüedad

- El nombre de producto es **AP Reelume**, la presentación completa es **AP Reelume by AP Solutions** y la firma es **by AP Solutions**, según ADR-0001.
- La comprobación formal de marca y disponibilidad en la Store sigue siendo obligatoria antes de publicar; los IDs internos no dependen del nombre público.
- La interfaz sale inicialmente en español, pero la documentación es bilingüe desde el inicio.
- La detección automática de introducciones/créditos no forma parte del MVP, pero sí bloquea la primera estable.
- ARM64 no forma parte del MVP, pero sí bloquea la primera estable.
- El reproductor externo no proporciona seguimiento exacto.
- El renombrado no mueve archivos ni carpetas.
- Los duplicados nunca se borran automáticamente.
- No existe telemetría activada por defecto.
- No existe soporte de cursos, listas personalizadas en MVP, Dolby Vision ni passthrough.

---

# Part II — English specification

## 1. Vision and expected outcome

**AP Reelume by AP Solutions** is a local movie and TV library for one person on one PC. It catalogs existing videos without copying them, automatically identifies movies and episodes, plays a broad range of formats, and remembers the exact position. It requires no account or synchronization.

The first delivery is an installable, genuinely usable x64 MVP. The first stable public release adds publishing requirements, ARM64, and automatic intro/credits detection marked as release blockers in the feature matrix. The product is free, open source, and requires no mandatory proprietary backend.

### Success indicators

1. A real folder with heterogeneous names produces a correct catalog and separates ambiguous cases.
2. An interrupted session resumes within ±5 seconds of the last persisted point.
3. A 10,000-file library remains navigable and searchable without blocking the UI.
4. Disconnecting USB or NAS storage never deletes metadata or progress.
5. Restoring a backup to different paths recovers personal data and remaps roots.
6. All essential actions work with keyboard and Narrator without critical defects.
7. No release is called complete without updating the feature matrix and linking evidence.

## 2. Scope

### Included

- Movies and TV shows; not generic videos, courses, or arbitrary collections.
- Internal, USB, and UNC/NAS folders, always using files in their original location.
- Scan when adding a root, on startup, and on demand; continuous watching where reliable.
- Local filename/folder identification enriched through TMDB.
- Ambiguous-match review, duplicates, manual corrections, and optional safe rename.
- Embedded player, external fallback, progress, statuses, and next episode.
- Favorites, watch later, personal rating, and locally calculated recommendations.
- Local backup/restore, export/import, and application updates.
- Modern Fluent, accessible, internationalization-ready UI.

### Excluded from the MVP

- Accounts, profiles, synchronization, and cloud storage.
- Copying or moving videos into an application-managed structure.
- Multiple simultaneous videos.
- Personal timeline bookmarks, notes, or screenshots.
- Custom lists; they are `POST_STABLE`.
- Dolby Vision and Dolby/DTS passthrough; they require a new evaluation.
- macOS/Linux; architecture keeps them possible, but they are not current deliverables.

## 3. Technology decisions

| Area | Decision | Reason |
|---|---|---|
| Language | C# on .NET 10 LTS | Performance, type safety, ecosystem, and active support through 2028. |
| UI | Avalonia 12.1 with XAML and MVVM | Modern Fluent UI, accessibility, and a cross-platform path without weakening Windows-first. |
| Video | Stable LibVLCSharp 3 + VideoLAN LibVLC | Broad format matrix, acceleration, HDR, and API control. |
| Data | SQLite, WAL, migrations, and FTS5 | Robust, transactional, portable, searchable local file. |
| Metadata | Cached TMDB adapter | Rich Spanish data and a replaceable provider. |
| Packaging | MSIX for Store; independent GitHub artifacts | Safe installation/updates with zero mandatory cost. |
| License | GPL-3.0-or-later | Preserves the open nature of the product and matches the free-component design, subject to final audit. |

Versions are pinned in dependency files and updated through reviewed decisions. LibVLC sits behind `IMediaPlayerEngine`; TMDB behind `IMetadataProvider`; SQLite behind repositories. These choices never leak into the domain.

The TMDB read credential is never committed. Official builds inject it from CI secrets into an application resource that is extractable but restricted in scope; local builds accept a developer-supplied token. The application handles rate limits, provides attribution, and remains usable without the provider. The MVP introduces no custom proxy.

## 4. Architecture

### Dependency rule

```text
Presentation (Avalonia/MVVM)
            ↓
Application (use cases and orchestration)
            ↓
Domain (entities, values, policies, and interfaces)
            ↑
Infrastructure (SQLite, files, TMDB, LibVLC, Windows)
```

`Presentation`, `Infrastructure`, and the Windows host depend on internal contracts; the domain knows none of them. Application events communicate long-running scan, matching, and playback-session changes without exposing threads or framework objects.

### Modules

1. **Catalog**: movies, shows, seasons, episodes, file versions, and queries.
2. **Discovery**: roots, scans, watchers, fingerprints, and availability.
3. **Identification**: filename parsers, candidates, confidence, and review.
4. **Metadata**: TMDB, cache, language, artwork, and manually locked fields.
5. **Playback**: engine, tracks, output, HDR, speed, volume, windows, and failures.
6. **Continuity**: position, status, minimal history, threshold, and next episode.
7. **Personalization**: favorites, watch later, ratings, and local recommendations.
8. **Backup**: rotating backups, export, import, integrity, and remapping.
9. **WindowsIntegration**: MSIX, tray, startup, media keys, “Open with…”, and Mica.

Each module exposes narrow commands/queries and never lets a view access SQLite, TMDB, or the file system directly.

## 5. Data model

### Main entities

- `LibraryRoot`: normalized path, local/USB/UNC kind, availability, scan policy, and last result.
- `Title`: shared movie/show identity, external provider ID, localized titles, artwork, and locked fields.
- `Season` and `Episode`: order, standard/absolute/special numbers, and localized metadata.
- `MediaFile`: path, volume identity, file ID where available, lightweight fingerprint, size, duration, tracks, codecs, and availability.
- `MediaVersion`: connects one or more files to a movie/episode and selects the preferred version.
- `MatchCandidate`: candidate content, score, signals, explanation, and review status.
- `WatchState`: content, position, observed duration, status, threshold, dates, and manual override.
- `PlaybackPreference`: languages, subtitles, speed, volume boost, and output at global/show/file scopes.
- `PersonalState`: favorite, watch later, and rating.
- `IntroMarker`: manual or detected ranges, origin, confidence, and user correction.
- `RenameOperation`: preview, source/destination, conflicts, result, and undo data.

### Identity and movement

On NTFS, the volume and file identifiers are used when available. USB/NAS or file systems without stable identity use a lightweight fingerprint based on size, duration, technical metadata, and bounded byte samples. Normal scans never fully hash every video.

An exact match restores the previous entity. A likely match asks before merging. A new path does not remove the old record until reconciliation completes.

### Persistence

- SQLite uses WAL, foreign keys, and explicit indexes.
- FTS5 indexes titles, alternate titles, cast, and genres; never private paths.
- Schema changes have forward migrations and automatic pre-migration backup.
- Artwork cache is separate and regenerable.
- Personal data and manual locks are always exported.

## 6. Discovery and identification

### Scan flow

1. Enumerate with cancellation and concurrency limits, recording errors per root.
2. Filter supported extensions without opening unnecessary files.
3. Compare size, date, and identity with the previous index.
4. Probe only new or changed files through the media-probe adapter.
5. Parse folder/name rules such as `S01E02`, `1x02`, `Cap.803`, written season names, years, and bracketed tags.
6. Generate local candidates; query TMDB only when useful and online.
7. Combine title, season, episode, year, and duration into an explainable score.
8. Apply results: ≥90% automatic; 60–89% suggested review; <60% pending.
9. Reconcile missing files, moves, and duplicates without deleting data.

Compact `Cap.803` means season 8, episode 3 when context supports it. `Cap.800` is ambiguous/special and enters review. Two `5x10` files become versions of the same episode.

### Watching

Local roots use debounced events. USB/UNC uses watching when reliable and fallback startup/manual scans. Per-root limits prevent saturating NAS storage. An inaccessible root preserves catalog content and exposes an actionable error.

### Metadata

Spanish is preferred with a configurable fallback language. Cache entries carry normalized data, date, and provider version. User-edited/locked fields are not overwritten. Required attribution appears in About/Credits.

## 7. Playback and continuity

### Session start

1. Resolve the preferred available version.
2. Validate path and permissions without deleting the item on failure.
3. Prepare LibVLC with acceleration and applicable preferences.
4. If valid progress exists, offer resume or restart.
5. Apply audio, subtitles, speed, volume, and output.

### Saving

Position saves every five seconds and on pause, seek, mode change, file change, and close. The atomic write includes observed duration and file version. Trivial near-start positions are ignored and position is clamped to a valid range.

Content becomes “in progress” after meaningful advancement and “watched” at 90% by default. The threshold is configurable and manual watched/unwatched controls exist. Manual overrides win until the user reverses them.

### Version switching

Equivalent durations preserve the second within a safe tolerance. Compatible editions with different durations use proportional transfer. Large differences or different structures require confirmation. Progress belongs to content while retaining source-file information for audit.

### Controls

- Play/pause, seek, configurable skips, volume, and mute.
- Playback speed.
- Boost over 100% with peak limiter and visible warning.
- Internal audio/subtitles and external SRT, ASS, and VTT.
- Global preferences overridden per show or file.
- HDR10, SDR tone mapping, and acceleration with indicator/fallback.
- Stereo and 5.1/7.1 with selectable device.
- Fullscreen and mini player; only one active session.
- Media keys and configurable shortcuts.

External playback is an emergency fallback; the app does not claim exact position tracking. Episode completion shows a cancelable countdown and plays the next available episode. Manual intro/credits markers ship in MVP; automatic detection blocks stable release according to the matrix criterion.

## 8. UI and accessibility

The approved direction is modern Fluent on Avalonia: Mica/acrylic surfaces where Windows permits, calm blue, readable hierarchy, and desktop density. Home uses a hybrid pattern with prominent resume, in-progress content, and immediate library access.

Main views:

1. First-run folder and permission onboarding.
2. Home.
3. Searchable/filterable/sortable library.
4. Movie details.
5. Show/season/episode details.
6. Player.
7. Match/duplicate review inbox.
8. Metadata/rename editor.
9. Backup and restore.
10. Settings, privacy, credits, and updates.

Theme follows Windows with manual light/dark overrides; the player remains dark. Initial UI is translated into Spanish and all visible text comes from resources. Documentation is maintained in Spanish and English.

Accessibility requires full keyboard navigation, visible focus, names/roles/states for assistive technology, Narrator, text scaling, high contrast, reduced motion, reasonable touch targets, and subtitle customization. Color is never the only state signal.

## 9. Recommendations and personal data

Favorites, watch later, and ratings are local and included in backups. Recommendations use only local data, provide a simple explanation, and can be disabled. No remote profile or watch history is sent. Custom lists arrive after first stable without changing `PersonalState` semantics.

## 10. Failure and recovery

| Failure | Behavior | Recovery |
|---|---|---|
| USB/NAS disconnected | Keep catalog and mark unavailable | Revalidate on reconnect/scan |
| Access denied | Continue other roots and show path/action | Retry after fixing permissions |
| TMDB down/rate-limited | Use cache and keep local candidates | Backoff and manual retry |
| Corrupt file | Keep entity; show diagnosis | Other version, retry, or external player |
| Media engine failure | Persist last point and release session | Restart engine or open externally |
| Unexpected close | Lose at most the recent interval | WAL and startup integrity check |
| Migration failure | Never replace valid database | Restore pre-migration backup and abort update |
| Damaged database | Do not overwrite backup | Guided repair or restore |
| Rename conflict | Do not run a partial batch | Fix preview and retry |

Long jobs publish progress, accept cancellation, and retain per-item outcomes. One item failure never invalidates a whole batch unless transactional consistency requires it.

## 11. Backup and restore

- Rotating local backups after significant changes and before migrations.
- Manual ZIP export with versioned manifest, consistent database, and preferences.
- No videos or downloadable artwork cache.
- Import validates version, integrity, and space first.
- Missing roots open an old→new mapping wizard with a dry-run preview.
- Active data is never replaced until import fully validates.

## 12. Privacy and security

The application is offline-first. Without consent, only requested metadata/update network calls occur. Diagnostics are opt-in, allowlist-built, and exclude paths, full filenames, titles, library, and history.

Paths are normalized and rename operations verify that source/destination stay within selected roots. No command is constructed from filenames. NAS credentials belong to Windows and are never stored. Build tokens stay in CI secrets. Dependencies are pinned, scanned, and published in an SBOM.

## 13. Performance

Budgets on reference hardware meeting the official Windows 11 minimum:

- Useful window appears within 3 seconds with a warm 10,000-file database.
- Local search returns the first visible result set within 150 ms.
- Library scrolling sustains 60 FPS on reference hardware.
- No scan blocks the UI thread for more than 50 ms.
- Tray mode remains idle outside configured events/intervals.
- Incremental scan never reprobes unchanged files.

Budgets are measured with a generated 10,000-item catalog and a representative physical collection. They may be tightened but not relaxed without a recorded decision.

## 14. Test strategy

1. **Unit**: names, scoring, identities, duplicates, states, progress, preferences, and rename rules.
2. **Property/fuzz**: noisy Unicode names, long paths, dates, and malformed inputs.
3. **Integration**: SQLite/migrations, fake and temporary real file systems, mocked TMDB, backup/import, and simulated playback processes.
4. **Contract**: every adapter against its interface to preserve replaceability.
5. **Real media**: legally redistributable small samples covering containers, codecs, tracks, subtitles, HDR/SDR, and failures.
6. **UI**: components, navigation, visual regression, themes, scaling, and localization.
7. **Accessibility**: automation plus keyboard/Narrator/high-contrast review.
8. **Performance**: 10,000 items, simulated slow NAS, and playback during scanning.
9. **Recovery**: forced close, removed drive, damaged DB, failed migration, and interrupted update.
10. **Packaging**: clean install, update, rejected downgrade, repair, and uninstall on x64; ARM64 before stable.

User videos never enter the repository or artifacts. Test media must be redistributable or generated during tests.

## 15. Execution decomposition

This document is the product master specification, not an instruction to implement every subsystem at once. Execution is split into vertical increments that end with demonstrable software, tests, and updated documentation:

1. **Foundation**: solution, architecture boundaries, CI, localization, visual tokens, database, and contracts.
2. **Local library**: roots, scan, probe, identity, search, availability, and 10,000-file performance.
3. **Identification**: parsers, confidence, TMDB, cache, review, duplicates, editing, and rename.
4. **Playback**: LibVLC, tracks, windows, HDR/audio, and format compatibility.
5. **Continuity**: progress, statuses, version switching, next episode, and manual markers.
6. **Complete experience**: Home, personal data, recommendations, accessibility, tray, and “Open with…”.
7. **Resilience and delivery**: backup, import, privacy, diagnostics, updates, and x64 packaging.
8. **Stable publication**: ARM64, Store, final AP Reelume trademark clearance, automatic segment detection, and final audits.

Every increment receives a detailed plan and may produce technical subspecifications when an external interface requires one. The next increment does not begin while the current one misses its criteria or the matrix is stale.

## 16. Risks and mitigations

| Risk | Impact | Mitigation and decision point |
|---|---|---|
| Avalonia/LibVLC integration misses overlay, HDR, or mini-player needs | High | Early technical spike with MKV/AVI/HDR and overlay controls; `IMediaPlayerEngine` permits engine replacement. |
| Public TMDB token is extracted or exhausted | Medium | Injected read-only token, limits/cache, rotation, and provider-free mode; reconsider a proxy only if real usage demands it. |
| NAS events are lost or duplicated | Medium | Incremental fallback scan, idempotent reconciliation, and per-root limits. |
| Network rename fails midway | High | Full prevalidation, per-operation log, conservative execution, and guided recovery; never pretend unavailable atomicity. |
| Automatic segment detection misses quality target | High for stable | Corpus and threshold are defined before implementation; changing the blocker requires explicit scope approval. |
| ARM64 lacks an equivalent native dependency | High for stable | Verify packages during foundation and build ARM64 early even if final certification comes later. |
| Product scope delays usable value | High | Vertical increments, x64 MVP boundary, and later features identified in the matrix. |
| Codec/dependency license obligations | High | Audit, SBOM, notices, and exact-artifact review before every publication. |

## 17. Deliveries

### x64 MVP

Includes catalog, detection/review, TMDB, duplicates, editing, safe rename, player, progress, next episode, manual markers, personal data, recommendations, accessibility, backups, configurable tray, and documentation. It ships as a test MSIX and GitHub artifact with documented SmartScreen warning.

### First stable release

Requires ARM64, Microsoft Store, updater with summary, validated automatic intro/credits detection, final legal and availability clearance for **AP Reelume by AP Solutions**, accessibility/privacy/license audits, and complete public documentation. The naming decision is recorded in ADR-0001, while formal clearance remains a release gate. A blocking feature cannot remain informally “pending”: it must be `VERIFIED` or an approved scope decision must formally change the release.

### Post-stable

Custom lists and improvements prioritized from real usage. Dolby Vision and passthrough remain uncommitted until a new evaluation completes.

## 18. Distribution, updates, and costs

Mandatory budget is zero. Microsoft Store/MSIX is the main public route and provides signing/update handling. GitHub hosts code, docs, CI, and independent builds. Those builds may trigger SmartScreen without paid signing; hashes and a visible explanation are published.

The independent updater downloads in the background, verifies integrity, displays a bilingual summary, and requires confirmation. Migrations run after backup and before the active installation is replaced where packaging permits.

## 19. Documentation and traceability

Required documents, always Spanish and English:

- Functional/technical specification.
- `docs/FEATURES.md` as the canonical matrix.
- Versioned roadmap.
- ADRs for material architecture decisions.
- Per-release changelog.
- User and troubleshooting manuals.
- Development, contribution, and release guides.
- Privacy policy, third-party notices, and SBOM.

Every requirement uses a stable ID. A change begins in specification/matrix, continues through plan/issue, reaches code/tests, and finishes with linked evidence. CI validates links, format, and both languages.

## 20. Explicit decisions that prevent ambiguity

- The product name is **AP Reelume**, the full presentation is **AP Reelume by AP Solutions**, and the publisher signature is **by AP Solutions**, as recorded in ADR-0001.
- Formal trademark and Store availability clearance remains mandatory before publication; internal IDs do not depend on the public name.
- Initial UI ships in Spanish; documentation is bilingual from day one.
- Automatic intro/credits detection is outside MVP but blocks first stable.
- ARM64 is outside MVP but blocks first stable.
- External playback does not provide exact tracking.
- Rename never moves files or folders.
- Duplicates are never automatically deleted.
- Telemetry is never enabled by default.
- Courses, MVP custom lists, Dolby Vision, and passthrough are unsupported.

## 21. References / Referencias

- [Windows application platform](https://learn.microsoft.com/en-us/windows/apps/)
- [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)
- [Avalonia 12.1 release](https://avaloniaui.net/blog/release-12-1)
- [LibVLCSharp](https://github.com/videolan/libvlcsharp)
- [Microsoft Store code signing](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/code-signing-options)
- [TMDB API FAQ and attribution](https://developer.themoviedb.org/docs/faq)
