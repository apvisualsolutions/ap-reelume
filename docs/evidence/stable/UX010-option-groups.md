# UX-010 — Doce grupos, no catorce, y cada uno con su deshacer dentro / Twelve Groups, Not Fourteen, Each With Its Undo Inside

- Fecha / Date: 2026-09-13
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302.
- IDs: `UX-010=IMPLEMENTED`
- Pruebas re-ejecutables / Re-runnable tests:
  `tests/ApSolutions.LocalMedia.UiTests/Theme/OptionGroupTests.cs`,
  `tests/ApSolutions.LocalMedia.UiTests/Player/PlayerSettingsMenuTests.cs`,
  `tests/ApSolutions.LocalMedia.UiTests/Settings/ScanSettingsTests.cs`,
  `tests/ApSolutions.LocalMedia.UiTests/Settings/PlaybackSettingsTests.cs`,
  `tests/ApSolutions.LocalMedia.UiTests/Settings/AppearanceRowsTests.cs`,
  `tests/ApSolutions.LocalMedia.UiTests/Settings/PrivacyConsentTests.cs`,
  `tests/ApSolutions.LocalMedia.UiTests/Settings/WatchedThresholdSettingsTests.cs`,
  `tests/ApSolutions.LocalMedia.UiTests/Theme/ViewHeightTests.cs`,
  `tests/ApSolutions.LocalMedia.AccessibilityTests/EndToEnd/AssembledPhysicalWalkTests.cs`

## Veredicto / Verdict

**«Los catorce» era una cifra que nadie había contado, y estaba escrita en once sitios. Son doce.** Y
la frase que la acompañaba —«dos controles de restablecer en toda la aplicación, ninguno en una
sección de ajustes»— era falsa por sus dos mitades: eran cuatro, y uno estaba en una sección de
ajustes. /
**«The fourteen» was a figure nobody had counted, written in eleven places. There are twelve.**

## El recuento, medido por la puerta y no leído

`OptionGroupTests` enumera los dos enums de destinos y las vistas montadas donde se ofrecen opciones,
y su primer fallo con la tabla vacía **es** el inventario: 13 destinos, 15 vistas montadas, 4 claves
con forma de restaurar, 1 botón con la clave compartida.

| Sitio | Grupos |
| --- | --- |
| Engranaje del reproductor (4) | Imagen · Estilo de subtítulos · Cuenta atrás del siguiente episodio · Detección de segmentos |
| Ajustes (8) | Apariencia · Idioma · Escaneo · Recomendaciones · Atajos · Ciclo de vida · Privacidad · Actualizaciones |

El criterio que decide cada rechazo, y que está escrito en la propia lista: un grupo guarda al menos
un valor que alguien elige; ese valor tiene un valor de fábrica que esta aplicación puede declarar; y
aparece y desaparece como una unidad.

**Rechazados por escrito**, porque un candidato ausente se lee igual que uno olvidado: las carpetas
de la biblioteca (son datos; «devolverlas a fábrica» es vaciar la biblioteca), las copias y la
restauración (cero preferencias), la vista previa de diagnóstico (sólo lectura), los créditos, la
salida de audio (la disposición de canales **la escribe Windows**, no esta aplicación — medido el
2026-09-02 — así que un restablecer cambiaría el audio de todo el ordenador), el selector de pistas
(qué pista suena es contenido, no preferencia) y la velocidad (un mando en una barra con su propio
deshacer al lado).

**Consecuencia que contradice el criterio de aceptación de la fila**: `UX-010` **no necesita ninguna
confirmación destructiva**. La prometía «donde hay datos de por medio — raíces de medios, copias», y
ésos son exactamente los dos que no son grupos.

## Los cuatro controles de restablecer que había, y el que engañaba

| Control | Clave | ¿La compartida? |
| --- | --- | --- |
| `PictureResetButton` | `RestoreDefaultsAction` | Sí |
| `SpeedResetButton` | `TransportSpeedResetAction` | No |
| `RestoreProviderMetadata` | `MetadataRestoreAction` | No |
| `RestoreDefaultsButton` (atajos) | `ShortcutSettingsRestore` | **No, y su botón ya se llamaba así** |

El cuarto es el que justifica que la puerta afirme **la clave** y no el nombre ni el texto: un grep
por `RestoreDefaults` lo daba por cumplido.

## Las cinco cegueras que el auditor encontró en la puerta, y su mutante

Cada una verificada con un mutante que sobrevivía, y vuelta a medir con el mutante sonando después.

1. **El censo filtraba por prosa española** — tres prefijos literales. Una clave que dijera «Restaurar
   ajustes predeterminados», dibujada en un botón real de un panel que la lista seguía llamando sin
   botón, pasaba con las 1.372 pruebas en verde. Y **empieza por «Restaurar»**. Sustituido por un
   censo sobre el marcado: un botón es de los que restauran cuando su nombre o su comando lo dicen.
   El barrido estructural encontró **tres botones que el filtro no veía**.
2. **El lector de la clave miraba sólo atributos**, así que un botón con su contenido en un elemento
   hijo era invisible.
3. **El suelo de vistas montadas era uno solo sobre la suma**, y Ajustes aporta catorce: quitar la
   vista del engranaje dejaba la puerta verde. Ahora el suelo es de cada ancla.
