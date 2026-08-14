# La puerta ya no se cree un comentario / The gate no longer believes a comment

Evidencia de **ARQ-013**: la puerta de alcanzabilidad casaba sobre el texto crudo, así que una
referencia comentada contaba como alcanzada. / Evidence for **ARQ-013**: the reachability gate
matched raw text, so a commented-out reference counted as reached.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-14.

## Por qué este defecto pesa más que su tamaño / Why this one weighs more than its size

El defecto está **en una puerta**, y una puerta ciega no falla: aprueba. `SurfaceReachabilityTests`
existe para cazar la superficie huérfana —construida, probada y a la que nadie llega—, que es el
defecto característico de este repositorio. Casando sobre el texto sin quitar comentarios, la
superficie huérfana se esconde detrás de `<!-- -->` y la suite sigue verde **diciendo que se
alcanza**. / The defect is **in a gate**, and a blind gate does not fail: it approves.

## La medición previa / The measurement first

Antes de tocar nada, si algún comentario estaba tapando ya un huérfano de verdad. Barridos sobre
`src/ApSolutions.LocalMedia.Presentation`: ningún `<!--…-->` de un `.axaml` contiene un elemento de
vista —todos son la cabecera SPDX o prosa— y ningún comentario de línea o de bloque de un
`.axaml.cs` nombra una vista. / Measured before touching anything: nothing was hidden that way.

```
comentarios AXAML que abren un elemento de vista   0
comentarios de code-behind que nombran una vista   0
```

**La ceguera era real y no estaba cubriendo nada todavía.** Se dice porque cambia lo que significa
el verde de después: la corrección no destapó ningún huérfano, y eso es un dato, no una decepción. /
The blindness was real and was not covering anything yet, which is what the green afterwards means.

## El rojo / The red

La lectura salió de `SurfaceReachabilityTests` a `SurfaceReferences` **sin cambiar comportamiento**,
que es lo que permite medirla. Cinco pruebas contra esa lectura, tal cual estaba: / The reading moved
out to `SurfaceReferences` **with no behaviour change**, which is what makes it measurable. Five
tests against it as it was:

```
A_commented_out_element_is_not_a_reference_in_markup            [FAIL]  Expected: False  Actual: True
A_reference_commented_out_across_several_lines_is_not_a_ref…    [FAIL]  Expected: False  Actual: True
A_commented_out_type_is_not_a_reference_in_code                 [FAIL]  Expected: False  Actual: True
A_real_reference_is_still_read_as_one                           [PASS]
A_scheme_inside_a_string_does_not_start_a_comment               [PASS]
```

Las dos que ya pasaban no sobran: son la guarda contra pasarse. Una corrección que quite de más las
pondría rojas, y ese es justo el riesgo de recortar texto antes de buscar. / The two that already
passed are the guard against overcorrecting.

## La corrección / The fix

Quitar los comentarios antes de casar: `<!--…-->` en el marcado, `/*…*/` y `//…` en el código. La
forma de línea va guardada contra `://`, para que un esquema dentro de una cadena no se coma el resto
de la línea. / Strip comments before matching; the line form is guarded against `://`.

Nada más se intenta a propósito, y la razón es de dirección: **recortar de más pierde una referencia
y produce un huérfano, que falla a gritos; nunca puede inventar alcanzabilidad**. Un analizador de
verdad costaría más y erraría hacia el lado que sí importa. / Nothing further is attempted, on
purpose: over-trimming loses a reference and produces a loud orphan, and can never invent
reachability.

## Verde / Green

| Puerta / Gate | Resultado / Result |
|---|---|
| `SurfaceReferenceTests` | 5 de 5 / of 5 |
| `SurfaceReachabilityTests` | 18 de 18 / of 18 — las catorce superficies declaradas siguen alcanzables |
| `ApSolutions.LocalMedia.UiTests` | 410 de 410 / of 410 (405 + 5) |
| Huérfanos nuevos / New orphans | **0**, como la medición previa anticipaba / as measured beforehand |
