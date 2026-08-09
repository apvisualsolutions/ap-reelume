# Matriz de vídeo por hardware / Per-hardware video matrix

Inventario del equipo de referencia x64 usado para verificar `PLY-003`. Se nombran modelos, que
hacen la evidencia reproducible; no se registran identificadores de instancia, número de serie ni
ninguna ruta local. / Inventory of the reference x64 machine used to verify the acceleration and HDR
identifier. Models are named because they make the evidence reproducible; instance identifiers,
serial numbers, and local paths are not recorded.

## Inventario / Inventory

| Elemento / Item | Valor observado / Observed value |
|---|---|
| GPU discreta / Discrete GPU | NVIDIA GeForce RTX 5070, controlador 32.0.16.1062 |
| GPU integrada / Integrated GPU | **Ninguna, medido.** Windows no enumera ningún dispositivo gráfico Intel: ni presente, ni fantasma, ni en el registro de la clase de vídeo, aunque sí enumera veinte dispositivos Intel del chipset. El propietario del equipo confirma que su procesador no tiene gráficos / **None, measured.** Windows enumerates no Intel display device — not present, not phantom, not in the video class registry — while enumerating twenty Intel chipset devices. The machine's owner confirms the processor has no graphics |
| Pantallas / Displays | 2 × ASUS ProArt PA279CRV por DisplayPort, nativo 3840×2160 a 60 Hz, en uso a 2560×1440 con escala 150 % |
| HDR de pantalla / Display HDR | Soportado y **activado** en ambas: `BT2020RGB`, `BT2020YCC`, `Eotf2084Supported` |
| Sistema / System | Windows 11 Pro 10.0.26200 x64 |
| Motor / Engine | LibVLC 3.0.23.1 con LibVLCSharp 3.10.0 |

La consulta de capacidades no es una lectura de `dxdiag`: `WindowsDisplayCapabilityProvider` pregunta
a la configuración de pantalla de Windows y devolvió `supportsHdr10=True, hdrEnabled=True` con
`paths=2 queried=2 refused=0`. El registro se conserva en
`artifacts/test-results/T22/green/display-capabilities.csv`. / The capability query is a live system
call, not a report reading; its recorded output is retained at the path above.

## Resultados automatizados / Automated results

Estos resultados no dependen del hardware: la política decide desde los hechos que se le pasan. /
These results do not depend on the hardware; the policy decides from the facts it is given.

| Escenario / Scenario | Ruta / Path | Aceleración / Acceleration |
|---|---|---|
| HDR10 con pantalla HDR activa / HDR10 with active HDR display | `Hdr10Passthrough` | activa / active |
| HDR10 con pantalla sin HDR o con HDR apagado / HDR10 with SDR display or HDR off | `SdrToneMapped` | activa / active |
| SDR en cualquier pantalla / SDR on any display | `Sdr` | según se pida / as requested |
| Aceleración solicitada y no disponible / Acceleration requested and unavailable | sin cambio / unchanged | software, `FellBackToSoftware=true` |
| Dolby Vision | `SdrToneMapped` con `UnsupportedCapability` | sin ruta propia / no dedicated path |

## Resultados físicos / Physical results

Ejecutados en el equipo de arriba, con muestras generadas por el propio conjunto de pruebas. /
Run on the machine above with samples the suite generates itself.

| Muestra / Sample | Transferencia declarada / Declared transfer | Ruta reportada / Reported path | Fotogramas / Frames |
|---|---|---|---|
| `mkv-hevc-hdr10` (HEVC Main 10, BT.2020) | `smpte2084` leído con ffprobe / read with ffprobe | `Hdr10Passthrough` en pantalla HDR / on the HDR display | decodificados / decoded |
| `mkv-hevc-hdr10` con pantalla SDR simulada / with an SDR display | `smpte2084` | `SdrToneMapped` | decodificados / decoded |
| `mkv-hevc-sdr` (HEVC Main, BT.709) | `bt709` | `Sdr` | decodificados / decoded |
| `mkv-hevc-sdr` tras forzar el fallback / after forcing the fallback | `bt709` | `Sdr`, `HardwareAccelerationActive=false` | decodificados, la reproducción continúa / decoded, playback continues |

Un origen SDR nunca se promociona a HDR: la clasificación mira la característica de transferencia
declarada, no el contenedor ni el nombre del archivo. / An SDR source is never promoted to HDR; the
classification reads the declared transfer characteristics.

## Limitaciones de hardware / Hardware limitations

1. **Este equipo no tiene GPU integrada, y no va a tenerla.** El plan pide ejecutar la matriz en los
   adaptadores integrados y discretos **disponibles**; aquí sólo está el discreto, así que la matriz
   se ejecutó entera sobre el hardware disponible y la comparación entre ambas clases **no existe
   como trabajo pendiente**: no hay segundo adaptador que habilitar. Queda como límite de cobertura
   —la ruta de decodificación de Intel Quick Sync no se ha ejercido nunca— y así se registra en
   [ADR-0005](../../adr/0005-verify-the-video-matrix-on-available-adapters.md). / The plan asks for
   the **available** integrated and discrete adapters; only the discrete one exists here, so the
   matrix ran in full on the hardware available and the comparison between classes is not pending
   work: there is no second adapter to enable. It stands as a coverage limit — Intel Quick Sync's
   decode path has never been exercised — recorded in ADR-0005.
2. **La aceleración activa se reporta desde la solicitud y el estado del fallback, no desde una
   confirmación del motor.** LibVLC 3 no expone qué decodificador acabó usando, así que el indicador
   dice "se pidió aceleración y no se ha caído a software", que es exactamente lo que la aplicación
   puede probar. / Active acceleration is reported from the request and the fallback state because
   LibVLC 3 does not expose which decoder it selected.
3. **No se ha medido la salida HDR en la pantalla con un instrumento.** Se comprueba que el sistema
   declara HDR activo, que la fuente declara PQ y que el motor informa de la ruta de paso directo;
   no se ha medido luminancia ni gama en el panel. / The HDR signal was not measured with an
   instrument; the chain is verified by declared state, not by photometry.
