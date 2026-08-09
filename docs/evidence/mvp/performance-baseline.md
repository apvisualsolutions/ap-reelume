# T10 — Línea base de rendimiento / Performance baseline

- Fecha / Date: 2026-08-01
- Commit de producto medido / Measured product commit: `f434c5ca3463f01461fcf660da20ab444426d5ff`
- Comando / Command: `pwsh ./eng/run-performance.ps1 -Baseline none`
- Configuración / Configuration: Release, .NET SDK 10.0.302, SQLite WAL caliente / warm SQLite WAL

Esta línea base es bilingüe y versionada. El warm-up se ejecuta antes de cada
grupo y no entra en mediana/p95; cada métrica temporal contiene cinco muestras
internas salvo frame (60). / This is a bilingual, versioned baseline. Warm-up
runs before each group and is excluded from median/p95; every timing metric has
five internal samples except frame (60).

## Hardware de referencia / Reference hardware

- Windows 11 Pro `10.0.26200`, x64.
- Placa/equipo / Board/system: Micro-Star International `MS-7D91`.
- CPU: Intel Core i7-14700K, 28 procesadores lógicos / logical processors.
- RAM: 102.841.982.976 bytes.
- GPU: NVIDIA GeForce RTX 5070, driver `32.0.16.1062`.
- Volumen de pruebas / Test volume: `E:`, espacio libre observado
  1.639.229.231.104 bytes / observed free space.

## Corpus determinista / Deterministic corpus

La base contiene exactamente 10.000 `MediaFile`: 6.000 episodios y 4.000
películas (60/40), 1.000 no disponibles (10 %), 500 copias duplicadas (5 %) y
títulos Unicode con `Amélie`, `Ñandú` y `東京`. Las rutas son sintéticas y no
incluyen multimedia del usuario. / The database contains exactly 10,000 media
files: 6,000 episodes and 4,000 movies, 1,000 unavailable, 500 duplicate copies,
and Unicode titles. Paths are synthetic and no user media is included.

## Métricas limpias iniciales / Initial clean metrics

| Métrica / Metric | Unidad / Unit | Mediana / Median | p95 | Presupuesto / Budget | Resultado / Result |
|---|---:|---:|---:|---:|---|
| `useful-window` | ms | 1.4618 | 1.8465 | <3000 | PASS |
| `first-search-page` | ms | 2.8917 | 3.0379 | <150 | PASS |
| `concurrent-search` | ms | 2.0737 | 7.7323 | <150 | PASS |
| `frame-p95` | ms | 0.5730 | 0.8971 | <16.7 | PASS |
| `scan-ui-block` | ms | 0.0678 | 9.0995 | <50 | PASS |
| `unchanged-probes` | count | 0 | 0 | =0 | PASS |

La primera ejecución no aislada registró un RED reproducible en el arnés:
`concurrent-search p95=159,7476 ms`. Los TRX mostraron que las tres clases
creaban bases de 10.000 elementos en paralelo; tras serializar sólo el arnés,
sin modificar producción, la ejecución limpia produjo la tabla anterior. /
The first unisolated run recorded a harness RED at 159.7476 ms. TRX timings
showed all three classes building 10,000-item databases concurrently; serializing
only the harness, with no production change, produced the clean table above.

El frame mide render headless Skia real de `LibraryView` a 1024×720 con
`VirtualizingStackPanel`, invalidación y captura de 60 frames. No representa una
GPU física interactiva, por lo que C2 repite la demo visual en Windows. / Frame
timing uses real headless Skia rendering of the virtualized Library view at
1024×720 with invalidation and 60 captures. It is not a physical interactive GPU,
so C2 repeats the visual demo on Windows.
