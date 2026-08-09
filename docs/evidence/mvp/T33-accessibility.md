# T33 — Auditoría integral de accesibilidad / End-to-end accessibility audit

- Fecha / Date: 2026-08-03
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `10bd36f`
- Commit de tarea / Task commit: `fix: close MVP accessibility audit findings`
- Entorno / Environment: Windows 11 Pro 10.0.26200 x64, .NET SDK 10.0.302, Avalonia 12.1.1,
  FlaUI UIA3 5.0.0, NVIDIA GeForce RTX 5070, dos ASUS ProArt PA279CRV a 2560×1440 con escala 150 %
- IDs: `A11Y-001=VERIFIED`, `A11Y-002=VERIFIED`; `PLY-014`, `UX-002` y `UX-003` conservan `VERIFIED` y
  suman evidencia de lector de pantalla / keep verified and gain screen-reader evidence
- Informe firmado / Signed report: [informe de accesibilidad / accessibility report](accessibility-report.md)

## RED y GREEN / RED and GREEN

`KeyboardJourneyTests`, `NarratorMetadataTests`, `HighContrastTests`, `TextScalingTests`,
`ReducedMotionTests` y `SubtitleCustomizationTests` se escribieron antes de tocar una sola vista, junto
con el registro de defectos `AccessibilityAudit` y el catálogo `CanonicalJourney` que construye las
diecisiete superficies del recorrido. RED falló en **7 de 22 pruebas** con **61 hallazgos: 14 críticos,
30 mayores y 17 menores**. La salida está en `artifacts/test-results/T33/red/`, con el inventario
completo en `red/audit/`. / The six suites plus the audit log were written first; RED failed on seven
tests with sixty-one findings.

GREEN ejecuta **768 pruebas con 0 fallos y 0 omitidas** en `Release`, con TRX, registros y Cobertura
bajo `artifacts/test-results/T33/green/`. `dotnet format --verify-no-changes` no informa cambios y
ambas compilaciones terminan con 0 advertencias. La suite pasó de **745** a **768**. / GREEN runs 768
tests with no failures and no skips; the suite grew by 23.

## Los nueve defectos, uno por ciclo / The nine defects, one cycle each

