# Prompt para Claude Code

Copia todo lo que hay debajo de la línea y pégalo como primer mensaje, con el repositorio abierto en la rama `codex/ap-reelume-mvp-x64` y esta carpeta descomprimida en `design/`.

---

Tienes el rediseño completo de la interfaz de este proyecto en `design/`. Empieza leyendo `design/README.md` de principio a fin antes de escribir una línea de código: es el rediseño de las 48 vistas de este árbol, con tokens, estados y cadenas calculados contra el código real, no contra un mockup genérico.

**Primera tarea: confirma `design/` en la rama** con un commit propio, separado de cualquier cambio de implementación.

**Tres restricciones que este repositorio hace cumplir con pruebas.** No son preferencias de estilo; romperlas rompe la construcción:

1. Toda cadena visible existe en `Strings.es.axaml` **y** en `Strings.en.axaml`, por `DynamicResource`. Una cadena nueva va en los dos archivos o no va. Los únicos literales legítimos son símbolos: `○ ◐ ●`, `→`, `!`.
2. Todo control interactivo necesita nombre accesible, con 80 pruebas que lo exigen y un paseo automático que identifica cada control **por su clave de recurso**. En este árbol `Content` y `AutomationProperties.Name` apuntan a la misma clave, así que **reescribir la etiqueta de un botón es renombrar el control**. No reescribas etiquetas existentes.
3. Cada control nuevo necesita su prueba de nombre accesible y su línea en el paseo automático **en el mismo cambio** que lo introduce.

**El orden de trabajo importa. No lo cambies:**

1. **`Theme/DesignTokens.axaml` y los cuatro diccionarios de tema.** 12 brochas nuevas, 5 escalares nuevos, y el único valor existente que cambia: `AccentBrush` en los temas de alto contraste, de `#FFFF00` a `#00FFFF`, porque hoy el acento y el foco son el mismo amarillo y son indistinguibles justo donde el foco más importa. `--warn-fg` también debe salir del amarillo en ese tema. Añade el cuarto diccionario, `HighContrastLight`, y renombra `AppThemeVariants.HighContrast` a `HighContrastDark` — toca `AppThemeVariants`, `ThemePreference` y `FluentThemeService`. **No toques ninguna vista hasta que los tokens estén y las pruebas de tema pasen.**
2. **Los cinco estados de control**, con el anillo doble de foco: borde exterior 2 px en `FocusStrokeBrush` más anillo interior 1 px del color de la superficie. Sube los selectores de foco de 8 a 10 añadiendo `ToggleSwitch` y `RadioButton`, que hoy caen al foco del tema base sin cobertura. El borde punteado del deshabilitado necesita un `Rectangle` con `StrokeDashArray` en la plantilla: `Border` no tiene trazo discontinuo.
3. **`MiniPlayerWindow`** — hoy son diez líneas con un `Panel Background="Black"` y cero controles. Gana cinco, los cinco con el mismo estilo de 36×36 y radio 8 que el transporte del reproductor grande.
4. **`UpdateView`** — 23 mensajes en cuatro gramáticas donde hoy hay una. Todo el estado en **un solo** `Border` con `LiveSetting="Polite"`: partirlo en dos parte el anuncio al lector de pantalla.
5. **`PlayerView`** — 7 motivos de fallo con acciones condicionadas por motivo. «Elegir otra versión» es un flag independiente del motivo, no derivado de él.
6. **El resto de las vistas**, un cambio por vista, siguiendo la §4 de `Propuesta de diseño`.

**No implementes los activos de instalación.** Los 35 PNG están bloqueados esperando el original vectorial de la marca. Los cinco que hay en el paquete son marcadores de posición de entre 576 B y 7 KiB.

**Cuatro cosas quedan abiertas y están documentadas como tal** en la última sección del README. Si te encuentras con alguna, no improvises: `SURFACES.es.md` y `.en.md` necesitan diez correcciones ya listadas; `LooseFileBanner` no es verificable por un defecto medido; quedan 6 controles sin pulsar en `eng/walk-pending.txt`; y las 25 cadenas de consecuencia están **propuestas, no aprobadas** — pregunta antes de escribirlas.

**Un aviso sobre versiones:** el encargo original hablaba de Avalonia 11, pero `Directory.Packages.props` pina **12.1.1**. Trabaja contra lo que pina el árbol.
