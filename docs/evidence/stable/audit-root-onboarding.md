# Tres botones cambiaban un ajuste que la pantalla no enseñaba, y el estado más común de la primera pantalla no decía nada / Three buttons set a choice the screen never showed, and the first screen's commonest state said nothing

Quinto y último trabajo del tramo 7 de la §4. / §4's seventh tranche, fifth and last piece.

Fecha / Date: 2026-08-22. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El tipo de raíz que se elegía a ciegas / The kind chosen blind

`SelectKindCommand` escribía `SelectedKind`, y **ninguna vista leía la propiedad**: medido el 2026-08-22, el
único `SelectedKind` de un `.axaml` es el del editor de marcadores, que es otro modelo. Pulsar «USB»
dejaba la pantalla **exactamente igual** que pulsar «Local». Y el campo arranca en `Local`, así que
tampoco había un momento inicial sin nada elegido que hiciera la ausencia evidente. / No view read the
property, and the field starts at Local, so there was not even an empty moment to make the absence
obvious.

La forma ya estaba decidida en esta casa: los tres pasan a llevar el círculo `●`/`○` con la clase
`state-glyph` dentro de un `WrapPanel` con `theme-option`, que es **literalmente** lo que las pastillas
de tema y de idioma son. / The shape was already decided here.

## Y el contenedor, por novena vez / And the container, for the ninth time

Iban en un `StackPanel` horizontal, que ofrece anchura infinita a sus hijos y los dibuja donde caigan.
Medido el 2026-08-22 en la columna de 720: **262 px en español y 267 en inglés**, así que hoy caben en
una línea — y queda escrito para que nadie lo revierta creyendo que ahorra espacio. **No ahorra
espacio**; lo que compra es que el largo de «UNC o NAS» / «UNC or NAS» pertenezca a quien traduce.
/ They fit today; the change buys the translator's freedom, not pixels.

## La etiqueta que sólo oía el lector de pantalla / The label only the screen reader heard

`RootPathLabel` está escrita en los dos idiomas y **la gastaba únicamente `AutomationProperties.Name`**.
El lector decía «Ruta de la carpeta»; la pantalla no decía nada. Es el defecto que el editor de
metadatos tenía en ocho campos, y se corrige con la misma forma que allí. / Written in both languages
and spent by the automation name alone.

## Las tres superficies con un solo pincel / The three surfaces with one brush

Un rechazo, una carpeta a punto de salir del catálogo y una petición de permiso se pintaban las tres
sobre `AccentSubtleBrush`. Ahora dicen tres cosas distintas: / Three things, three surfaces:

| Superficie / Surface | Pincel / Brush | Por qué / Why |
| --- | --- | --- |
| Fallo al añadir | `WarningSurfaceBrush` | Las tres frases se explican solas — «ya está en la biblioteca y no se ha tocado nada» — y eso es algo que saber, no algo roto. Es la superficie de los tres mensajes del editor de metadatos. |
| Confirmación de retirada | `DangerSurfaceBrush` | La §4 lo pide, y es la única de las tres que destruye algo y la única que pide autorización. |
| Consentimiento del escaneo | `AccentSubtleBrush` | Se queda, **decidido y no heredado**: el audit de la recuperación nombra este mismo consentimiento como lo que el acento es. |

La prueba afirma cada pincel **y además que difiere del acento que era**, porque comparar sólo contra el
que debe ser aprueba igual si alguien los vuelve a igualar. / Each is asserted against the accent it
was, not only against the brush it should be.

## La cuarta forma, que no existía / The fourth form, which did not exist

SURFACES lista cuatro formas para esta vista —sin raíces, con raíces, confirmando borrado y pidiendo
consentimiento— y **«sin raíces» no tenía nada que pintar**: con el catálogo vacío el encabezado y las
filas simplemente no estaban. Es **el estado inicial de la pantalla de primeros pasos**, es decir el más
común que existe, y no decía absolutamente nada. / The first form had nothing to paint, and it is how
the screen starts.

Acento y no positivo: una bandeja de revisión vacía es un logro y lleva la superficie positiva; una
biblioteca vacía es donde empieza todo el mundo y queda trabajo por hacer, así que pintarla como
terminada sería decir lo que no es. / An empty library is not an achievement.

## Las rutas: la medición que evitó copiar la solución de al lado / The paths, and the measurement that stopped a borrowed fix

La vista previa de restauración trunca sus rutas con `PathSegmentEllipsis`, y la tentación era repetirlo.
Medido el 2026-08-22 en la columna de 720, con el botón «Retirar» al lado: / Measured with Remove beside
it:

```
 12 caracteres  Wrap                 texto 649 x 17   fila 36
 12 caracteres  PathSegmentEllipsis  texto 649 x 17   fila 36
 74 caracteres  Wrap                 texto 649 x 17   fila 36
 74 caracteres  PathSegmentEllipsis  texto 649 x 17   fila 36
139 caracteres  Wrap                 texto 649 x 33   fila 36
139 caracteres  PathSegmentEllipsis  texto 649 x 17   fila 36
```

