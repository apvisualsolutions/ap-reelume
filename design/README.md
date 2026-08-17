# Handoff: AP Reelume — rediseño completo de las 48 superficies

## Resumen

AP Reelume es una **biblioteca de medios local para Windows 11**: cataloga y reproduce vídeos que ya están en el disco del usuario. Sin cuentas, sin telemetría por defecto, sin servidor, sin streaming. GPL-3.0-or-later.

Este paquete contiene el rediseño de **las 48 vistas** del árbol, más las 8 superficies que no son `.axaml` (bandeja, diálogos del sistema, Explorador, arranque MSIX, barra de título), los tokens en los cuatro temas, el inventario de controles con su delta, y las cadenas nuevas en los dos idiomas.

## ⚠ Este caso NO es «recrear el HTML en un framework»

Lo habitual en un handoff es elegir un framework e implementar el diseño. **Aquí no.** Existe un codebase real y el trabajo es implementar dentro de él:

- **Repositorio**: `apvisualsolutions/ap-reelume`, rama `codex/ap-reelume-mvp-x64`
- **Stack**: C# + **Avalonia 12.1.1** (AXAML). *Ojo: el encargo original decía Avalonia 11; `Directory.Packages.props` pina 12.1.1.*
- **Motor de reproducción**: LibVLCSharp 3.10.0 tras un contrato sustituible
- **Datos**: SQLite (Microsoft.Data.Sqlite)

Los `.dc.html` de este paquete son **referencias de diseño**: prototipos que muestran aspecto y comportamiento previstos. No son código para copiar. La implementación va en AXAML, siguiendo los patrones que el árbol ya establece.

## Fidelidad

**Alta (hifi).** Colores, tipografía, espaciado, estados y copia son definitivos y están calculados contra el árbol real: los hex salen de `DesignTokens.axaml`, los textos de `Strings.es.axaml` / `Strings.en.axaml`, y los ratios de contraste están computados con la fórmula WCAG 2.1.

## Restricciones que el repositorio hace cumplir con pruebas

Estas no son preferencias. Romperlas rompe pruebas o reintroduce defectos ya medidos.

1. **Bilingüe.** Toda cadena visible existe en `Strings.es.axaml` y `Strings.en.axaml`, por `DynamicResource`. Una cadena nueva va en los dos archivos o no va. Los únicos literales legítimos son símbolos: `○ ◐ ●`, `→`, `!`.
2. **Nombre accesible en cada control interactivo**, con 80 pruebas que lo exigen y un paseo automático que pulsa los controles y los identifica **por su clave de recurso**. En este árbol `Content` y `AutomationProperties.Name` apuntan a la misma clave: **reescribir la etiqueta de un botón es renombrar el control.**
3. **Cuatro temas**: claro, oscuro, alto contraste claro, alto contraste oscuro. Más un ajuste de movimiento reducido que hay que honrar.
4. **Geometría real.** Ventana de referencia 1600×1000. La columna lateral del reproductor es de **320 px fijos**. Dos formas han causado defectos medidos: (a) fila horizontal con etiqueta de anchura libre junto a un botón — empujó controles hasta x=1674, x=1737, x=1611 y x=2146; (b) panel superpuesto sin alineación explícita — se estiró a 1280×1400 y a 1280×1200, opaco sobre el transporte, comiéndose cada clic. **Regla:** toda fila de acciones es `WrapPanel ItemSpacing/LineSpacing`; todo panel superpuesto declara alineación **y acota las dos dimensiones** (`MaxWidth` *y* `MaxHeight`).
5. **Sin dependencias externas.** Ni fuentes descargadas, ni iconos de CDN, ni imágenes remotas. La tipografía usa lo que Windows 11 trae de serie; los iconos son glifos de Segoe Fluent Icons.

## Los cuatro documentos de diseño

Léelos en este orden:

