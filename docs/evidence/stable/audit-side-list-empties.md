# Cuatro listas en una columna, y «vacío» resultó ser tres cosas distintas / Four lists in one column, and "empty" turned out to be three different things

Cuarto trabajo del tramo 4 de la §4. Las ocho cadenas salen del paquete tal cual; lo que costó medir
fue **qué significa vacío en cada una**. / The eight strings come from the package; what took measuring
was what "empty" means in each.

Fecha / Date: 2026-08-21. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El estado de partida / Where it started

```
Each_of_the_four_side_lists_says_what_its_own_empty_means
  MarkersEmptyTitle is not declared, so nothing can paint it.
```

Ninguna de las cuatro decía nada. Quien abría el selector de pistas sobre un archivo con una sola
pista de audio encontraba un panel sin nada dentro, **y sin forma de distinguir eso de algo que aún
está cargando**. / Nothing to tell an empty panel from one still loading.

## Los tres significados / The three meanings

1. **Marcadores y detecciones: vacío es cero.** El estado se deriva de la lista y **la lista es quien
   lo anuncia** (`CollectionChanged`), porque si cada camino que añade o limpia tuviera que acordarse,
   el que se olvidara dejaría el panel diciendo que está vacío sobre una lista con algo dentro. /
   Derived from the list, announced by the list.
2. **El selector de pistas NUNCA llega a cero.** Su lista de subtítulos lleva **siempre** la opción
   «desactivado» que el propio modelo añade, así que contar elementos definiría un estado que **no
   ocurre jamás**. Su vacío es **una sola opción real por tipo** —`AudioTracks.Count <= 1 &&
   SubtitleTracks.Count <= 2`—, que es exactamente lo que dice el texto del paquete: «este archivo trae
   una sola pista de cada tipo». **Y la frase va ENCIMA de los dos desplegables, no en su lugar**: la
   pista que cada uno tiene sigue siendo la que suena, y alguien puede querer leer cuál es. Lo que
   falta es la elección, no la información. / The list never reaches zero, so counting items would
   define a state that cannot happen.
3. **La lista de versiones lo resolvía al revés que la §4 y gana la §4.** Hoy la vista entera se
   ocultaba con `IsVisible="{Binding HasAlternatives}"` —que es **ausente**— y el paquete le pide una
   frase, que es **presente y vacío**. La razón está en dónde vive: esta lista está en una columna
   junto a otras tres, **y una que desaparece mueve las demás**; quien pregunta «¿hay otra versión de
   esto?» merece leer «una sola» en vez de no encontrar dónde estaba. **Es la excepción a la gramática
   de ausente de esta casa, y se anota como tal** porque contradice a `PrivacySettingsView`. / §4 wins
   this one, and the exception is recorded rather than assumed.

**Y donde la lista se sustituye, se sustituye:** en marcadores y detecciones el `ListBox` se retira
cuando aparece su explicación, porque un `MinHeight` de 96 px dejaría el hueco vacío **debajo** del
texto que explica que está vacío. / Where the sentence replaces the list, the list steps aside.

## El verde / The green

```
UiTests             633/633
AccessibilityTests  135/135 en las dos pasadas / on both passes
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
El paseo / the walk: 136 declaraciones en 135 identidades; 135 pulsadas, 0 pendientes
```

Con esto van **10 de las 22 cadenas de vacío del paquete** gastadas: dos de la biblioteca y estas
ocho. / Ten of the package's twenty-two empty strings are now spent.