Hasta 74 caracteres **son idénticos**. A 139 el envoltorio cuesta una segunda línea de 33 px, que **cabe
dentro de los 36 que el botón ya impone**: la fila no crece. Truncar no compraría nada y costaría el
final de la ruta, que es lo que distingue dos carpetas de alguien. Allí la columna son 600 px
compartidos con una caja de texto; aquí la ruta tiene 649 para ella sola. **Mismo problema aparente,
geometría distinta, decisión distinta.** Ganan la familia monoespaciada y `WrapWithOverflow`, que es lo
que la pantalla de recuperación ya usa para lo mismo. / Same apparent problem, different geometry,
different answer.

## ⚠ Y un control que se veía y no se podía pulsar / And a control you could see and could not press

El paseo físico se puso en rojo con este cambio, **determinista**: verde en 4 s con el árbol limpio,
rojo en 66 s con él, ocho pulsaciones sin efecto. No era un defecto de la vista: era uno que la vista
destapó al crecer 25 px. / Deterministic, and not a defect of the view but one the view uncovered.

Medido dentro del paseo, en su ventana de 1600 × 1000: / Measured inside the walk:

```
botón «Revisar versiones»  en y=939, alto 36  →  su centro cae en 957
ScrollViewer de biblioteca  desplazamiento 0, extensión 927, viewport 904
```

El viewport de ese `ScrollViewer` acaba en **952**. Trece de los treinta y seis píxeles del botón
estaban dentro y **su punto medio no**, así que un clic sobre él llegaba al `Grid` del shell que hay
detrás. El desplazamiento disponible eran **23 px para un control que necesitaba 23**, y `BringIntoView`
lo dejaba donde estaba porque ya lo consideraba a la vista. / Thirteen of its thirty-six pixels were
inside and its middle was not.

**Una pila cuyo último hijo termina a ras del borde del viewport está a un cambio de diseño de esto
siempre**, y el panel de carpetas que tiene encima creció justo esa distancia. El `StackPanel` de la
ruta de biblioteca gana `Margin="0,0,0,24"`: el colchón es el derecho de un control a ser pulsado.
/ The bottom margin is a control's right to be pressed.

## ⚠⚠ Y el arnés estaba CIEGO, no equivocado / And the harness was BLIND, not wrong

El margen arregló la ejecución aislada y **la suite completa siguió fallando una de cada dos veces**,
siempre en el mismo punto. Ahí estaba la causa de verdad, y no era la geometría: era `Fits`, la función
con la que `Reveal` decide **si hace falta desplazar**. / The margin fixed the isolated run and the full
suite kept failing half the time, which is where the real cause was.

Preguntaba **sólo por la ventana**:

```csharp
centre.X < window.Bounds.Width && centre.Y < window.Bounds.Height
```

Y un `ScrollViewer` **recorta su contenido**. Un control puede estar holgadamente dentro de la ventana y
estar cortado por el visor en el que vive: el botón en 957 estaba **dentro de la ventana de 1000** y
**cinco píxeles fuera del viewport que acaba en 952**. `Fits` contestaba «cabe», `Reveal` no desplazaba
nada, y las ocho pulsaciones iban a lo que el recorte dejaba detrás. / A scroller clips its content, so
a control can sit well inside the window and be cut off by the viewer it lives in.

Es la forma que este repositorio ya tiene nombrada: **una prueba se vuelve ciega en vez de falsa**. No
afirmaba nada falso; simplemente no miraba donde había que mirar, y por eso el fallo se leía como «el
botón no funciona» en vez de «el arnés no lo ha traído a la vista». `Fits` pasa a exigir además que el
punto esté dentro de **cada** visor entre el control y la ventana. Eso **endurece** la puerta: ahora
`Reveal` desplaza en los casos en que antes se quedaba quieto. / A test goes blind rather than false.

Tres pasadas completas seguidas de la suite, **135/135 las tres**, y el ledger en **135 pulsados de 136
declaraciones, 0 pendientes**. / Three consecutive full passes, and the ledger unchanged.

**Y la vista de carpetas mide 426 px de los 904 del viewport, siempre**, porque `HasOnboarding` es
`Onboarding is not null` y por tanto nunca es falso: casi la mitad de la ruta de biblioteca es un panel
de «Añade tus carpetas» encima de la biblioteca de quien ya tiene quinientas. Eso es el shell y no esta
vista, y queda anotado con su número. / Written down with its number: that one belongs to the shell.

## El verde / The green

```
UiTests             702/702
AccessibilityTests  135/135
IntegrationTests    456/457, 1 omitida por diseño / 1 skipped by design
DocumentationTests   87/87
dotnet format       sin cambios / no changes
dotnet build        0 advertencias con -warnaserror / 0 warnings
```

Los 720 px se quedan y también están medidos: son la columna de las cuatro vistas a pantalla completa
—`StartupView`, `ReviewInboxView`, `DatabaseRecoveryView` y ésta— y la §4 misma llama «los 720 px» a la
de recuperación. 620 es el ancho de una sección de ajustes, que es otro grupo. / The 720 stays, and it
is measured too.