4. **La regla 11 se hacía cumplir en un solo sentido**: un grupo de Ajustes viviendo en el engranaje
   pasaba limpio.
5. **El trinquete de pendientes era un techo**: subirlo a 20 no rompía nada.

Y un defecto real, latente: el `Border` anfitrión del engranaje y el `StackPanel` de dentro
respondían los dos a `PlayerSettingsSurface`, visibles a la vez uno dentro del otro.

## Lo que la mudanza al engranaje midió

- **«`SubtitleStyleView` no cabe» fue una medición falsa mía, y estuvo escrita unas horas.** La
  prueba de ancho leía `Bounds.Right`, relativo al padre y **ciego a las transformaciones de
  render**: un `Viewbox` dispone a su hijo a tamaño natural y lo escala, así que un icono medía
  2.010 px en su espacio y **12 dibujados**. Con `TranslatePoint` contra la banda —lo que
  `ViewOverflowTests` ya hacía— las cuatro vistas caben. **Y la banda real no son 380 px sino 346**:
  el borde y el relleno del panel se llevan 34, así que la prueba comparaba contra un número que la
  vista nunca recibe.
- **El menú con todas sus ramas dibujadas mide 1.126 px**, más alto que la ventana más corta que la
  aplicación permite. La banda ganó un `ScrollViewer` de 360 px: lo que un grupo necesita con
  holgura, así que no muerde en una ventana normal — y si mordiera, el hit test del paseo no sigue un
  panel desplazado y subiría un trinquete que sólo baja.
- **`ViewHeightTests` no podía verlo**: una vista que vive dentro del reproductor se alcanza por un
  `ContentControl` cuyo `Content` es un binding, así que sin sesión la plantilla no se aplica y la
  vista no está en ningún árbol. Ahora lee también el marcado, por contención.

## Dos trampas de medición que costaron vueltas

- **Un doble que no guarda no atestigua nada.** «Restaurar devuelve el tema» pasaba con la línea del
  tema **borrada**, porque el doble de tema de esa suite devuelve la constante `System` y su `Apply`
  no hace nada. Lleva ahora uno que recuerda.
- **La previsualización local de suelos no ve una bajada**, sólo suelos cortos y archivos nuevos. Su
  silencio no dice nada sobre lo que acabas de escribir, y así se colaron dos rojos de CI seguidos:
  `PlaybackSettingsViewModel` cayó a 100/95 y `RecommendationSettingsViewModel` a 94/78, los dos por
  la rama que decide no escribir cuando el grupo ya está en su valor de fábrica.
- **Pero sí predice el suelo nuevo, y eso se midió hoy en vez de suponerlo.** Con
  `-Suites UiTests,AccessibilityTests` dio **96/85** para ese archivo y el artefacto del run
  posterior dijo **96/85**. La cautela seguía siendo correcta —un suelo sale del artefacto, y la
  previsualización sólo conoce las suites que le nombras— pero la sospecha de que la aritmética
  sumada de ramas hiciera divergir las dos cifras **no se sostuvo con el dato**.

## Y la segunda pasada del auditor, que encontró seis más

La primera auditoría cubrió la puerta de `UX-010`; estas tres piezas llegaron después y se auditaron
aparte. Ocho hallazgos verificados con mutación, y dos cambiaron una decisión:

- **La aritmética de la puerta de ancho era falsa** — `Bounds.Right` en vez de `TranslatePoint`—, y
  sobre ella descansaba «el estilo de subtítulos no cabe» y una tarea de rediseño que no hacía falta.
  Con el instrumento corregido, las cuatro vistas caben y el grupo bajó al engranaje.
- **El regex que lee el ancho casaba `MaxWidth`**, y ese número alimenta **a la vez** la ventana de la
  prueba y su umbral: un valor equivocado concuerda consigo mismo y deja pasar un desborde de 600 px.
  Medido. Lleva frontera por la izquierda y una aserción del rango.
- **La teoría tenía una fila para tres grupos**, y se montaba la vista suelta a 380 en vez de la
  cadena real, que entrega 346.
- **`KeySaidBy` devolvía el nombre accesible, no la etiqueta**, en catorce de los dieciséis botones
  del censo: la puerta escrita para afirmar «todos lo dicen con la misma clave» afirmaba otra cosa.
  Ahora lee la etiqueta y exige que las dos concuerden.
- **`PutsSomethingBack` no leía la clave**, así que un botón llamado de otra manera y atado a otro
  comando podía dibujar «Restaurar valores por defecto» sin que nada sonara.
- **La puerta de altura era existencial y no transitiva**: una aparición bajo un `ScrollViewer` en
  cualquier fichero valía por todos los montajes —incluida una con `IsVisible="False"`— y en cambio
  no reconocía una vista scrolleada a través de la que la contiene. Ahora pregunta por **todos** sus
  montajes y resuelve la contención hacia arriba.

**Y la escena del paseo cazó un defecto de producción al mudar el grupo**: el estilo de subtítulos
del engranaje **no leía lo guardado**. La página que dejó la cargaba la configuración de la ventana,
y al engranaje no lo configura nadie — el mismo agujero que el grupo de imagen tuvo el día anterior,
en el mismo menú. Abrir el engranaje es lo que carga, y ahora carga los dos.
