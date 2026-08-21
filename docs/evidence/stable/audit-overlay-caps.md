# La corrección de agosto no bastaba: centrado no es lo mismo que acotado / August's fix was not enough: centred is not capped

Tercer trabajo del tramo 4 de la §4, y **la prueba encontró que el defecto de 2026-08-17 seguía vivo**
con la mitad de su síntoma. / §4's fourth tranche, and the defect fixed in August turns out to be half
fixed.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que se creía cerrado / What was thought closed

El 2026-08-17 el paseo encontró tres paneles superpuestos dibujados **a 1280×1400 sobre un escenario de
1280×1400**, con sus botones en la esquina superior izquierda en vez de en la tarjeta a la que
pertenecen. Se corrigió dándoles **alineación explícita**, y los tres comentarios del árbol lo cuentan
con su medición al lado. / Three overlays drawn at 1280×1400 over a 1280×1400 stage.

**Faltaba la otra mitad, y la §4 la pedía por su nombre: `MaxWidth`.** Medido hoy, con las dos
alineaciones puestas y una frase larga dentro:

```
No_panel_over_the_picture_stretches_to_the_stage
  ResumePromptSurface took 1278 px of a 1280 px stage, past its 420 px cap.
```

**Centrar impide que un panel se desplace, no que crezca.** Con los textos cortos que estas vistas
llevan hoy no llegan a 420 px por sí solas — por eso el defecto no se veía y por eso **la primera
versión de esta prueba pasó sin haber cambiado nada**: medir las vistas «tal como vienen» es
exactamente el falso verde que la casa ya conoce. La frase larga se inyecta a propósito, porque es lo
único contra lo que el tope protege. / Centring stops a panel drifting, not growing — and measuring it
with today's short strings is the false green.

## Lo aplicado / What was applied

| Superficie / Surface | Tope / Cap |
| --- | --- |
| `ResumePromptSurface` | 420 |
| `NextEpisodeSurface` | 420 |
| `VersionSwitchSurface` | 520 |

Y **`SkipMarkerButton` no es un panel, así que lo que necesita es una esquina**: abajo a la derecha con
margen 24, fuera del paso del transporte y del centro de la imagen. No tenía alineación ninguna. / The
skip button needs a corner rather than a cap.

## El verde / The green

```
UiTests             632/632
AccessibilityTests  135/135 en las dos pasadas / on both passes
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
El paseo / the walk: 136 declaraciones en 135 identidades; 135 pulsadas, 0 pendientes
```

**Y una honestidad sobre esa línea:** la puerta de accesibilidad falló **una vez en su segunda pasada**
y dio 135/135 en las dos al repetirla sola. Queda dicho en vez de tapado: es la forma conocida de los
intermitentes de este arnés, y **quien decide es CI**, que la corre igual con dos pasadas. / It failed
once on pass 2 and passed both when repeated; said out loud rather than hidden.
