# Informe de accesibilidad del MVP / MVP accessibility report

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Alcance / Scope: recorrido canónico completo, diecisiete superficies, español e inglés
- Puerta / Gate: `0 Critical`, `0 Major`. Ninguna severidad se rebajó y ningún chequeo se suprimió. /
  No severity was lowered and no check was suppressed.

## Entorno / Environment

Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, PowerShell 7, Avalonia 12.1.1, FlaUI UIA3 5.0.0,
NVIDIA GeForce RTX 5070, dos ASUS ProArt PA279CRV a 2560×1440 con escala del sistema al 150 %.
Preferencias de accesibilidad de Windows leídas durante la sesión: animación de área de cliente
activada, alto contraste desactivado, factor de escala de texto 100, Narrator instalado. /
Recorded environment and the accessibility preferences Windows actually reported.

## Cómo se audita / How the audit runs

```powershell
pwsh ./eng/run-accessibility.ps1 -Mode Audit    # inventaria todo, no bloquea / inventory, never blocks
pwsh ./eng/run-accessibility.ps1 -Mode Verify -Passes 2   # la puerta / the gate
pwsh ./eng/run-accessibility.ps1 -RealApp       # árbol UIA del ejecutable real / real UIA tree
```

`Audit` recoge la lista completa de una sola pasada; `Verify` falla ante cualquier defecto crítico o
mayor. Los dos escriben los mismos hallazgos en JSON y Markdown. / Audit inventories, Verify gates,
both write the same findings.

## Recorrido canónico / Canonical journey

| Paso / Step | Superficie / Surface |
|---:|---|
| 1 primer inicio / first run | `ShellView` |
| 2 añadir raíz / add root | `RootOnboardingView` |
| 3 buscar / search | `LibraryView` |
| 4 revisar / review | `ReviewInboxView` |
| 5 abrir ficha / open details | `MovieDetailsView`, `ShowDetailsView` (con `EpisodeRowView`) |
| 6 reproducir / play | `PlayerView` |
| 7 controlar / control | `TransportControlsView` |
| 8 reanudar / resume | `ResumePromptView`, `HomeView` (con `ResumeHeroView`, `InProgressRailView`, `LibraryEntryView`, `RecommendationsRailView`) |
| 9 favorito / favourite | `PersonalActionsView` |
| 10 copia / backup | destino `Backups` del shell / the shell's Backups destination |
| 11 ajustes / settings | `AppearanceSettingsView`, `RecommendationSettingsView`, `ScanSettingsView`, `SubtitleStyleView`, `ShortcutSettingsView` |

El paso 10 no tiene vista propia hasta T36. Se audita el destino tal como existe hoy y se declara; no
se adelanta trabajo de I6. / Backups has no dedicated view until T36; the destination is audited as it
exists and the limit is stated.

## Defectos encontrados y cerrados / Defects found and closed

La primera pasada en modo `Audit` registró **61 hallazgos: 14 críticos, 30 mayores y 17 menores**,
que se agrupan en nueve defectos con archivo propietario. Cada uno se corrigió en su propio ciclo. /
The first audit pass recorded 61 findings in nine defects, each fixed in its owning file.

| # | Severidad | Defecto / Defect | Archivo propietario / Owning file | Corrección / Fix |
|---|---|---|---|---|
| D1 | Critical | Cada tarjeta del catálogo anunciaba `Avalonia.Controls.Grid`, el nombre de la clase de su contenido, en vez del título. | `Library/LibraryView.axaml`, `Library/CatalogItemViewModel.cs` | El botón anuncia el título y añade la disponibilidad como texto de ayuda, traducida desde una clave. |
| D2 | Critical | Los diez botones de valoración anunciaban la misma frase, así que no se podía elegir una nota concreta con lector. | `Catalog/PersonalActionsView.axaml` | Cada botón añade su número como texto de ayuda. |
| D3 | Critical | Todos los botones «Reproducir episodio» anunciaban lo mismo en una temporada entera. | `Show/EpisodeRowView.axaml`, `Show/SeasonViewModel.cs` | Cada botón añade `SxxEyy` como texto de ayuda. |
| D4 | Major | La lista de resultados de la biblioteca no tenía nombre. | `Library/LibraryView.axaml` | Nombre propio desde recurso. |
| D5 | Major | El escaneo, el único trabajo largo del recorrido, no se anunciaba en ninguna parte: no existía región activa. | `Library/LibraryView.axaml`, `Library/LibraryViewModel.cs` | La biblioteca muestra el progreso en palabras dentro de una región activa `Polite`, alimentada por el `ScanProgressViewModel` que ya existía y que nadie mostraba. |
| D6 | Major | Favorito y ver más tarde no anunciaban su estado: la respuesta sólo vivía en una etiqueta vecina. | `Catalog/PersonalActionsView.axaml` | Son `ToggleButton`, así que exponen el patrón de alternancia y Narrator dice «activado» o «desactivado». |
| D7 | Major | Ningún destino de navegación decía si era el actual. | `Shell/ShellView.axaml`, `Navigation/RouteStateConverter.cs` | Cada destino lleva un glifo visible y un estado de elemento; el actual se anuncia. |
| D8 | Major | Sólo `Button` recibía el token de foco aprobado. `CheckBox`, `ComboBox`, `ListBoxItem`, `NumericUpDown`, `Slider` y `TextBox` dependían del tema base, cuyo contraste nadie había medido. | `Theme/DesignTokens.axaml` | Los siete tipos usan el mismo pincel y grosor de foco ya validados en las cuatro variantes. |
| D9 | Major | El aviso de estado de vídeo aparecía solo y no se anunciaba. | `Player/VideoStatusOverlay.axaml` | Región activa `Polite`, como el aviso de siguiente episodio. |
| D10 | Major | A 200 % varias filas de acciones salían del viewport y quedaban fuera de alcance: valoraciones, estado de visionado, acciones de revisión y la cabecera de recomendaciones. | `Catalog/PersonalActionsView.axaml`, `Catalog/WatchStatusControl.axaml`, `Review/ReviewInboxView.axaml`, `Home/RecommendationsRailView.axaml` | Esas filas envuelven en lugar de desbordar. |
| D11 | Minor | Ninguna superficie declaraba encabezados, así que un lector no podía saltar entre secciones. | doce vistas / twelve views | Cada página declara su encabezado; los componentes embebidos no, para no ensuciar la lista de encabezados. |