| Archivo | Qué contiene |
| --- | --- |
| `Auditoría del inventario - AP Reelume.dc.html` | Qué faltaba por nombrar antes de diseñar: 7 superficies que no son `.axaml`, 181 enlaces `IsVisible` frente a una nota condicional, 15 listas con 4 estados vacíos escritos, los temas que no se pueden aplicar |
| `Propuesta de diseño - AP Reelume.dc.html` | Tokens en los 4 temas con ratios calculados, tipografía y espaciado, los 5 estados de cada control, vista por vista, los activos de instalación, y la compatibilidad AXAML con sus dos excepciones |
| `Inventario de controles - AP Reelume.dc.html` | El delta del trinquete: **128 → 202 controles**, añadidos por superficie, renombrados, eliminados. Y las 8 superficies del sistema especificadas |
| `Cadenas nuevas - AP Reelume.dc.html` | **470 → 517 claves por idioma**: 22 de estados vacíos + 25 de consecuencia, con su texto en los dos idiomas. Y las 16 frases que NO son cadenas, con el archivo donde van como comentario |
| `Catálogo de elementos - AP Reelume.dc.html` | Cada elemento en sus estados, el reproductor completo, y las 4 animaciones del proyecto corriendo. Ordenado por elemento, no por pantalla: la lista «en:» de cada ficha es el alcance de un cambio |

El prototipo navegable es `AP Reelume.dc.html`. Su panel **Demostración** recorre 27 estados, incluidos los que ninguna ruta alcanza: primer arranque, base de datos que no abre, los 7 motivos de fallo del reproductor, sin duplicados, sin resultados.

## Tokens

Aterrizan en las claves que ya existen en `Theme/DesignTokens.axaml`. Punto de partida medido: **58 declaraciones / 40 nombres** + 3 en `Resources/Brand.axaml`. Después de la propuesta: **133 declaraciones / 70 nombres**.

### Las nueve brochas de tema, en los cuatro diccionarios

| Clave | Claro | Oscuro | AC claro *(nuevo)* | AC oscuro |
| --- | --- | --- | --- | --- |
| `ShellSurfaceBrush` | `#F8FAFC` | `#111827` | `#FFFFFF` | `#000000` |
| `NavigationSurfaceBrush` | `#EEF3F7` | `#172033` | `#FFFFFF` | `#000000` |
| `CardSurfaceBrush` | `#FFFFFF` | `#1F2937` | `#FFFFFF` | `#000000` |
| `ControlFillBrush` | `#E2E8F0` | `#1F2937` | `#FFFFFF` | `#000000` |
| `ShellBorderBrush` | `#64748B` | `#94A3B8` | `#000000` | `#FFFFFF` |
| `TextPrimaryBrush` | `#111827` | `#F8FAFC` | `#000000` | `#FFFFFF` |
| `FocusStrokeBrush` | `#005A9C` | `#7CC4FF` | `#000000` | `#FFFF00` |
| `AccentBrush` | `#1769AA` | `#62AEE8` | `#0000FF` | **`#00FFFF`** ← cambia |
| `AccentSubtleBrush` | `#DCEAF6` | `#203B55` | `#FFFFFF` | `#000000` |

**El único valor que cambia de los existentes.** Hoy en alto contraste `AccentBrush` y `FocusStrokeBrush` son el mismo `#FFFF00`: foco y marca indistinguibles justo en el tema donde el foco más importa. El acento pasa a `#00FFFF` (16,75:1 sobre negro) y el amarillo queda reservado al foco. **Y `--warn-fg` también debe salir del amarillo** en ese tema, por lo mismo.

### Doce brochas nuevas

