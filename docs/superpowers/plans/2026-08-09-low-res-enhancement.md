# Plan de PLY-016 — Mejora de calidad para vídeos de baja resolución / Low-resolution quality enhancement — 2026-08-09

Registro ejecutable de la investigación e implementación de PLY-016, con las decisiones técnicas ya
tomadas (autorizado por el propietario el 2026-08-09: decidir como experto y ejecutar sin
re-deliberar). / The executable record of PLY-016's research and implementation, with the technical
decisions already taken.

**Motivación.** Series de baja resolución (480p/576p, material de la era DVD) se ven «regular» al
escalarse a pantallas 1080p/4K: la ruta de vídeo D3D11 de LibVLC escala con el sampler de la GPU
(bilineal) y no restaura nitidez ni limpia el ruido de origen. / Low-resolution series look poor
when scaled to modern screens: LibVLC's D3D11 video path scales with the GPU sampler and restores
no sharpness.

**Método por tarea:** el ciclo de la casa — RED archivado → corrección mínima → GREEN + puertas →
evidencia bilingüe → changelogs ES/EN → un commit → push con `main` en fast-forward y CI vigilada.

## Decisiones tomadas (2026-08-09, como experto)

1. **Dos fases, investigación primero.** La mejora sólo se implementa con lo que el spike
   demuestre que funciona sobre LibVLC 3.x en Windows; nada se promete por leído en documentación.
2. **Candidatos del spike, por coste ascendente** (todos aplicables por sesión como opciones del
   medio, nunca a la instancia global — la mejora debe poder aplicarse sólo a los archivos que
   califican):
   - `sharpen` (`:video-filter=sharpen`, `:sharpen-sigma` en 0.05–2.0): nitidez tras el escalado.
   - `hqdn3d` encadenado: ruido de origen en material DVD.
   - `postprocess` (deblocking): sólo códecs antiguos (MPEG-2/DivX del corpus).
   - `swscale-mode` (bicúbico/lanczos): verificar si tiene efecto alguno con la salida D3D11 o
     sólo en la ruta por software.
3. **Qué mide el spike por candidato**: (i) que el motor acepta la opción por sesión y reproduce;
   (ii) efecto real medible — frame capturado por `IVideoFrameSource` y métrica de nitidez
   (varianza del laplaciano) antes/después sobre muestras del corpus; (iii) coste (tiempo de open,
   CPU durante reproducción). Un candidato sin efecto medible o con coste desproporcionado se
   descarta con su medición archivada.
4. **Alternativas evaluadas y no adoptadas ahora** (documentadas, nunca en silencio): la
   super-resolución por GPU (NVIDIA VSR / Intel, `d3d11-upscale-mode`) exige VLC 4.x y LibVLCSharp
   estable sigue en 3.x; cambiar de motor (mpv/madVR) es otra decisión de arquitectura. Ambas
   quedan como opciones futuras con su coste nombrado.
   **Corregido el 2026-09-11: la super-resolución no exige VLC 4.** Está en VLC 3 desde 3.0.19
   (NVIDIA e Intel) y 3.0.21 (AMD), y en el binario 3.0.23.1 que se instala; pero sólo en la salida
   `direct3d11` de VLC, que dibuja en su propia ventana. Ver la corrección en
   [la evidencia del spike](../../evidence/stable/PLY16-low-res-spike.md).
5. **Diseño de la implementación (fase 2, con lo que sobreviva del spike):**
   - `LowResolutionEnhancementPolicy` (Domain): un medio califica cuando su altura es conocida y
     **menor de 720**; la política produce la cadena de opciones aprobada. Constantes con tests.
   - Preferencia global `playback.low-res-enhancement` en `ISettingsStore`, **apagada por
     defecto** (una mejora subjetiva no se activa sola). Superficie en Ajustes → Apariencia, donde
     ya viven las decisiones visuales.
   - `PlaybackRequest` gana `MediaOptions` opcionales (lista de opciones de medio, vacía por
     defecto) y el motor las aplica al crear el medio; `OpenPlayerAsync` las pide a la política
     cuando la preferencia está activa.
   - **Honestidad en pantalla**: `VideoStatusViewModel` dice cuándo la mejora está activa, con el
     patrón del indicador de aceleración — sólo si el motor confirmó la cadena, nunca por
     intención del clic.
   - El corpus gana una muestra 480p en `eng/generate-test-media.ps1` para que MediaTests mida
     con decodificación real.
6. **Criterio de aceptación (borrador para la fila):** con la mejora activada, un vídeo que
   califica reproduce con la cadena aprobada aplicada y el indicador lo dice; con la mejora
   apagada nada cambia; el coste medido respeta los presupuestos de reproducción; el juicio
   visual final sobre una serie real lo firma el propietario en el paseo físico (lo subjetivo lo
   firma una persona, no una métrica).

