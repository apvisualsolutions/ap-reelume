# La puerta de los escalares gastados / The gate on scalars that get spent

«Declarado y nunca alimentado» tenía puerta para los servicios del contenedor y ninguna para los
números del tema, que es donde más barato sale cometerlo. Tres se habían colado, y dos de ellos eran
**una copia paralela de un número que la aplicación toma de otro sitio**. / "Declared and never fed"
had a gate for services in the container and none for the theme's numbers, which is where it is
cheapest to commit. Three had got in, and two of them were a parallel copy of a number the
application takes from somewhere else.

Fecha / Date: 2026-08-19. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo medido / What was measured

Consumo real de cada escalar, contando **cualquier `.axaml` de `src/`**, el propio archivo de tokens
incluido: / Real consumption of each scalar, counting **any `.axaml` under `src/`**, the token file
included:

```
FocusStrokeThickness                11        SpaceXSmall …  SpaceXLarge        0
CornerRadiusSmall                    2        CornerRadiusMedium                0
FocusInnerStrokeThickness            1        MotionDurationStandardMilliseconds 0
ControlHeight                        1        MotionDurationReducedMilliseconds  0
                                              TextControlPlaceholderOpacity      0  (lo gasta el tema base)
                                              SelectedStateGlyph                 0
```

## Los tres que se borran, y por qué no es limpieza / The three deleted, and why it is not tidying

- **`MotionDurationStandardMilliseconds` y `MotionDurationReducedMilliseconds`.** Ningún AXAML los
  lee, y `FluentThemeService` tiene su propio `TimeSpan.FromMilliseconds(160)`. **Dos números iguales
  en dos sitios, y dos pruebas afirmando sobre la copia** —una en `ContrastTokenTests` y otra en
  `ReducedMotionTests`—, mientras el que la aplicación usa no lo miraba nadie. / Two equal numbers in
  two places, and two tests asserting about the copy while nothing watched the one in use.
- **`SelectedStateGlyph`.** El `●` está **literal en seis sitios** —uno en AXAML y cinco en modelos de
  vista— y ni `○` ni `◐` tienen recurso: la abstracción estaba a medias y nadie la usaba. El glifo es
  un dato del modelo de vista, no del tema. / The dot is literal in six places and neither of its two
  siblings has a resource: the abstraction was half-made and unused.

## La garantía no se pierde, se muda / The guarantee is not lost, it moves

Lo que aquellas dos pruebas protegían —«el movimiento reducido es cero y el estándar es corto»— pasa
a `ThemeTests`, sobre `FluentThemeService.MotionDuration`, **que es el número que la aplicación
pregunta**. La mitad de cero ya estaba allí; la otra mitad es nueva. / What those tests protected
moves onto the service's own property, which is the number the application asks for. The zero half
was already there; the short half is new.

`ReducedMotionTests` conserva intacta la que sí mide algo —que **ninguna vista escribe una duración
propia**— y ahora dice, donde se lee, que el token se declarará cuando la primera transición lo
necesite, con el servicio leyendo de él. / The audit that measures something real is untouched, and
now says where the number lives.

## La puerta / The gate

`ScalarTokenTests`, dos pruebas:

1. **Todo escalar declarado está gastado**, o nombrado en una de dos listas: la del tema base —hoy
   uno, `TextControlPlaceholderOpacity`— o la de **los que aún no se gastan**, que hoy son **seis**
   (`SpaceXSmall`, `SpaceSmall`, `SpaceMedium`, `SpaceLarge`, `SpaceXLarge`, `CornerRadiusMedium`) y
   que las vistas vaciarán.
2. **La lista sólo encoge**: falla si un nombre suyo **empieza a gastarse** —hay que quitarlo— y falla
   si **deja de estar declarado** —la lista describiría un tema que ya no existe—. Lo mismo para la
   del tema base, al revés: si un archivo nuestro empieza a leer uno, deja de ser del tema base.

**Lo que hay que excluir está escrito como lista de lo que NO es un escalar** (pinceles, colores,
redirecciones, diccionarios, temas de control), no como lista de tipos válidos: así un escalar de un
tipo que nadie ha usado todavía queda vigilado **desde el día en que aparece** en vez de pasar
inadvertido. / The exclusion is written as what is *not* a scalar rather than as a list of valid
types, so a scalar of a type nobody has used yet is watched from the day it appears.

**Y lleva suelo anticeguera**: si el analizador dejara de encontrar los tokens, la puerta pasaría por
no medir nada. Exige al menos ocho escalares y que `FocusStrokeThickness` y `CornerRadiusSmall`
consten como gastados. / And it carries an anti-blindness floor: if the parser stopped finding
tokens, the gate would pass by measuring nothing.

## Probada fallando, en tres direcciones / Proved failing, in three directions

Mutaciones aplicadas y revertidas: / Mutations applied and reverted:

```
1. un escalar nuevo que nadie gasta / a new scalar nobody spends
   "ProbeScalarNobodySpends — declared in the theme and read by no .axaml under src/…"
2. uno de la lista que empieza a gastarse / a listed one that starts being spent
   "SpaceMedium is on the unspent list and something now spends it. Take it off the list…"
3. uno de la lista que deja de existir / a listed one that stops existing
   "CornerRadiusMedium is on the unspent list and is not declared any more…"
```

## Lo que CI pidió después, y es la puerta funcionando / What CI asked for afterwards

El run de este commit **falló**, y no por una prueba: las trece suites pasaron y lo que se quejó fue
**el trinquete de cobertura**. / The run failed, and not on a test: all thirteen suites passed and the
coverage ratchet is what complained.

```
src/ApSolutions.LocalMedia.Presentation/Theme/FluentThemeService.cs now reaches 90/69;
raise its floor
```

**El trinquete falla en las dos direcciones**, y ésta es la de mejorar: la prueba nueva
—`Motion_that_is_allowed_is_short_rather_than_absent`, la mitad de la garantía que se mudó aquí— cubre
código de `FluentThemeService` que antes no medía nadie, y lo lleva de **88/65 a 90/69**. Mudar la
garantía al sitio donde vive el número **mejoró la cobertura del número**, que es el argumento de por
qué el cambio era bueno, dicho por la máquina. / The ratchet fails in both directions, and this is the
improving one: moving the guarantee to where the number lives improved that file's coverage, which is
the argument for the change, made by the machine.

El suelo se sube **copiando entero el artefacto `coverage-debt` de ese run**, nunca a mano: una sola
línea cambió. / The floor is raised by copying the run's `coverage-debt` artifact whole.

## El verde / The green

```
ScalarTokenTests                              2/2
ApSolutions.LocalMedia.UiTests              589/589
ApSolutions.LocalMedia.AccessibilityTests   133/133  (eran 135: dos vigilaban la copia / were 135,
                                                      two of them watched the copy)
dotnet build -c Release -warnaserror          0 advertencias / 0 warnings
dotnet format --verify-no-changes             limpio / clean
```