| Clave | Claro | Oscuro | AC | Ratio |
| --- | --- | --- | --- | --- |
| `TextSecondaryBrush` | `#475569` | `#94A3B8` | = primario | 7,24 / 6,92 |
| `ControlFillHoverBrush` | `#D6DFEA` | `#2A3648` | inversión | 13,18 / 11,67 |
| `ControlFillPressedBrush` | `#C4D0DF` | `#354357` | inversión | ≥ 11,0 |
| `ControlFillDisabledBrush` | `#EEF3F7` | `#172033` | = relleno | — |
| `TextDisabledBrush` | `#64748B` | `#7A8AA0` | = primario | 4,26 / 4,63 |
| `FocusInnerStrokeBrush` | `#FFFFFF` | `#111827` | `#FFFFFF` / `#000000` | ver foco |
| `ShellHairlineBrush` | `rgba(15,23,42,.09)` | `rgba(255,255,255,.07)` | = borde | decorativo |
| `WarningSurfaceBrush` | `#FCF0D0` | `#3A2E0B` | = superficie | 15,64 / 12,74 |
| `WarningBorderBrush` | `#8A6100` | `#E8B84B` | = borde | 4,88 / 7,23 |
| `DangerSurfaceBrush` | `#FDECEA` | `#3B1A17` | = superficie | 15,51 / 14,91 |
| `DangerBorderBrush` | `#B3261E` | `#F2B8B5` | = borde | 5,72 / 9,14 |
| `PositiveSurfaceBrush` | `#E4F2E9` | `#12301F` | = superficie | 15,35 |
| `PositiveBorderBrush` | `#14653B` | `#7BD3A0` | = borde | 6,15 / 7,95 |

**Hallazgo importante sobre los bordes.** Los hairlines del prototipo (`#3A424F` oscuro, `#B7C2CF` claro) dan **1,80:1** y **1,81:1** — perfectos para separar tarjetas, insuficientes para delimitar algo pulsable, que necesita 3:1. De ahí el token partido en dos: `ShellHairlineBrush` separa superficies, `ShellBorderBrush` delimita controles (`#6B7484` oscuro = 3,88:1; `#7E8B9C` claro = 3,47:1).

**En los dos temas de alto contraste el color no distingue nada.** Advertencia, fallo y éxito comparten superficie y borde: los separa el **glifo** y el **encabezado**. Si funciona ahí, funciona para quien no distingue el ámbar del rojo.

### Escalares

`FocusStrokeThickness` 2 · `FocusInnerStrokeThickness` 1 (nuevo) · `ControlHeight` 36 · `SpaceXSmall` 4 (nuevo) · `SpaceSmall` 8 · `SpaceMedium` 16 · `SpaceLarge` 24 · `SpaceXLarge` 32 (nuevo — ya existe como `Padding="32"` literal) · `CornerRadiusSmall` 4 · `CornerRadiusMedium` 8 · `MotionDurationStandardMilliseconds` 160 · `MotionDurationReducedMilliseconds` 0 · `SelectedStateGlyph` `●`

## Tipografía

Todo de serie en Windows 11. Precedente en el árbol: `SubtitleStyleViewModel.SafeFontFamilies` ya es una lista blanca de fuentes del sistema.

| Clave | Valor |
| --- | --- |
| `FontFamilyUI` | Segoe UI Variable Text, Segoe UI |
| `FontFamilyDisplay` | Segoe UI Variable Display, Segoe UI — ≥ 20 px |
| `FontFamilyMono` | Cascadia Mono, Consolas — rutas, hashes, códecs, el `→` del renombrado |
| `FontFamilyIcons` | Segoe Fluent Icons |
| `FontSizeDisplay` / `Title` / `Subtitle` | 32 / 28 / 20 — el 28 y el 20 **ya son los valores del árbol** |
| `FontSizeBody` / `Caption` / `Mono` | 14 / 12 / 13 |

El antetítulo (`10,5 px`, `letter-spacing .18em`, mayúsculas, `TextSecondaryBrush`) es el recurso del prototipo para etiquetar secciones sin gastar un nivel de encabezado.

## Los cinco estados de cada control