Dos hallazgos iniciales resultaron ser defectos **de la propia auditoría**, no de la aplicación, y se
corrigieron antes de fijar el ciclo rojo: el tamaño deseado de una etiqueta incluye su margen y el
ancho asignado no, lo que marcaba como recortada cualquier etiqueta con margen; y el nombre local del
atributo de región activa llega con prefijo, lo que ocultaba una región que sí existía. Se registran
aquí porque una auditoría que no distingue su propio ruido no vale nada. / Two initial findings were
defects in the audit itself and are recorded rather than quietly dropped.

## Qué se automatiza y qué no / What is automated and what is not

Automatizado, en las diecisiete superficies y en los dos idiomas: árbol UIA con nombre, rol y estado;
orden y alcance del foco; ausencia de trampas de foco; patrón de invocación; tokens de foco por tipo
de control; contraste y ausencia de color literal; ausencia de estado indicado sólo por color;
escalado al 100, 150 y 200 %; ausencia de duraciones incrustadas fuera del token de movimiento;
regiones activas; y los seis controles de subtítulos con su rango. / Automated across every surface
and both languages.

No automatizado: la voz de Narrator. Un lector de pantalla no expone su locución, así que **no se
transcribe lo que dijo**; lo que sí se registra es exactamente aquello de lo que habla —el árbol UIA
que Windows publica del ejecutable real— y que Narrator convive con la aplicación sin degradarla. /
Narrator's speech is not transcribed because it cannot be captured; what it reads is.

## Comprobación sobre la aplicación real / Real-application check

`pwsh ./eng/run-accessibility.ps1 -RealApp` lanza el ejecutable `Release` y lo inspecciona con
FlaUI UIA3, la misma interfaz que usa Narrator:

- **45 elementos** en el árbol UIA publicado, con nombres en español y roles correctos.
- **Siete controles propios aceptan el foco cuando se les pide**: los cinco destinos de navegación,
  «Abrir biblioteca» y el interruptor de recomendaciones. Los tres restantes del árbol —barra de
  título y menú de sistema— los aporta Windows, no la aplicación.
- Los glifos de estado `●` y `○` aparecen en el árbol junto a cada destino, así que el destino actual
  no depende del color.
- Narrator y la aplicación se ejecutaron a la vez durante doce segundos: la aplicación siguió
  respondiendo, conservó su ventana y ninguno de los dos procesos terminó por su cuenta.

El recorrido de tabulación **sintético** sobre la aplicación real no se pudo ejecutar: Windows no
concede el primer plano a un proceso lanzado en segundo plano, así que las pulsaciones sintéticas
llegaban a otra ventana del escritorio. En su lugar se pide el foco por UIA a cada control, que es lo
que hace un lector, y se comprueba que lo toma. El orden de tabulación completo sí está verificado de
forma exhaustiva en las diecisiete superficies por la automatización. / The synthetic tab walk is
blocked by the Windows foreground lock and is replaced by a UIA focus request per control; tab order
itself is verified exhaustively in automation.

El recorrido con la aplicación real se hace en español porque el host no expone conmutador de idioma:
la interfaz arranca en español y el inglés se selecciona por recursos. La paridad de los dos idiomas
está verificada en el árbol UIA de las diecisiete superficies. / The real-application walk is Spanish
only because the host has no language switch; parity is verified in automation.

El escalado al 200 % sobre la aplicación real tampoco se fuerza: Avalonia 12.1 no ofrece variable de
entorno de escala y cambiar la escala del sistema afectaría a la sesión de escritorio. El equipo está
a 150 % de forma nativa, que es la escala del recorrido físico; 100 % y 200 % se cubren en la
automatización, que usa el mismo mecanismo de escala de render que el DPI real. / Real-application
scaling stays at the machine's native 150 %; 100 % and 200 % are covered in automation.

## Resultado / Result

Dos pasadas consecutivas de `-Mode Verify` en `Release`: **0 críticos, 0 mayores, 0 menores** en
ambas. La puerta de accesibilidad del MVP queda superada. / Two consecutive verify passes are clean.
