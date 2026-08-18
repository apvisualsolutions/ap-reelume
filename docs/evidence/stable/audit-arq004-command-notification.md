# El comando que nadie escuchaba / The command nobody listened to

`ARQ-004`, cerrado el 2026-08-18: el botón «Volver» de la biblioteca pasa a usar su comando, el
comando aprende a anunciarse, y una puerta impide que aparezca un noveno. / The library's Back button
now uses its command, the command learns to announce itself, and a gate stops a ninth from appearing.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

El rojo, medido con la escena del paseo `The_library_is_browsed_with_the_mouse` en el momento en que
los dos botones quedaron enlazados al comando y antes de que el comando supiera anunciarse: / The red,
measured the moment both buttons were bound to the command and before the command could announce:

```
Volver a la biblioteca is on screen but cannot be pressed: visible=True, enabled=False.
```

Con la notificación, la misma sonda: / With the notification, the same probe:

```
Correctas! - Con error: 0, Superado: 1   (The_library_is_browsed_with_the_mouse)
Correctas! - Con error: 0, Superado: 117 (ApSolutions.LocalMedia.AccessibilityTests, 2 m)
Correctas! - Con error: 0, Superado: 449 (ApSolutions.LocalMedia.UiTests)
```

## La nota heredada era cierta, y su explicación era falsa / The inherited note was true for the wrong reason

La cola decía que `LibraryViewModel.BackCommand` era el único de los ocho con riesgo real —cierto— y
que **hoy no muerde porque «la vista se hace visible y el botón vuelve a preguntar»**. Eso último se
midió y es falso. Lo que ocurría es más simple: / The queue said the button did not bite because the
view becomes visible and the button asks again. Measured, that is false. What was happening is
simpler:

```xml
<Button HorizontalAlignment="Left" Click="OnBackClick" ... />
```

**Ningún AXAML enlazaba `BackCommand`.** Los dos botones llamaban a `BackToLibrary()` por el
code-behind, así que el predicado `Surface != LibrarySurface.Browse` no se evaluaba nunca en la
aplicación. Un comando público, construido, con predicado, que ninguna vista consumía: el defecto de
la casa —registrado y nunca alimentado— en la cara de un comando. / No AXAML bound `BackCommand`. Both
buttons went through code-behind, so the predicate was never evaluated in the running application.

Y una segunda razón por la que el rojo no podía existir tal como estaba escrito: cada botón vive
**dentro del `Grid` de su propia superficie**, así que mientras es visible el predicado es
verdadero por construcción. La hipótesis pedía un «Volver» apagado cuando debería estar vivo, y en el
árbol de entonces eso no podía pasar. / Each button lives inside the grid of its own surface, so
while it is visible the predicate is true by construction.

## Por qué el rojo aparece al cumplir la promesa / Why the red appears when the promise is kept

Las dos ramas de detalle **viven en el árbol visual a la vez** aunque sólo una se vea — está medido
desde la primera tanda del paseo, cuando `LibraryBackAction` casó con dos controles a la vez. Al
enlazar el comando, el botón pregunta al adjuntarse, con `Surface` todavía en `Browse`, y la
respuesta es no. Con `CanExecuteChanged { add { } remove { } }` la suscripción se tira a la basura, así
que nadie vuelve a preguntar: el botón queda **visible y apagado para siempre**. / Both detail
branches sit in the visual tree at once, so the button is asked while the surface is still Browse and
the empty event throws the subscription away.

Ese es el rojo de arriba. No preexistía: aparece al cumplir la promesa del comando, y es exactamente
lo que prueba que la notificación hace falta. / It did not pre-exist: it appears when the command's
promise is kept, which is what proves the notification is needed.

## La corrección / The fix

```csharp
OnPropertyChanged(nameof(IsShowDetails));

// Both detail branches sit in the visual tree from the start, so the Back button
// is asked once — while the surface is still Browse — and the answer is no. Without
// this it renders enabled=False forever, which is what the walk measured.
_back.RaiseCanExecuteChanged();
```

El `RelayCommand` privado cambia su evento vacío por uno real, el campo es del tipo concreto —no un
`as` que pueda dejar de casar en silencio— y el disparo va en el **único** sitio donde `Surface` se
asigna. El manejador `OnBackClick` desaparece del code-behind. / The private command swaps its empty
event for a real one, the field is the concrete type rather than an `as` that could silently stop
matching, and the raise sits where `Surface` is assigned.

Y la prueba de unidad que faltaba, en `LibraryNavigationTests`, donde **la cuenta es la aserción**:
el predicado solo pasa tanto si alguien fue avisado como si no. / And the unit test that was missing,
where the count is the assertion: the predicate alone passes whether or not anyone was told.

## La nota pasa a puerta / The note becomes a gate

`CommandNotificationTests` lleva la **lista cerrada** de los siete archivos que quedan con el evento
vacío, y cada entrada carga **el predicado exacto** que hace segura su omisión: / The closed list of
the seven files that still silence the event, each carrying the exact predicate that makes it safe:

| Archivo / File | `CanExecute` |
|---|---|
| `RootOnboardingViewModel`, `ShortcutSettingsViewModel`, `LifecycleSettingsViewModel`, `WindowsTrayService` | `=> true` |
| `AppearanceSettingsViewModel` (×2) | `parameter is "es" or "en"`, `parameter is ThemePreference` |
| `ShellViewModel` | `parameter is AppRoute` |
| `DatabaseRecoveryViewModel` | `parameter is DatabaseRecoveryAction action && SafeActions.Contains(action)` |

Un `CanExecuteChanged` vacío sólo importa cuando `CanExecute` mira **estado que cambia**; con una
constante, o con un predicado que no lee más que su parámetro, no hay nada que anunciar. Por eso la
puerta no vigila el evento a solas: vigila **la pareja**. / An empty event only matters when
`CanExecute` reads state that changes, so the gate watches the pair rather than the event alone.

**La puerta se probó fallando**, que es la única forma de saber que no es ciega: / The gate was tested
by making it fail, which is the only way to know it is not blind:

```
# un predicado que cambia de forma / a predicate that changed shape
The predicates in src/ApSolutions.LocalMedia.Windows/Tray/WindowsTrayService.cs are no longer the
ones this list vouches for. Declared: ... => true;. Found: ... => parameter is not null;.

# un noveno que la lista no nombra / a ninth the list does not name
These declare a CanExecuteChanged that throws its subscriptions away, so a button bound to them
keeps the first answer forever: src/ApSolutions.LocalMedia.Windows/Tray/WindowsTrayService.cs.
```

La cuarta prueba es el suelo del propio análisis: cuenta lo que encuentra y comprueba que sigue
distinguiendo un evento silenciado de uno real —`LibraryViewModel` **no** puede aparecer—, para que un
reformateo que deje de casar no ponga la puerta en verde por silencio. / The fourth test is the scan's
own floor, so a reformat cannot turn the gate green by silence.

## Lo que queda dicho / What this leaves said

Ocho eran, siete quedan, y de esos siete ninguno tiene riesgo: se sale de la lista **ganando un evento
real**, nunca editándola. / Eight were, seven remain, and none of the seven carries risk: a file
leaves the list by gaining a real event, never by being edited out.