| Estado | Claro y oscuro | Alto contraste |
| --- | --- | --- |
| Reposo | `ControlFillBrush`, borde 1 px `ShellBorderBrush`, alto 36 | Igual; el borde es la única forma |
| Sobre | Relleno → `ControlFillHoverBrush`. Sin mover nada de sitio | **Inversión**: relleno ← borde, texto ← superficie |
| Pulsado | Relleno → `ControlFillPressedBrush`. Sin desplazamiento ni escala | Inversión + borde 2 px |
| **Con foco** | **Anillo doble** — ver abajo. Idéntico en los cuatro temas | Idéntico |
| Deshabilitado | `ControlFillDisabledBrush` + `TextDisabledBrush` + **borde punteado 1 px** | Sólo el borde pasa a punteado |

### El foco, sin depender del color

Hoy el foco se dibuja poniendo `BorderBrush` = `FocusStrokeBrush` y `BorderThickness` = 2, en **ocho** tipos de control (`Button`, `ToggleButton`, `TextBox`, `ComboBox`, `CheckBox`, `Slider`, `NumericUpDown`, `ListBoxItem`). Dos agujeros medidos: en alto contraste claro el color del foco y el del borde son **el mismo negro**, así que sólo cambia un píxel de grosor; y un `Slider` no tiene borde visible donde pintarlo.

**Anillo doble:** borde exterior 2 px en `FocusStrokeBrush` + anillo interior 1 px en `FocusInnerStrokeBrush` (siempre el color de la superficie). Tres bordes concéntricos donde había uno: la señal es la **geometría**, no el tono, y sobrevive a un tema donde todos los colores son el mismo negro. Es además el patrón del propio rectángulo de foco de Windows 11.

En AXAML sale con `BoxShadow` de *spread* en un `Border` (`0 0 0 1 <interior>, 0 0 0 3 <foco>`) o con dos `Border` anidados.

**Los ocho selectores suben a diez:** faltan `ToggleSwitch:focus` y `RadioButton:focus`, que hoy caen al foco del tema base, que ninguna comprobación cubre — el propio comentario de `DesignTokens.axaml` advierte de esto.

### El borde punteado del deshabilitado — la excepción de AXAML

`Border` **no tiene trazo discontinuo**. Se hace con un `Rectangle` con `StrokeDashArray` superpuesto en la plantilla del control. Merece el rodeo: es la señal que separa *deshabilitado* de *ausente* sin pedirle a nadie que compare dos grises.

## Ausente ≠ deshabilitado

La distinción más importante del rediseño, y la que el repositorio ya modela en `PrivacySettingsView` (LIB-016):

- **Ausente**: el control **no existe**. No deja hueco ni sombra. El interruptor de refresco automático no existe sin conexión consentida, porque ofrecerlo sería ofrecer algo que no puede ocurrir.
- **Deshabilitado**: el control existe y se pone gris con borde punteado. Exportar diagnósticos existe siempre.

**Las dos gramáticas conviven en la misma pantalla**, y por eso hay que distinguirlas visualmente.

Superficies condicionales que hay que pintar en sus dos formas: los 8 bloques de Ajustes de `ShellView`, los 5 paneles opcionales de la columna del reproductor, `HasPreview` en privacidad, los 4 estados de `RootOnboardingView`, `LooseFileBanner`, los 3 mensajes de `MetadataEditorView`, `RecommendationsRailView` (vacío ≠ apagado por ajuste).

## Los cuatro tonos de estado

Una gramática, cuatro tonos, y **el rechazo no es un fallo**:

| Tono | Cuándo | Glifo |
| --- | --- | --- |
| Neutro | Proceso en curso: comprobando, descargando, copiando, escaneando | `○` |
| Positivo | Al día, copia terminada, restaurado, **bandeja de revisión vacía**, sin duplicados | `✓` |
| **Advertencia** | Los **8 motivos de rechazo** del actualizador, no disponible, USB fuera, caída a software, raíz sin reasignar, rango solapado | `!` |
| Error | Los **7 motivos de fallo** del reproductor, descarga interrumpida, base que no abre, borrado destructivo | `✕` |

