# ADR-0003 — Ensamblar la aplicación antes de empaquetarla / Assemble the Application Before Packaging It

- Estado / Status: `ACCEPTED`
- Fecha / Date: 2026-08-03
- Decisor / Decision owner: Engineering, para revisión del Product Owner / for Product Owner review
- Relacionado / Related: [`FEATURES.md`](../FEATURES.md), [C7](../evidence/mvp/C7-recovery-gate.md),
  Incremento I7 del [plan](../superpowers/plans/2026-08-01-ap-reelume-windows-mvp-implementation.md)

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be updated together.

---

## Español

### Contexto

Al cerrar I6 se recorrió la aplicación real con automatización UIA para verificar la privacidad. Ese
recorrido enseñó algo que ninguna prueba había señalado: **la aplicación expone mucho menos de lo que
tiene construido**.

Medido sobre el árbol en `0f2d001`, contando qué superficies alcanza `ShellView` directamente o a
través de otra superficie que sí alcanza:

| Superficie / Surface | ¿Alcanzable desde la aplicación? |
|---|---|
| Inicio, Biblioteca, fichas de película y serie | sí |
| Copias y restauración | sí |
| Ajustes: apariencia, recomendaciones, ciclo de vida, privacidad | sí |
| Recuperación de base dañada | sí, por el camino de fallo |
| **Añadir una carpeta a la biblioteca** (`RootOnboardingView`) | **no** |
| **Bandeja de revisión** (`ReviewInboxView`, `CandidateCardView`) | **no** |
| **Duplicados** (`DuplicateReviewView`) | **no** |
| **Editor de metadatos y arte** (`MetadataEditorView`) | **no** |
| **Renombrado seguro** (`RenamePreviewView`) | **no** |
| **Reproductor** (`PlayerView`, `TransportControlsView`) | **no** |
| **Pistas y subtítulos** (`TrackSelectorView`, `SubtitleStyleView`) | **no** |
| **Salida de audio** (`AudioOutputView`) | **no** |
| **Marcadores manuales** (`MarkerEditorView`) | **no** |
| **Reanudar** (`ResumePromptView`) | **no** |
| **Atajos** (`ShortcutSettingsView`) | **no** |
| **Ajustes de escaneo** (`ScanSettingsView`) | **no** |
| **Créditos y atribución TMDB** (`CreditsView`) | **no** |

Catorce superficies. Además, `CompositionRoot` no registra `IMetadataProvider` ni `ArtworkCache`, así
que el shell real no puede identificar nada aunque su bandeja de revisión estuviera cableada, y
`StartPlayback` y `PlayerWindowCoordinator` sólo aparecen como registros de inyección: **nadie los
invoca desde ninguna superficie**.

En términos prácticos: la aplicación que se ejecuta hoy arranca, navega entre cinco destinos, permite
crear copias, restaurar y ajustar preferencias — y no permite añadir una biblioteca, identificar un
título ni reproducir un vídeo.

Nada de esto es una regresión de I6. Cada componente existe, tiene pruebas y tiene evidencia: lo que
falta es el ensamblaje, y ninguna prueba lo comprobaba porque todas construyen la superficie que van
a examinar en lugar de pedírsela a la aplicación.

### Decisión

**1. I7 empieza ensamblando la aplicación, no empaquetándola.** Se añade una tarea **T39B** delante
de T40. Ningún identificador de tarea existente se renumera: el plan cita veintiocho SHA y decenas de
referencias cruzadas, y romperlas costaría más de lo que aclararía.

**2. La puerta del ensamblaje es una prueba que recorre la aplicación, no una lista.** La comprobación
que faltaba es estructural y se puede automatizar: toda superficie con `AutomationProperties.Name` que
el producto declara tiene que ser alcanzable desde `ShellView` por navegación o por otra superficie
alcanzable. Una vista huérfana pasa a ser un fallo de compilación de la suite, no un descubrimiento
manual.