Cada defecto se corrigió en su archivo propietario y ninguna severidad se rebajó para pasar. La tabla
completa, con reproducción y corrección, está en el
[informe firmado](accessibility-report.md#defectos-encontrados-y-cerrados--defects-found-and-closed).
Resumen: / Each defect was fixed in its owning file; the signed report carries the full table.

| Severidad | Defectos / Defects |
|---|---|
| Critical | tarjetas del catálogo sin nombre real; diez valoraciones indistinguibles; episodios indistinguibles |
| Major | lista sin nombre; escaneo silencioso; alternancias sin estado; destino actual mudo; seis tipos sin token de foco; aviso de vídeo sin región activa; filas fuera del viewport a 200 % |
| Minor | doce páginas sin encabezado |

Dos hallazgos iniciales eran defectos **de la auditoría**, no del producto —el margen cuenta en el
tamaño deseado y el atributo de región activa llega con prefijo—, y se corrigieron antes de fijar el
rojo en lugar de aceptarlos como fallos del producto. / Two initial findings were audit defects and
were fixed before the RED was recorded.

## Qué comprueba la automatización / What automation checks

| Suite | Comprobación / Check |
|---|---|
| `KeyboardJourneyTests` | Cada parada se entra con teclado; el anillo de tabulación no atrapa; cada tipo focusable está cubierto por el token de foco aprobado; todo botón expone patrón de invocación; Enter activa de verdad la acción primaria de Inicio y la alternancia personal |
| `NarratorMetadataTests` | Nombre no vacío y no derivado de un nombre de clase; dos acciones de una misma superficie nunca anuncian la misma frase; toda alternancia expone estado; el trabajo largo tiene región activa; cada página declara encabezado |
| `HighContrastTests` | Las diecisiete superficies renderizan en alto contraste claro y oscuro; ninguna vista pinta un color literal; ningún color se enlaza directamente a estado |
| `TextScalingTests` | Al 100, 150 y 200 % ninguna etiqueta pierde palabras sin envolver ni recortar, y ningún control queda fuera del viewport |
| `ReducedMotionTests` | Ninguna vista incrusta una duración; el token reducido es cero y el estándar se queda por debajo de 250 ms; los avisos que aparecen solos declaran región activa |
| `SubtitleCustomizationTests` | Los seis controles tienen nombre, aceptan foco y exponen su valor; la vista previa tiene nombre; el rango llega al 300 % y baja al 50 % |

Todo se ejecuta en **español e inglés**. / Everything runs in both languages.

## Verificación física / Physical verification

`pwsh ./eng/run-accessibility.ps1 -RealApp` sobre el ejecutable `Release`, con FlaUI UIA3:

| Medida / Measurement | Resultado / Result |
|---|---|
| Elementos en el árbol UIA real / Elements in the real UIA tree | **45** |
| Controles propios que toman el foco al pedirlo / Own controls that take focus when asked | **7 de 7** |
| Glifos de estado presentes en el árbol / State glyphs present in the tree | `●` y `○` por destino |
| Narrator y la aplicación a la vez / Narrator alongside the application | la aplicación siguió respondiendo doce segundos y conservó su ventana |
| Preferencias de Windows leídas / Windows preferences read | animación activada, alto contraste desactivado, escala de texto 100, Narrator instalado |

Tres límites declarados, no simulados: la locución de Narrator no se puede capturar, así que se
registra el árbol que lee y no una transcripción; el recorrido de tabulación sintético sobre la
aplicación real choca con el bloqueo de primer plano de Windows y se sustituye por una petición de
foco por UIA a cada control; y el 200 % físico no se fuerza porque Avalonia 12.1 no ofrece variable de
escala y el equipo corre a 150 %. Los tres están cubiertos de forma exhaustiva en la automatización. /
Three limits are declared rather than simulated, and all three are covered in automation.

## Dos pasadas limpias / Two clean passes

```powershell
pwsh ./eng/run-accessibility.ps1 -Mode Verify -Passes 2
```

Pasada 1: 43 pruebas, 0 fallos. Pasada 2: 43 pruebas, 0 fallos. Recuento agregado: **0 críticos,
0 mayores, 0 menores**. / Two consecutive verify passes, both clean.

## Cobertura / Coverage

| Archivo / File | Líneas / Lines |
|---|---:|
| `Presentation/Navigation/RouteStateConverter.cs` | 13/13 — 100 % |
| `Presentation/Show/SeasonViewModel.cs` (`SeasonEpisodeLabel`) | cubierta / covered |
| `Presentation/Library/CatalogItemViewModel.cs` (`AvailabilityKey`) | cubierta / covered |
| `Presentation/Library/LibraryViewModel.cs` (`ScanProgress`) | cubierta / covered |
| **Total del código nuevo / New code total** | **17/17 — 100 %** |

La única línea sin cubrir de `CatalogItemViewModel` es `Year`, anterior a esta tarea. El resto del
trabajo de T33 son vistas XAML y pruebas, que no producen líneas de cobertura. / The one uncovered
line predates this task; the rest of T33 is XAML and tests.

## Baseline estructural / Structural baseline

Los cambios de esta tarea no alteran el registro estructural de Inicio: el primer foco sigue siendo
Continuar, el acceso a Biblioteca sigue dentro del primer viewport en las 36 combinaciones y el orden
de foco no cambia, porque los controles que se convirtieron en `ToggleButton` o pasaron a envolver no
llevan nombre en la baseline. `HomeLayoutTests` pasa sin regenerarla. / Home's approved baseline is
unchanged and still passes.

## Privacidad y límites / Privacy and boundaries

- **Sin telemetría ni red**: ninguna prueba ni archivo de esta tarea abre un socket, resuelve un
  nombre ni emite un evento.
- **Sin rutas ni datos ajenos en la evidencia**: la captura del árbol UIA registra sólo elementos del
  propio proceso; cuando el foco salió de la aplicación durante el intento de recorrido sintético se
  anotó como tal y **nunca por nombre**, porque pertenecía a otra ventana del escritorio.
- **Sin operaciones destructivas**: ningún `File.Delete`, `File.Move` ni escritura sobre archivos
  multimedia.
- **Artefactos ignorados**: `artifacts/` no aparece en `git status`.
- **Sin datos personales versionados**: ningún archivo tocado contiene nombre de usuario, nombre de
  equipo ni ruta absoluta local.

`A11Y-001` y `A11Y-002` pasan a `VERIFIED`: el recorrido completo se opera con teclado, cada control
anuncia nombre, rol y estado, el foco es visible desde el token aprobado en los siete tipos, el texto
sobrevive al 200 %, el alto contraste no pierde ninguna señal, nada se mueve cuando se pide menos
movimiento y los subtítulos se personalizan con rangos accesibles. / Both accessibility identifiers
verify.
