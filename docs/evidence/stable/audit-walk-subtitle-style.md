# El estilo de subtítulos, elegido y conservado / The subtitle style, chosen and kept

Los cuatro mandos del estilo de subtítulos, pulsados con el ratón en Ajustes — y la elección, que
antes moría con la ventana. / The four subtitle-style controls, pressed with the mouse in Settings —
and the choice, which used to die with the window.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 101 | **105** |
| Pendientes / Pending | 27 | **23** |

```
The walk: 129 declared command controls in 128 identities; 105 pressed, 23 pending.
```

## El defecto: una superficie desconectada por los dos extremos / Disconnected at both ends

**No lo dijo un grep, lo dijo el compilador.** Se pusieron en privado `Style`, `LoadAsync`,
`SaveAsync` y `ResetAsync` de `SubtitleStyleViewModel`, y `src/` **compiló sin un solo error**: nada
del producto guardaba el estilo, nada lo cargaba y nada lo leía. / The compiler said it, not a grep:
with those four members made private, `src/` built clean — nothing in the product stored the style,
loaded it, or read it.

El efecto entero de cuatro mandos era **un campo de un objeto** que se destruía al cerrar la ventana.
/ The whole effect of four controls was a field of one object that died with the window.

## Los dos rojos, archivados / Both archived reds

**Escribir**, con la aplicación tal como se publicaba: / Writing, with the application as it shipped:

```
dragging the subtitle size never left a size stored anywhere. 8 presses, the last at 610, 1522,
where a click reaches Border inside thumb inside PART_Track inside HorizontalTemplate inside
SliderContainer.
```

Ocho pulsaciones que **sí llegan al deslizador** —la cadena de impacto nombra el pulgar— y no dejan
nada. / Eight presses that do reach the slider, and leave nothing behind.

**Leer**, con la mitad que escribe ya corregida: / Reading, with the writing half already fixed:

```
the style stored before this window opened never reached the screen it belongs to
```

## La corrección, que son dos mitades / The fix, which is two halves

- **Se guarda donde se cambia.** Todos los setters pasan por `Update`, así que es el único sitio desde
  el que se puede almacenar — y almacena **sin que nadie se lo pida**, porque nada en la aplicación
  iba a pedírselo. La carga queda excluida: mostrar lo que ya está guardado no es un cambio que
  guardar. / It stores where it changes, and loading is excluded because showing what is already
  stored is not a change worth storing back.
- **Se carga sobre la instancia que el shell enseña.** Este modelo de vista es **transitorio**, así que
  resolver uno nuevo habría llenado correctamente una pantalla que nadie mira. Se carga el que el
  shell recibió. / It loads onto the instance the shell was handed: this view model is transient, so a
  freshly resolved one would have been filled correctly and shown to nobody.

## La escena: un viaje de ida y vuelta, no una pulsación / The scene: a round trip

1. Se guarda un estilo **antes** de que la ventana arranque, como lo dejaría una sesión anterior.
2. Se llama a `ConfigureWindow`, que es el arranque que tiene una persona, y se espera a que la
   pantalla muestre **lo guardado** y no lo de fábrica.
3. Se arrastran los tres deslizadores y se abre el desplegable, **con la sonda en la base de datos**.
4. Se lee la fila: los tres valores cambiaron, y **el color de texto que nadie tocó sigue igual** —
   el estilo es un campo de una fila que también lleva las pistas, así que guardarlo tiene que dejar
   el resto en paz.

La sonda es **lo almacenado**, no el modelo de vista. Es justo la regla que esta superficie rompía:
afirmar sobre el campo habría probado que el campo conserva lo que se le mete. / The probe is what is
stored, not the view model — asserting on the field would have proved the field keeps what is put in
it.

## Lo que sigue faltando, dicho y no implicado / What is still missing, said rather than implied

**El estilo llega a la base de datos y no llega a la imagen.** LibVLC toma su dibujado de subtítulos
de las opciones con las que se construye la instancia, y esta aplicación construye **una instancia
cacheada por juego de opciones** (`LibVlcFactory`), sin ninguna opción de subtítulos. Aplicar un
estilo elegido es, por tanto, trabajo aparte — y de lo que sólo confirma **una pantalla física**, así
que va al paseo de diez minutos del propietario. / The style reaches the database and not the picture:
LibVLC takes its subtitle rendering from the options its instance is built with, and this application
builds one cached instance per option set, with no subtitle options in it.

**Y eso pide revisar `A11Y-002` en el corte de versión.** Su nota dice «ofrece controles de tamaño,
fondo y contraste», que es literalmente cierto y fue literalmente verificado; pero quien lee
«subtítulos personalizables» entiende que los subtítulos se ven distintos. Se decide en el corte, con
el manifiesto regenerado, que es donde ese cambio de estado cuesta lo que cuesta. / This asks for
`A11Y-002` to be revisited at the version cut: its note says it offers controls, which is literally
true, but "customizable subtitles" reads as subtitles that look different.

## Las puertas / The gates

```
dotnet format --verify-no-changes --severity warn          # limpio / clean
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1   # 0 avisos / 0 warnings
eng/run-accessibility.ps1 -Mode Verify -Passes 2           # 110 + 110, 0 críticos / 0 critical
dotnet test tests/ApSolutions.LocalMedia.UiTests            # 448
eng/check-walk-coverage.ps1                                # 105 pulsados, 23 pendientes / pressed, pending
```

## Cómo se reprodujo / How it was reproduced

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet build ApSolutions.LocalMedia.sln -c Release -warnaserror -m:1
./eng/run-accessibility.ps1 -Mode Verify -Passes 2
./eng/check-walk-coverage.ps1
```
