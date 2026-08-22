# La puerta que escribí para alinear las secciones midió siete de diez y pasó / The gate I wrote to align the sections measured seven of ten and passed

Segundo trabajo del tramo 7 de la §4, **y una corrección de una puerta propia**. La página de Ajustes se
alineó el 2026-08-21 con una prueba que buscaba las secciones **por el nombre de su clase**, y tres de
ellas no se llaman así. / §4's seventh tranche, and a correction of one of my own gates.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo medido / What was measured

```
H1 28  x=296  Ajustes
H2 20  x=454  Apariencia
H2 20  x=454  Recomendaciones
H2 20  x=454  Escaneo y vigilancia
H2 20  x=454  Atajos de teclado
H2 20  x=296  Estilo de subtítulos          <- 158 px a la izquierda
H2 20  x=454  Detección automática de segmentos
H2 20  x=454  Bandeja e inicio con Windows
H2 20  x=454  Privacidad y diagnósticos
H2 20  x=296  Actualizaciones               <- 158 px a la izquierda
H2 20  x=296  Créditos                      <- 158 px a la izquierda
```

**La página tiene diez secciones y tres empiezan en otro sitio.** No son vistas de ajustes por su
nombre: el estilo de subtítulos y los atajos viven en `Player/`, el actualizador en `Updates/` y los
créditos en `About/` — pero **las cinco están montadas en el mismo `ScrollViewer`**, que es lo que
decide qué son. / The page has ten sections and three start somewhere else.

## El error, que es de la prueba y no del marcado / The mistake, which is the test's and not the markup's

`SettingsPageStructureTests` buscaba el dueño de cada encabezado así:

```csharp
.FirstOrDefault(owner => owner.GetType().Name.EndsWith("SettingsView", StringComparison.Ordinal))
```

Siete de diez lo cumplen. Los otros tres se filtraban fuera, la prueba **medía siete, los encontraba
consistentes y pasaba** — mientras tres secciones de la misma página empezaban 158 px más a la
izquierda. **Es la puerta ciega en vez de falsa**, y esta vez la escribí yo el día anterior. / The test
measured seven, found them consistent, and passed.

**Una convención de nombres no es una estructura.** El panel se llama ahora `SettingsSections` y la
prueba **lo recorre**: una sección no puede escaparse por llamarse de otra manera. Y afirma el
**recuento primero** —al menos diez—, porque la versión que midió siete también encontró una sola x. /
A naming convention is not a structure; the panel is named and the test walks it, asserting the count
first.

## Y `CreditsView`, cuya fila se rechaza con lo que la pantalla tiene / And the credits row, refused with what the screen holds

La §4 pide «atribución de TMDB con su logo local; licencias en monoespaciado».

- **El logo ya estaba**, y bien: es un `Path` con la geometría oficial de TMDB, versionada al lado como
  `Assets/tmdb-logo.svg`, y `TmdbLogoTests` compara las dos y vigila que se dibuje a 16 contra los 24
  del nombre del producto. Nada que hacer. / The logo was already there, with its own gate.
- **El monoespaciado se rechaza.** `AboutLicenseNotice` es **una frase** —«AP Reelume es software libre
  publicado bajo la licencia GPL-3.0-or-later»—, no un listado. El ancho fijo es para texto cuyos
  caracteres tienen que caer unos debajo de otros; la prosa puesta en él sólo se lee peor. Los avisos de
  terceros en los que la fila piensa son **archivos dentro del paquete**, y el día que tengan pantalla
  propia será el día en que el monoespaciado les pertenezca. **Undécima discrepancia §4↔árbol.** / The
  licence line is one sentence, not a listing; prose in fixed width only reads worse.

## El verde / The green

```
UiTests             692/692
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```