**Un rechazo es el actualizador negándose a instalar algo que no pudo verificar.** Va en ámbar con el **motivo como encabezado por encima del estado** — el motivo es la noticia, el estado es el contexto. Y todo dentro de **un solo** `Border` con `LiveSetting="Polite"`: partirlo en dos cajas parte el anuncio al lector de pantalla.

**Assertive, no Polite:** `RootOnboardingView` tiene dos zonas vivas `Assertive` — el fallo al añadir y la confirmación de borrado. Un fallo y una confirmación destructiva **interrumpen**; el estado de una copia espera su turno.

## Vista por vista

Está en la §4 de `Propuesta de diseño`, con las 48 filas y sus estados condicionales. Los puntos que más trabajo llevan:

- **`UpdateView`** — 23 mensajes (15 estados + 8 motivos de rechazo) en cuatro gramáticas donde hoy hay una. Los dos resúmenes bilingües se apilan a propósito, para poder citar la nota de versión.
- **`PlayerView`** — 7 motivos de fallo con acciones **condicionadas por motivo**: «reintentar» no se ofrece sin motor ni tras fallar el lanzamiento externo. «Elegir otra versión» es un flag **independiente** del motivo, como lo modela `CanChooseAnotherVersion`.
- **`RestoreWizardView`** — la reasignación de raíces: sólo la raíz ausente gana campo editable, y su estado cambia a «Reasignada» al escribir.
- **`DatabaseRecoveryView`** — no gana ruta desde el shell (una pantalla sin retorno no puede ser un destino), respeta la barra de título, y el detalle del fallo sale del color de marca.
- **`MiniPlayerWindow`** — hoy son diez líneas con un `Panel Background="Black"` y **cero controles**. Gana cinco, los cinco con `pl.pbtn` (36×36, radio 8): no hereda el círculo de 52 px de la pausa del reproductor grande, porque ahí es la acción primaria y aquí es uno de cinco iguales.

## Animaciones

El conducto ya existe: `IReducedMotionService` → `MotionDuration`, 160 ms o 0. **Las cuatro del prototipo:**

| Nombre | Qué hace | Dónde |
| --- | --- | --- |
| `apr-in` | Opacidad + 6 px hacia arriba, 160 ms ease-out | Cada cambio de pantalla del shell |
| `apr-shim` | Brillo recorriendo el esqueleto, 1,35 s en bucle | Cuadrícula de Biblioteca e Inicio mientras carga |
| `apr-tip` | Tooltip entrando 6 px desde la izquierda | Los 6 destinos del riel |
| `apr-pulse` | Pulso de opacidad | Punto del escaneo |

Más la transición `left 160ms` de la manija del interruptor. **La barra de progreso no se anima**: su relleno es el progreso real.

En Avalonia el brillo del esqueleto no puede animar `background-position`: es un rectángulo que se mueve dentro de un recorte.

**Movimiento reducido las lleva a 0 ms, no las acorta.**

## Delta del trinquete: 128 → 202 controles

El detalle está en `Inventario de controles`. Resumen: **85 controles fijos añadidos** + 4 familias «por elemento», **−15 sustituidos** (9 píldoras de velocidad → menú, 3 `ComboBox` → desplegables, 3 botones de tema → 4 píldoras). Neto **+74**.

- **Renombrados: 0 controles.** Deliberado: ninguna etiqueta existente se reescribe. Sí hay un renombrado de diccionario: `AppThemeVariants.HighContrast` → `HighContrastDark`, más `HighContrastLight`. Toca `AppThemeVariants`, `ThemePreference` y `FluentThemeService`.
- **Eliminados: 2 controles**, los dos duplicados. En Copias había un segundo **«Restaurar» habilitado siempre** que se saltaba la elección del archivo y la reasignación de raíces.
- **Un control cambia de tipo**: `PlayerRecoveryChooseAnotherVersion` es un `TextBlock` en el AXAML y pasa a botón.
- **No son controles**: las 30 fichas de demostración del prototipo. Si se contaran, el inventario saltaría a 232 sin que exista ninguno.

