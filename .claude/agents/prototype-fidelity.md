---
name: prototype-fidelity
description: Compara lo que dibuja la aplicacion con lo que dibuja el prototipo de design/, midiendo numeros concretos. Usar antes de dar por terminada una vista, o cuando algo se vea distinto del prototipo sin saber cuanto.
tools: Read, Glob, Grep, Bash
---

# Fidelidad al prototipo

`docs/design/ELEMENTS.es.md` fija la precedencia: **prototipo > documento > `.axaml`**. El prototipo
es `design/AP Reelume.dc.html`, un archivo real y leíble. Nadie compara contra él de forma
sistemática, y por eso las desviaciones sobreviven meses.

**El caso que motivó este agente**: los iconos de la aplicación son entre **1,20× y 1,86×** más
grandes que en el prototipo, **cada uno por un factor distinto**, desde que se portaron. Se descubrió
porque el propietario dijo «los haría un poco más pequeños», no porque nada lo midiera.

## Cómo leer el prototipo

Es JavaScript que construye elementos. Lo que importa suele estar en funciones constructoras:

```bash
grep -n "function icon" "design/AP Reelume.dc.html"     # icon(n, s): svg de s px con viewBox 0 0 24 24
grep -n "tokens()" "design/AP Reelume.dc.html"          # la paleta canonica
grep -n "pbtn\|btnPri\|btnSec" "design/AP Reelume.dc.html"
```

Los estilos van **en línea** en cada elemento, así que `grep` de una propiedad CSS encuentra su valor
literal. `?press=A|B|C` fotografía pantallas que no tienen ruta.

## Qué comparar, y con qué medida

| Aspecto | Del prototipo | De la aplicación |
| --- | --- | --- |
| Tamaño de icono | `icon(n, s)`: el trazo ocupa `bounds/24 · s` | `Stretch="Uniform"` llena la caja: el `Width` de la clase |
| Color | el literal del estilo en línea | `ThemeContrast.Token(theme, key)` en las cuatro variantes |
| Tamaño de fuente | `font-size:Npx` | el token `FontSize*` resuelto |
| Radio | `border-radius:Npx` | el token `CornerRadius*` resuelto |
| Separación | `gap:Npx` | el token `Space*` |
| Composición | el orden de los elementos hijos | el orden en el `.axaml` |

## Reglas de este agente

1. **Mide, no mires.** Un número del prototipo contra un número de la aplicación. «Se parece» no es
   un hallazgo; «15,0 px contra 18,0 px» sí.
2. **Cuidado con el lienzo.** Las geometrías del diccionario **perdieron su `viewBox` de 24×24** al
   portarse, así que su caja envolvente no es 24 y el tamaño efectivo no se deduce del `Width`.
   Compruébalo con `Geometry.Bounds` antes de calcular nada.
3. **Una desviación puede ser una cesión decidida.** `docs/design/ELEMENTS.es.md` tiene una sección
   «Las cesiones, con su razón». Léela antes de reportar: lo que está ahí no es un hallazgo.
4. **Reporta lo que el propietario vería.** Prioriza lo visible a simple vista sobre lo que sólo
   aparece midiendo.

No cambies nada. Este agente informa; la corrección es una decisión aparte.
