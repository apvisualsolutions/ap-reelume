# Ajustes no son siete páginas: es una página con siete secciones, y la medición ensamblada lo dijo / Settings is not seven pages but one page with seven sections, and the assembled measurement said so

Tercer trabajo del tramo 5 de la §4, **y una corrección del segundo**. La §4 describe una geometría y
la llama «mismo esqueleto»; aplicarla vista por vista produjo una página con **cuatro encabezados de
nivel 1** y un escalón de 158 px por el medio. / §4's fifth tranche, and a correction of its own
previous step.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## ⚠ Lo que este cambio corrige del anterior / What this corrects in the one before it

El commit anterior subió el título de tres vistas de ajustes a `FontSizeTitle` (28) porque la §4 dice
«título 28», **y la §4 describe cada vista por separado**. Medido sobre el shell ensamblado, las siete
vistas de ajustes están **apiladas en un solo `ScrollViewer`**: son secciones de una página, no páginas.
/ Measured on the assembled shell, all seven settings views are stacked in one `ScrollViewer`.

```
H1 28  x=454  Apariencia                          <- cuatro secciones reclamando el nivel 1
H2 20  x=454  Idioma
H1 28  x=454  Recomendaciones
H2 14  x=454  Umbral de «visto»
H1 28  x=454  Escaneo y vigilancia
H2 20  x=296  Atajos de teclado                   <- y tres alineadas 158 px a la izquierda
H1 28  x=454  Detección automática de segmentos
H2 20  x=296  Bandeja e inicio con Windows
H2 20  x=296  Privacidad y diagnósticos
```

**Cuatro landmarks de nivel 1 dentro de un mismo destino**, para quien salta por encabezados con un
lector de pantalla. Y un escalón de 158 px por el medio de una página cuyas secciones son iguales. /
Four top-level landmarks inside one destination, and a 158 px step down the middle of a page whose
sections are peers.

**Lo que la §4 no podía ver es el ensamblado.** Sus artboards muestran una vista cada uno, y ahí cada
una parece una página. La regla, que ya se había pagado con `LibraryEntryView`: **una decisión sobre
jerarquía se mide sobre la pantalla ensamblada, que es la mitad que una tabla por vista no puede ver.**
/ A decision about hierarchy is measured on the assembled screen.

## Lo que queda ahora / What it is now

```
H1 28  x=296  Ajustes                             <- la página, que no tenía encabezado ninguno
H2 20  x=454  Apariencia
H3 14  x=454    Idioma
H2 20  x=454  Recomendaciones
H3 14  x=454    Umbral de «visto»
H2 20  x=454  Escaneo y vigilancia
H2 20  x=454  Atajos de teclado
H2 20  x=454  Detección automática de segmentos
H2 20  x=454  Bandeja e inicio con Windows
H2 20  x=454  Privacidad y diagnósticos
H3 14  x=454    Conexiones que la aplicación puede hacer
```

**Una página, un nivel 1, siete secciones iguales y alineadas.** El nombre de la página es
`NavigationSettings` —el que el carril de navegación ya usa— porque **un destino y la página que abre
son la misma cosa**, y una cadena nueva para decir lo mismo sería una cadena que puede divergir. / One
page, one level one, seven aligned peers; the page's name is the destination's, because a new string
saying the same thing is a string that can diverge.

**Y la mitad del cambio anterior que era correcta se queda**: la superficie con `Padding 32` y la
columna de 620 px **son de todas las secciones**, no de las tres que la §4 nombra. Las tres que no la
tenían —atajos, bandeja y privacidad— la reciben aquí, que es lo que las alinea. / The half of the
previous change that was right stays: the padded surface and the 620 column belong to every section.

## Lo que se afirma, y por qué «al menos uno» no bastaba / What is asserted, and why "at least one" was not enough

`NarratorMetadataTests` ya exigía que **cada superficie declare al menos un encabezado**, y las cuatro
lo cumplían mientras estaban mal. La puerta nueva afirma **exactamente uno** de nivel 1 en la página,
ninguno dentro de una sección, y **un solo valor de x** para los siete títulos de sección. / The
existing gate asked for at least one heading, which is what the four satisfied while being wrong.

Los niveles 3 anidados —la vista previa de diagnósticos vive en una superficie propia dentro de
privacidad— quedan fuera de la comparación de x **a propósito y por escrito**: están indentados porque
están dentro. / Nested level threes are excluded from the x comparison on purpose and in writing.

## El verde / The green

```
UiTests             669/669
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
ArchitectureTests   30/30
DocumentationTests  87/87
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```
