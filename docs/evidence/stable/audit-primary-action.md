# La acción principal, que era una clase sin estilo / The primary action, a class with no style

`primary-action` estaba puesta en el botón de `ResumeHeroView` —«continuar viendo»— y **no la definía
ningún estilo ni la buscaba ninguna prueba**. El botón que es el sentido de esa pantalla se pintaba
exactamente igual que cualquier botón secundario a su lado. El defecto de la casa con cara de atributo
`Classes`. / `primary-action` was on `ResumeHeroView`'s button and **no style declared it and no test
looked for it**. The button that is the point of that screen was painted exactly like every secondary
button beside it. The house defect wearing a class attribute's face.

Fecha / Date: 2026-08-19. Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El rojo / The red

```
The_primary_action_leads_at_rest_and_answers_like_the_rest
  Assert.Equal() Failure: Values differ        × 4 temas / themes
```

Un solo uso en todo el árbol, y cero pruebas nombrándola: exactamente la forma de «registrado y nunca
alimentado» que este repositorio ya persigue en el contenedor. / One use in the whole tree and zero
tests naming it.

## La decisión, y por qué invierte como todo lo demás / The decision

En reposo: fondo `AccentBrush`, texto `AccentTextBrush`, borde `AccentBrush`. Al pasar el ratón y al
pulsar: **invierte igual que cualquier otro control** (`ControlFillHoverBrush` /
`ControlFillPressedBrush` con `ControlTextActiveBrush`). / At rest the accent; on hover and press it
inverts like every other control.

**La jerarquía la lleva entera el reposo**, que es cuando una persona mira una pantalla y decide. Una
sola gramática de estados en toda la aplicación vale más que una cuarta manera de decir «pulsado» — y
en los dos temas de alto contraste un acento que se quedara quieto al pasar el ratón sería **el único
control que deja de contestar**. / The hierarchy is carried entirely by the resting state, which is
when a person looks and decides.

## El mecanismo, medido y no supuesto / The mechanism, measured rather than assumed

El estilo pone `Background`, `BorderBrush` y `Foreground` **en el `Button`**, y eso alcanza sólo al
reposo. No es suerte: el `ControlTheme` fija el relleno del `ContentPresenter` **por pseudoclase** y
deja el de reposo al `TemplateBinding`, así que un setter sobre el botón llega al reposo y **sólo** al
reposo. / The control theme sets the presenter's fill per pseudo-class and leaves the resting one to
the template binding, so a setter on the button reaches rest and only rest.

**Es el mismo mecanismo que en la fase 2f fue un defecto**, y aquí es el diseño. Por eso está
**afirmado y no dado por hecho**: la prueba comprueba los cinco estados, incluido el deshabilitado,
que ninguna otra reclamaba. / The same mechanism phase 2f met as a defect is the design here, so it is
asserted rather than assumed — all five states, disabled included.

## Y lidera, con un número / And it leads, as a number

No basta con que sea distinto: tiene que **separarse del botón ordinario**. La prueba monta los dos y
mide uno contra otro, con listón de 3, en los cuatro temas. Sin eso, «es el acento» sería una promesa
sobre un color y no sobre lo que se ve. / It is not enough to be different: the test builds both and
measures one against the other, at a bar of 3, in all four themes.

## El verde / The green

```
The_primary_action_leads_at_rest_and_answers_like_the_rest   4/4
ApSolutions.LocalMedia.UiTests                             593/593
ApSolutions.LocalMedia.AccessibilityTests                  133/133
dotnet build -c Release -warnaserror                         0 advertencias / 0 warnings
dotnet format --verify-no-changes                            limpio / clean
```