## Tareas / Tasks

- [x] **Fase 1 — spike medible** con medios reales del corpus (RED: la métrica de nitidez sin
      filtros como línea base archivada): probar los cuatro candidatos, archivar medición y
      veredicto por candidato en `docs/evidence/stable/PLY16-low-res-spike.md`, y congelar la
      cadena aprobada (o concluir honestamente que ninguna paga y re-sincerar la fila a
      `DEFERRED` con el hallazgo). **Hecho 2026-08-09 (noche): ninguna paga.** Los cuatro
      candidatos quedan descartados con su medición archivada — no por falta de efecto visual,
      sino porque **nunca procesan un fotograma**: el constructor de la cadena de filtros del
      vout de VLC 3 falla al compensar formatos con la salida por callbacks (`Failed to
      compensate for the format changes, removing all filters`), con cualquier vía de activación
      (medio o instancia), decodificador (D3D11 o software) y chroma (RV32 e I420). La métrica
      demostró sensibilidad (hw vs sw difieren) y el spike queda re-ejecutable en
      `LowResEnhancementSpikeTests` (MediaTests) con la muestra 480p DVD del spike. Fila a
      `DEFERRED`; evidencia en
      [PLY16-low-res-spike.md](../../evidence/stable/PLY16-low-res-spike.md).
- [ ] ~~**Fase 2 — implementación**~~ **No se ejecuta**: su condición era implementar sólo lo
      que el spike demostrara, y el spike demostró que ningún candidato de VLC 3 funciona en
      esta ruta. Reabrir PLY-016 pasa por una de las alternativas nombradas en la evidencia
      (VLC 4, realce administrado sobre los fotogramas BGRA, u otro motor), cada una con su
      coste, y es una decisión de alcance del propietario. / **Does not run**: its condition was
      to implement only what the spike proved, and the spike proved no VLC 3 candidate works on
      this path. Reopening PLY-016 goes through one of the named alternatives, each with its
      cost, as an owner scope decision.

- [ ] **Fase 3 — la cadena D3D11, reabierta por el propietario el 2026-09-11** y ampliada a los tres
      fabricantes sin esperar a VLC 4. Su bloqueo se resolvió el 2026-09-12 —
      [PLY16-gpu-composition-harness.md](../../evidence/stable/PLY16-gpu-composition-harness.md) — y
      lo que queda por construir son cuatro eslabones, en este orden porque cada uno se mide sin el
      siguiente:

  1. **`UYVY` → `YUY2` al subir.** No es un cambio de lo que se le pide al decodificador: pedir
     `YUY2` **es una regresión ya medida** —el 2026-08-25 perdía el subtítulo entero, cero bytes de
     diferencia contra 61 687 con `UYVY`— y DXGI no tiene formato `UYVY`. Es aritmética pura, va
     junto a `PackedYuvConverter` y se prueba sin tarjeta. **Y quita trabajo**: hoy ese mismo bucle
     ya convierte cada píxel a `BGRA`, así que la línea base del coste no es cero, es lo que
     desaparece.
  2. **La textura y el procesador de vídeo**, con el realce de bordes estándar que las dos tarjetas
     declaran y que mueve el 24,4 % de la imagen en ambas. Se mide leyendo la textura de vuelta con
     una textura de staging, que es lo que `WindowsVideoUpscaleProbe` ya hace.
  3. **La importación en la composición**, por `TryGetCompositionGpuInterop` →
     `ImportImage(D3D11TextureNtHandle)` → `UpdateWithKeyedMutexAsync(image, acquire, release)`. La
     documentación de Avalonia nombra otras tres llamadas y **ninguna existe**; la ruta buena está
     medida en el ensamblado. El arnés headless no puede correr esto —contesta que no hay interop—,
     así que lo que se prueba aquí es lo que decide, con un visual de composición por software.
  4. **FSR 1 en SkSL** para las tarjetas sin superresolución propia, y **AMD**, que sigue escrita y
     sin verificar por falta de máquina.

  **Y una pregunta previa que nada del árbol contesta hoy**: qué motor gráfico elige la aplicación
  real. `Program.cs` usa `UsePlatformDetect()`, la importación D3D11 vive en la ruta ANGLE, y ningún
  test monta el anfitrión de verdad — el paseo «físico» también es headless. Decidir dónde vive esa
  sonda es lo primero del eslabón 3.

## Riesgos nombrados

- Los filtros de vídeo de VLC 3 pueden no surtir efecto en la ruta D3D11 (por eso el spike mide
  frames, no opciones aceptadas).
- La métrica del laplaciano necesita capturas estables; `IVideoFrameSource` ya existe y el paseo
  físico ya captura frames reales.
- El coste en la máquina sin gráficos integrados está por medir; los presupuestos de reproducción
  (PerformanceTests físicos) son la puerta.
