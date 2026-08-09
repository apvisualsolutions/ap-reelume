# Dónde retomar

Estado del proyecto al cerrar la sesión del **2026-08-09 (noche)**, y qué toca a continuación.
La versión inglesa está en [NEXT-SESSION.en.md](NEXT-SESSION.en.md). El registro canónico del
alcance sigue siendo [FEATURES.md](FEATURES.md); el trabajo pendiente de la auditoría vive en
[2026-08-08-audit-remediation.md](superpowers/plans/2026-08-08-audit-remediation.md). Esto es sólo
el punto de retomada.

## Verificación de arranque

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet --version                                        # 10.0.302
git status --short --branch                             # limpio
git merge-base --is-ancestor main HEAD; $LASTEXITCODE   # 0
```

## ⚠ Lo primero: la facturación de GitHub Actions está rota

**Casi ningún run de CI de esta sesión pudo ejecutarse.** Los jobs mueren sin arrancar un solo
paso con la anotación: «The job was not started because recent account payments have failed or
your spending limit needs to be increased. Please check the 'Billing & plans' section in your
settings». Es una decisión de cuenta (tuya): arreglar el pago o el límite de gasto en GitHub →
Billing & plans. La cuota fue intermitente: algún run consiguió arrancar (el del spike en `main`
quedó vigilado al cierre); el resto murió al instante, y varios de `main` ya ni admiten
`gh run rerun`. **Tras arreglar la facturación**: relanzar los runs de `aa930d1` (ambas ramas)
si lo permiten, o dejar que el siguiente push verifique el estado completo — `aa930d1` contiene
todo lo de hoy. Todas las puertas de hoy pasaron **en local** (format, `-warnaserror`, suites
afectadas, verify-docs, guard de patrones). El run del flake del watcher-storm (31319008700)
quedó rojo en el histórico sin retry posible; su aparición ya está anotada en CI-005.

## Qué está terminado (esta sesión: `dec5ac3`, `230602e`, `aa930d1`)

- **PLY-016 resuelto por la vía honesta: `DEFERRED` con medición.** El spike midió los cuatro
  candidatos del plan como opciones de medio (con decodificación hw **y** sw) más dos controles a
  nivel de instancia (RV32 e I420): **ningún filtro de vídeo de VLC 3 procesa un solo fotograma
  en la ruta por callbacks** — el propio VLC monta la cadena y la retira entera con `Failed to
  compensate for the format changes, removing all filters`, capturado del log nativo. La métrica
  (varianza del laplaciano) demostró sensibilidad: hw vs sw difieren (1169→927). El spike queda
  re-ejecutable (`LowResEnhancementSpikeTests`, MediaTests, con muestra 480p MPEG-2 ruidosa
  propia); re-correrlo tras una futura subida de LibVLC responde si el bloqueo persiste. La fase
  2 no se ejecutó (su condición no se cumplió); las alternativas (VLC 4, realce administrado
  sobre los fotogramas BGRA, otro motor) quedan nombradas con su coste en
  [PLY16-low-res-spike.md](evidence/stable/PLY16-low-res-spike.md) — reabrir es decisión de
  alcance del propietario.
- **TST-001 (WP-7 completo): la puerta de cobertura existe y muerde.** `eng/check-coverage.ps1`
  como paso bloqueante de `verify.ps1`: todo archivo fuente nuevo contra `origin/main` debe
  llegar con ≥96 % líneas y ramas (comparación entre árboles — el checkout superficial de CI no
  la rompe; base inalcanzable = rojo en voz alta; `*.g.cs` excluidos). `reportgenerator` en
  `.config/dotnet-tools.json` y `CoverageGateTests` fijando script, umbrales e invocación.
  Calibrada contra `797c8cb`: tres rojos **verdaderos** de la sesión anterior
  (`ReconcileScannedFiles` 86,7 % líneas, `CompositeFileIdentityProvider` 66,7 %,
  `PlayerVersionsViewModel` 60,6 % — caminos felices paseados, ramas de error no), nombrados como
  deuda visible en [TST1-coverage-gate.md](evidence/stable/TST1-coverage-gate.md) sin bajar el
  umbral. Sus dientes son locales (main avanza en fast-forward con la rama, así que en CI el
  diff suele estar vacío y lo declara).
- **ARQ-006 paso 1 completo.** Las cuatro aserciones textuales restantes sobre
  `CompositionRoot.cs` son ahora de descriptores en `CompositionDescriptorTests`: constructor
  explícito del `MigrationRunner`, coordinador único de sesiones, singleton de la superficie de
  actualizaciones, y la dirección del actualizador afirmada sobre el **objeto** compuesto
  (`GitHubReleaseUpdateProvider` expone `RepositoryOwner/Name`) contra ambos changelogs. Dos
  mitades de invocación quedan declaradas como texto (arranque del check automático;
  `videoStatus.Apply` en `OpenPlayerAsync`) hasta que el arranque salga del archivo (pasos
  2-3/ARQ-001).

## Lo que sigue (en este orden)

1. **WP-9**: CONTRIBUTING.md, CLAUDE.md raíz, plantillas de issue/PR, CODEOWNERS, dependabot
   NuGet (SECURITY.md ya está). Nada de esto depende de la facturación.
2. **ARQ-006 pasos 2-3** (módulos `AddData`/`AddPlayback`/…, extraer `WindowsFilePickers`,
   `DatabaseStartup`, `WindowLifecycle`) y después ARQ-001/004/005/010.
3. La deuda de cobertura nombrada en TST-001 (ramas de error de los tres archivos) puede saldarse
   cuando se toque esa zona; la puerta no la exige retroactivamente.

## Pendiente tuyo (no del agente)

- **Arreglar Billing & plans de GitHub** (bloquea toda la CI).
- El paseo físico manual de diez minutos
  ([audit-physical-walk.md](evidence/stable/audit-physical-walk.md)).
- La copia de seguridad cifrada de la clave de firma.
- Revisar los tres PRs de dependabot (checkout/upload-artifact v7, setup-dotnet v6).
- Las decisiones económicas de siempre (certificado, Store, ARM64, jurídica, logs de
  `.superpowers/`).

## Cosas aprendidas que conviene no volver a aprender

- **Un fallo de CI con el job en `failure` y 0 pasos no es código**: es la facturación; la
  anotación del check-run lo dice. No hay nada que corregir en el árbol.
- **Los filtros de vout de VLC 3 son inertes con salida por callbacks** (vmem), da igual el
  chroma, el decodificador o la vía de activación; medir fotogramas (no opciones aceptadas) fue
  lo que lo destapó. El log nativo (`libVlc.Log`) da la causa con nombre.
- **La varianza del laplaciano distingue decodificadores** (hw vs sw difieren en la misma
  muestra): si un filtro corre, la métrica lo ve.
- **La puerta de cobertura mira el diff antes que los informes**, así el caso vacío (CI tras el
  fast-forward) cuesta cero y no exige cobertura donde no hay nada que sostener.
