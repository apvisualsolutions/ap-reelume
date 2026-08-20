# Qué botón lidera cada pantalla, decidido una vez y escrito / Which button leads each screen, decided once and written down

`primary-action` es la única parte del rediseño que **no se puede barrer**: cuál es la acción
principal de una pantalla es una decisión **de esa pantalla**. Así que se decidieron las 48 de una vez,
la tabla vive en la prueba, y una vista nueva falla hasta que alguien elija. / `primary-action` is the
one part of the redesign that **cannot be swept**, so all 48 were decided at once and the table lives
in the test.

Fecha / Date: 2026-08-20. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El estado de partida / Where it started

**34 vistas tienen botón y sólo 3 tenían acción principal** — `ResumeHeroView`, `PlayerView` y
`UpdateView`, las tres de las fases anteriores. / 34 views have a button and only 3 had a leading
action.

## Las 14 que la ganan / The 14 that gain one

| Vista / View | Lidera / Leads with |
|---|---|
| `MovieDetailsView` | `MovieResumeAction` |
| `ResumePromptView` | `ResumeButton` |
| `NextEpisodeOverlay` | `PlayNextNowButton` |
| `MarkerEditorView` | `SaveMarkerButton` |
| `DetectedMarkerReviewView` | `AcceptDetectionButton` |
| `VersionSwitchDialog` | `ConfirmSwitchButton` |
| `LooseFileBanner` | `AddContainingFolderButton` |
| `ReviewInboxView` | `AcceptReviewAction` |
| `MetadataEditorView` | `MetadataSaveAction` |
| `RenamePreviewView` | `RenameExecuteAction` |
| `BackupView` | `CreateCopyButton` |
| `RestoreWizardView` | `ConfirmRestoreButton` |
| `RootOnboardingView` | `RootAddAction` |
| `DatabaseRecoveryView` | `RecoveryOpenBackupFolder` |

**`MovieDetailsView` merece su párrafo**, porque parece el caso prohibido y no lo es. `MovieResumeAction`
aparece con `CanResume` y `MoviePlayAction` está siempre, así que **coexisten**: con progreso guardado,
«Continuar» y «Reproducir desde el principio» están las dos en pantalla. No es `Play`/`Pause`, que son
**la misma acción conmutada**; son dos acciones distintas con una preferencia estable —si hay dónde
continuar, se continúa—. Sin progreso, la ficha no destaca ninguna, y eso es correcto: reproducir desde
el principio y ver el tráiler son elecciones equivalentes cuando no hay nada empezado. Es el mismo
patrón que `UpdateView`, que tampoco destaca «Instalar». / **`MovieDetailsView` looks like the forbidden
case and is not**: two distinct actions with a stable preference, not one action toggled.

## Las 16 que NO la llevan, y por qué / The 16 that carry none, and why

Las razones son de seis clases, y **cada una es una decisión**: / The reasons fall into six kinds:

1. **Un marco no es una pantalla.** `ShellView` tiene once botones: cinco destinos de navegación, tres
   acciones sobre el título en curso y tres del cromo del reproductor. Ninguno es «para lo que está el
   shell», porque el shell no está para nada: es el marco. / A frame is not a screen.
2. **Una jerarquía que se repite N veces no es jerarquía.** `LibraryEntryView`, `EpisodeRowView` y
   `PlayerVersionsView` son filas o tarjetas repetidas; acentuar su botón pinta la parrilla entera del
   color del acento y no destaca nada. `ResumeHeroView` sí lo lleva porque es **una**. / A hierarchy
   repeated N times is not a hierarchy.
3. **El cromo alterna por estado.** `TransportControlsView` y `MiniPlayerChromeView`: una acción
   principal que se mueve con lo que está pasando es lo único que una jerarquía no puede hacer. /
   Chrome alternates by state.
4. **Opciones excluyentes no son acciones.** `AppearanceSettingsView` (tema e idioma) y
   `WatchStatusControl` (visto / sin empezar / limpiar). / Mutually exclusive options are not actions.
5. **Un botón solo no tiene con qué compararse.** `SkipMarkerButton`, `ShortcutSettingsView`,
   `RecommendationSettingsView`. / One button alone has nothing to be ranked against.
6. **Y la que es una decisión de principio: en un consentimiento, acentuar la afirmativa es un patrón
   oscuro.** `LifecycleSettingsView` —conceder o rechazar el arranque con Windows— y
   `PrivacySettingsView` —previsualizar o exportar el diagnóstico— lideran con **nada**, a propósito,
   en una aplicación cuyo argumento es que no manda nada a ninguna parte. Destacar «Exportar» en la
   pantalla de privacidad sería empujar exactamente donde no se debe. / **And the one that is a matter
   of principle: accenting the affirmative of a consent is a dark pattern.**

## La puerta, probada fallando en tres direcciones / The gate, proved by failing in three directions

```
1. BackupView pierde su acción principal
   -> BackupView: expected [CreateCopyButton] to lead, found []
2. BackupView gana una segunda
   -> BackupView: expected [CreateCopyButton] to lead, found [CreateCopyButton, ExportButton]
3. BackupView sale de la tabla
   -> BackupView is in the tree and not in the table, so nobody has decided
      whether it leads with anything (it currently accents 1).
```

La tercera es la que hace que la tabla no envejezca: **una vista nueva falla hasta que alguien decida**.
/ The third is what keeps the table from ageing.

**Qué es una vista se decide por su elemento raíz** —`UserControl` o `Window`— y no por una lista de
excepciones, que es donde se escondería una vista real el día que alguien añada una que no encaje. El
primer intento acusó a `App`, `Brand`, `Strings.en` y `Strings.es`, que son diccionarios. / **What
counts as a view is decided by its root element**, not by an exclusion list.

**Y dos suelos anticeguera**: al menos 40 vistas leídas, y el total de botones acentuados tiene que
coincidir **exactamente** con los declarados en la tabla y ser al menos 17. Sin el segundo, un patrón
que dejara de casar encontraría cero acentos, coincidiría con una tabla de nulos y declararía la
aplicación entera «indecisa a propósito». / **Two anti-blindness floors**, and the second is what stops
a pattern that stopped matching from agreeing with a table of nulls.

**Una trampa medida por el camino:** el identificador de un botón se sacaba con `Name="..."`, que casa
**dentro de `AutomationProperties.Name`** y devolvía `{DynamicResource X}` como nombre propio del
control. Seis vistas lo delataron a la vez. / The identity pattern matched inside
`AutomationProperties.Name`; six views gave it away at once.

## El verde / The green

```
UiTests             614/614
AccessibilityTests  135/135
IntegrationTests    456/457 (1 omitida por diseño / 1 skipped by design)
```