**3. Ningún identificador se degrada por este hallazgo.** Sus criterios de aceptación describen
comportamiento y ese comportamiento está demostrado. Lo que este ADR cambia es dónde se cobra la
diferencia: en **T41**, donde la puerta MVP exige que cada compromiso sea alcanzable en el artefacto
publicado, y en **`PLY-001`**, que se mantiene `IN_PROGRESS` por esta razón y no sólo por los bloqueos
de hardware de C4.

### Consecuencias

- **I7 crece.** T39B es trabajo de integración real: rutas, ventanas, comandos y el cableado de
  metadatos que hoy no existe en la composición. No es cosmético.
- **La verificación física deja de ser opcional en cada tarea.** El recorrido de T38 encontró esto y
  otros tres defectos que las pruebas sin cabeza no podían ver, porque las pruebas construyen la
  superficie y la aplicación la ensambla. Son dos preguntas distintas.
- **La atribución de TMDB no es alcanzable hoy.** `CreditsView` existe y tiene su prueba, pero un
  requisito de licencia que nadie puede leer en la aplicación no está cumplido. T39B lo resuelve.
- **La estimación de I7 en el plan queda corta** y así se declara, en lugar de descubrirlo a mitad de
  T40 con un MSIX ya construido alrededor de una aplicación incompleta.

---

## English

### Context

Closing I6 involved walking the real application with UIA automation to verify privacy. That walk
showed something no test had flagged: **the application exposes far less than it has built**.

Measured against the tree at `0f2d001`, counting which surfaces `ShellView` reaches directly or
through another reachable surface, fourteen surfaces are unreachable: adding a library folder, the
review inbox, duplicates, the metadata editor, safe rename, the player, tracks and subtitles, audio
output, manual markers, resume, shortcuts, scan settings, and the TMDB credits.

`CompositionRoot` also registers no `IMetadataProvider` and no `ArtworkCache`, so the real shell could
not identify anything even if its review inbox were wired, and `StartPlayback` and
`PlayerWindowCoordinator` appear only as dependency registrations — **nothing invokes them from any
surface**.

In practical terms: the application that runs today starts, navigates five destinations, backs up,
restores, and adjusts preferences — and cannot add a library, identify a title, or play a video.

None of this is an I6 regression. Every component exists, is tested, and has evidence. What is missing
is the assembly, and no test caught it because every test builds the surface it is about to examine
rather than asking the application for it.

### Decision

**1. I7 begins by assembling the application, not by packaging it.** A task **T39B** is inserted
before T40. No existing task identifier is renumbered: the plan cites twenty-eight SHAs and dozens of
cross-references, and breaking them would cost more than it would clarify.

**2. The assembly gate is a test that walks the application, not a checklist.** The missing check is
structural and automatable: every surface the product declares must be reachable from `ShellView`
through navigation or through another reachable surface. An orphaned view becomes a failing suite
rather than a manual discovery.

**3. No identifier is downgraded by this finding.** Their acceptance criteria describe behaviour, and
that behaviour is demonstrated. What this ADR changes is where the gap is charged: at **T41**, where
the MVP gate requires every commitment to be reachable in the published artifact, and on **`PLY-001`**,
which stays `IN_PROGRESS` for this reason and not only for C4's hardware blocks.

### Consequences

- **I7 grows.** T39B is real integration work: routes, windows, commands, and the metadata wiring that
  does not exist in the composition today. It is not cosmetic.
- **Physical verification stops being optional per task.** T38's walk found this and three other
  defects headless tests could not see, because tests build the surface and the application assembles
  it. Those are two different questions.
- **TMDB attribution is not reachable today.** `CreditsView` exists and has its test, but a licence
  requirement nobody can read in the application is not met. T39B resolves it.
- **The plan's estimate for I7 is short**, and it is declared as such rather than discovered halfway
  through T40 with an MSIX already built around an incomplete application.
