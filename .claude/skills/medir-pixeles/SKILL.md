---
name: medir-pixeles
description: Rasteriza un control de Avalonia y cuenta pixeles de tinta, para medir lo que se ve en vez del layout. Usar cuando se investigue algo VISIBLE — alineacion, tamano, contraste, centrado — porque Bounds y las metricas de fuente describen el modelo y el defecto puede vivir solo en el pixel.
---

# Medir el píxel, no el layout

`Bounds`, `TranslatePoint` y las métricas de una fuente describen **el modelo**. Si lo que se
investiga es algo que alguien **ve**, hay que rasterizar.

**Por qué existe esta skill**: el 2026-08-29 los botones de esta aplicación dibujaban su icono y su
palabra **2 px separados** con **dos puertas verdes encima**. `ButtonInkTests` medía la caja y
`ButtonOpticalCentreTests` la tinta *calculada* desde las métricas. Ninguna dibujaba nada.

## El arnés

Funciona porque `TestAppBuilder` levanta Skia de verdad con `UseHeadlessDrawing = false`.

```csharp
using var frame = window.CaptureRenderedFrame()
    ?? throw new InvalidOperationException("el backend no devolvio frame.");
using var buffer = frame.Lock();

var bytes = new byte[buffer.RowBytes * frame.PixelSize.Height];
System.Runtime.InteropServices.Marshal.Copy(buffer.Address, bytes, 0, bytes.Length);

// La primera y la ultima fila con un pixel mas oscuro que el umbral, en una banda de columnas.
for (var y = 0; y < frame.PixelSize.Height; y++)
{
    for (var x = fromX; x < toX; x++)
    {
        var i = (y * buffer.RowBytes) + (x * 4);
        if (bytes[i] < threshold && bytes[i + 1] < threshold && bytes[i + 2] < threshold) { /* tinta */ }
    }
}
```

## Las cinco trampas, todas medidas

1. **Dos umbrales, no uno.** El fondo de un botón está *entre* el papel y la tinta: `< 250` encuentra
   el borde del botón y `< 110` sólo la tinta. Con un umbral solo se mide el botón creyendo medir el
   texto — daba «filas 18..61» en los dos.
2. **Un control invisible mide cero, y cero pasa cualquier comparación.** Una barra con
   `IsVisible=false` respondió `Bounds.Height == 0` y la aserción de «tres píxeles» se cumplió por la
   razón contraria. Toda puerta de este tipo necesita su suelo antiblindaje: que encontró lo que
   compara **antes** de restar.
3. **La palabra va como parámetro.** El centro de la tinta no es propiedad de la fuente sino de la
   cadena: en Inter a 14 px va de `+0,62` («Guardar el informe») a `+3,82` («ppp») según lleve
   descendente, y se mueve al traducir. Una puerta que fije una palabra certifica una compensación
   calibrada para esa palabra. Usa al menos una con descendente y otra sin, en los dos idiomas.
4. **La API no es la que parece.** `IGlyphTypeface` no es público, `FontMetrics` no expone
   `CapHeight`, `GlyphTypeface` no lleva `GetGlyphMetrics`, y `Shape` necesita castearse a `Visual`
   para `TranslatePoint`. Seis vueltas de compilación costó averiguarlo — consulta el MCP de Avalonia
   antes (regla 0).
5. **El escenario pinta sus propios colores.** Un `Border` blanco y tinta negra explícita hacen que
   los umbrales sobrevivan a un cambio de tema. Si tomas los colores del diccionario, la puerta se
   rompe cuando la paleta se mueva.

## El ejemplo vivo

`tests/ApSolutions.LocalMedia.UiTests/Theme/ButtonPixelCentreTests.cs` es el arnés completo y
funcionando: escena, escaneo con los dos umbrales, suelo antiblindaje y la palabra como parámetro.
Cópialo de ahí antes de escribirlo de nuevo.