Cada control nuevo necesita su prueba de nombre accesible y su línea en el paseo, **en el mismo cambio**.

## Cadenas: 470 → 517 por idioma

En `Cadenas nuevas`, con su texto en español e inglés. 22 de estados vacíos + 25 de consecuencia. Más **16 frases que NO son cadenas** y van como comentario, con el archivo indicado.

**La regla:** si la frase ayuda a decidir o a actuar, se traduce; si explica por qué está diseñada así, es un comentario del AXAML.

## Activos de instalación — BLOQUEADO

Los cinco PNG de `src/ApSolutions.LocalMedia.Windows.Package/Assets/` son marcadores de posición (576 B a 7 KiB). Y cinco archivos no son cinco activos: MSIX escala cada uno.

| Activo | Escalas 100/125/150/200/400 | Además |
| --- | --- | --- |
| `Square44x44Logo` | 44 · 55 · 66 · 88 · 176 | `targetsize` 16/24/32/48/256 + `altform-unplated` — 15 archivos |
| `Square150x150Logo` | 150 · 188 · 225 · 300 · 600 | 5 archivos |
| `Wide310x150Logo` | 310×150 · 388×188 · 465×225 · 620×300 · 1240×600 | 5 archivos |
| `StoreLogo` | 50 · 63 · 75 · 100 · 200 | 5 archivos |
| `SplashScreen` | 620×300 · 775×375 · 930×450 · 1240×600 · 2480×1200 | Fondo `#111827` = `DarkSurfaceColor` |
| `tray-icon.png` | 16 · 20 · 24 · 32 · 48 px reales | **El sexto activo, que nadie había listado** |

**35 archivos, y no se pueden producir sin el original vectorial de la marca.** Reglas: área segura del 12 % en los cuadrados; a 16 px la forma debe leerse con **una sola** figura; PNG de 32 bits con alfa real, sin fondo pintado en `unplated`; el mosaico ancho es el único formato donde cabe el nombre.

## Pendiente al cerrar este handoff

1. **Los 35 activos de instalación** — bloqueado en el original vectorial de la marca.
2. **`SURFACES.es.md` / `.en.md`** — los diez cambios están listados en `Inventario de controles`, sin volcar al repositorio.
3. **`LooseFileBanner` no es verificable** — un defecto medido el 17-08-2026 impide que llegue a pantalla al abrir un archivo desde el Explorador. Y quedan 6 controles sin pulsar en `eng/walk-pending.txt`.
4. **Decidir el alcance de las 25 cadenas de consecuencia** antes de escribirlas: están propuestas, no aprobadas.

## Archivos de este paquete

| Archivo | Qué es |
| --- | --- |
| `AP Reelume.dc.html` | Prototipo navegable de las 48 vistas, con 27 estados en el panel Demostración |
| `Catálogo de elementos - AP Reelume.dc.html` | Catálogo por elemento con sus estados y las animaciones |
| `Auditoría del inventario - AP Reelume.dc.html` | Qué faltaba por nombrar |
| `Propuesta de diseño - AP Reelume.dc.html` | Tokens, tipografía, estados, vista por vista, activos, compatibilidad |
| `Inventario de controles - AP Reelume.dc.html` | Delta del trinquete y superficies del sistema |
| `Cadenas nuevas - AP Reelume.dc.html` | Las 47 claves nuevas y las 16 que van como comentario |
| `support.js`, `doc-page.js` | Runtime de los prototipos. No forman parte del diseño |
| `github.md` | Asociación al repositorio y recibo de sincronización |

Los `.dc.html` se abren en un navegador. Ninguno necesita servidor.
