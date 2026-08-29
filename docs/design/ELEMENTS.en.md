# Element catalogue

What every control draws, in all of its states, and which token draws it.

This is not a proposal: it is a reading of
`design/Catálogo de elementos - AP Reelume.dc.html` — the catalogue the owner named as canonical —
carried over into the tokens of `Theme/DesignTokens.axaml`. Where the prototype and this document
disagree, the prototype wins and this document is wrong. Where this document and an `.axaml`
disagree, this document wins and the `.axaml` is wrong.

The prototype's numbers are CSS on a 1600 px page. They are copied as they stand except where this
tree already holds a measured decision against them, and those concessions are written at the end
with their reason.

## How to read an entry

Every element carries its states in the order the catalogue orders them — **rest · hover · pressed ·
focus · disabled** — and, when the control also makes a choice, **chosen** before the five. Beside
each state is the token that paints it, not the colour: a hex value written here would be right in
one of the four dictionaries and false in the other three.

Three rules hold for everything that follows and are not repeated in each entry:

- **Disabled carries a dotted border** in all four action variants. It is the signal that is not
  colour, and it separates "disabled" from "absent" without comparing two greys.
- **Focus is a double ring**, the same in all four themes: 1 px of the ground's colour and 3 px of
  the focus colour outside it. In high contrast light the border and the focus are the same black and
  the only thing separating them is the geometry, which is exactly why they are two rings and not one
  thick one.
- **No choice is said with colour alone.** Every control that chooses carries a second sign — a
  glyph, a bar, a mark — because in the two high contrast dictionaries the accent fill and the plain
  fill resolve to the same white or the same black.

## Action · the five states

Four variants, and the primary one appears **exactly once per screen**.

### Primary

38 tall in the prototype, `ControlHeight` (36) here, pill radius, semi-bold, `20,0` padding at the
sides.

| State | Fill | Ink | Border |
| --- | --- | --- | --- |
| Rest | `PrimaryActionBrush` | `PrimaryActionTextBrush` | same as the fill |
| Hover | `PrimaryActionHoverBrush` | `PrimaryActionTextBrush` | same as the fill |
| Pressed | `PrimaryActionPressedBrush` | `PrimaryActionTextBrush` | same as the fill |
| Focus | rest + double ring | `PrimaryActionTextBrush` | same as the fill |
| Disabled | `ControlFillDisabledBrush` | `TextDisabledBrush` | dotted |

### Secondary

The same height and the same radius, `16,0` padding, no fill of its own.

| State | Fill | Ink | Border |
| --- | --- | --- | --- |
| Rest | transparent | `TextPrimaryBrush` | `ButtonBorderBrush` |
| Hover | `ControlFillHoverBrush` | `ControlTextActiveBrush` | `ButtonBorderBrush` |
| Pressed | `ControlFillPressedBrush` | `ControlTextActiveBrush` | `ButtonBorderBrush`, 2 px |
| Focus | rest + double ring | `TextPrimaryBrush` | `ButtonBorderBrush` |
| Disabled | `ControlFillDisabledBrush` | `TextDisabledBrush` | dotted |

### Icon

Square, `ControlHeight` by `ControlHeight`, no inner padding, the glyph centred by its own geometry.
The pill radius over a square target draws it round: one number for the circle and for the pill.

Its five states are the secondary's. The one thing of its own is that it **carries no optical
compensation** — see "Vertical alignment" — because a shape has no baseline to answer for.

### Link

No box: only the word, in `AccentBrush`, semi-bold.

| State | Ink | Underline |
| --- | --- | --- |
| Rest | accent | no |
| Hover | lightened accent | yes |
| Pressed | darkened accent | yes |
| Focus | accent + double ring | no |
| Disabled | `TextDisabledBrush` | struck through |

## Selection · every control in its forms

Here is the distinction that gets it wrong most often: **a menu and a drop-down do not choose the
same way.**

### Option pill

32 tall in the prototype, `ControlHeight` here, pill radius, `0 15` padding, and **always** a state
glyph (`●` chosen, `○` not chosen) as well as the colour.

| State | Fill | Border | Ink | Weight |
| --- | --- | --- | --- | --- |
| Chosen | `AccentSubtleBrush` | `AccentBrush` | `AccentInkBrush` | semi-bold |
| Not chosen | `ControlFillBrush` | **transparent** | `TextSecondaryBrush` | medium |
| Hover | `ControlFillHoverBrush` | `ButtonBorderBrush` | `ControlTextActiveBrush` | medium |
| Focus | as not chosen + double ring | transparent | `TextSecondaryBrush` | medium |

What has been drawn wrong most often: **the unchosen pill carries no border and its text is not the
primary ink.** A permanent border and full ink make three options look like three chosen ones.

Where: the four themes, the language, the root kind, the library's type tabs, the marker kind, the
season picker of a series.

### Drop-down

32 tall in the prototype, `ControlHeight` here, pill radius, `0 13` padding. It carries the label on
the left at 72 % opacity in caption size, the value in semi-bold, and the chevron at the end.

| State | Fill | Border | Chevron |
| --- | --- | --- | --- |
| Closed | `ControlFillBrush` | `ComboBoxBorderBrush` | `IconChevronDown` |
| Open | `AccentSubtleBrush` | `AccentBrush` | `IconChevronUp` |
| Hover | `ControlFillHoverBrush` | `ComboBoxBorderBrush` | `IconChevronDown` |
| Focus | closed + double ring | `ComboBoxBorderBrush` | `IconChevronDown` |
| Disabled | `ControlFillDisabledBrush` | dotted | `TextDisabledBrush` |

The panel it opens: padding 4, `CornerRadiusMedium`, `CardSurfaceBrush` surface,
`ComboBoxDropDownBorderBrush` border, and its rows separated by 2.

Its rows, and here there **is** accent, which is what the prototype draws:

| Row state | Fill | Border | Weight |
| --- | --- | --- | --- |
| Chosen | `ComboBoxItemBackgroundSelected` | `AccentBrush`, **1 px** | semi-bold |
| Hover | `ControlFillHoverBrush` | transparent | normal |
| Rest | transparent | transparent | normal |

Where: the five settings and filter drop-downs, plus the speed one, which opens upwards.

**The speed one has three things of its own**, all of them the prototype's. Its rows are three
columns — a mark, a name and a note: `● Normal · 1×`, `2× · faster` — because the number alone does
not say which way it goes: `0.75×` and `1.25×` are the same distance from normal and read alike. Its
panel comes out **upward**, which is the only sensible direction on the player's bottom edge. And its
closed face says the multiplier where the row says the word for it: the pill reads `SPEED 1× ▲` and
the row reads `● Normal · 1×`.

The mark is not decoration beside the wash and the border: on both high contrasts the selected row's
fill and the resting fill are the same colour, so the mark is the only thing left saying which one is
in force — the same reason the appearance pills carry a state glyph and the rail carries a bar.

### Menu row

**This is not a drop-down and is not painted like one.** A menu says where you are, not which one you
picked off a list, and the prototype draws it with a neutral wash and **no accent border at all**.

34 tall in the prototype, `ControlHeight` here, `CornerRadiusSmall`, `8,0` padding, 11 between the
icon and the word.

| State | Fill | Border | Ink | Weight |
| --- | --- | --- | --- | --- |
| Current | `SelectionFillBrush` | `SelectionStrokeBrush` | `TextPrimaryBrush` | semi-bold |
| Rest | transparent | transparent | `TextSecondaryBrush` | normal |
| Hover | `ControlFillHoverBrush` | transparent | `ControlTextActiveBrush` | normal |
| Focus | whatever it is + double ring | — | — | — |

`SelectionFillBrush` is the prototype's `rgba(127,145,170,.16)`: a blue-grey at 16 %, the same in
light and in dark. `SelectionStrokeBrush` is **transparent** in light and dark, and the theme's one
colour in the two high contrasts, where the wash says nothing and the geometry has to say everything.

Where: the settings side index, the rows of the rail's fly-out menus, and any `ListBoxItem` that is
not a card.

### Navigation destination

46 × 42, radius 12, and **the accent is a bar, not a border**: 3 px wide to the left of the button,
with 11 px of air above and below, present or absent.

| State | Fill | Bar | Glyph ink |
| --- | --- | --- | --- |
| Current | `SelectionFillBrush` | `AccentBrush` | `TextPrimaryBrush` |
| Rest | transparent | absent | `TextSecondaryBrush` |
| Hover | `ControlFillHoverBrush` | absent | `ControlTextActiveBrush` |
| Focus | whatever it is + double ring | — | — |

The action at the foot of the rail — "Add media" — shares the size and is told apart by the one thing
the five destinations are denied: a hairline border. It is never "current", so it has neither wash
nor bar.

### Toggle

42 × 24, pill radius, an 18 px knob with 2 px of air, transition of `MotionDuration`.

| State | Track | Border | Knob |
| --- | --- | --- | --- |
| On | `AccentBrush` | `AccentBrush` | `AccentTextBrush`, to the right |
| Off | `ControlFillBrush` | `ComboBoxBorderBrush` | `TextSecondaryBrush`, to the left |
| Focus | whatever it is + double ring | — | — |
| Disabled | `ControlFillDisabledBrush` | dotted | `TextDisabledBrush` |

And **its state is written beside it**, not only in the knob's position.

### Selectable row

The row of a list where one thing is chosen among several — versions, markers, tracks, duplicates.
`11,9` padding, `CornerRadiusMedium`.

| State | Fill | Border |
| --- | --- | --- |
| Chosen | `AccentSubtleBrush` | lightened accent, 1 px |
| Rest | `ControlFillBrush` | `ShellHairlineBrush` |
| Hover | `ControlFillHoverBrush` | `ButtonBorderBrush` |
| Focus | rest + double ring | `ShellHairlineBrush` |
| Disabled | no fill | dotted, `TextDisabledBrush` ink |

## State, input and badges

### The five state tones

| Tone | Fill | Border | Sign | For |
| --- | --- | --- | --- | --- |
| Neutral | `ControlFillBrush` | `ShellHairlineBrush` | `○` | a process under way |
| Positive | `PositiveSurfaceBrush` | `PositiveBorderBrush` | `✓` | up to date, a desirable empty |
| Warning | `WarningSurfaceBrush` | `WarningBorderBrush` | `!` | the eight refusals |
| Error | `DangerSurfaceBrush` | `DangerBorderBrush` | `✕` | the seven failures |
| Absent | no fill | dotted | — | the control is not there |

### Fields

`ControlHeight` tall, `CornerRadiusMedium`, `11,0` padding, `TextControlBorderBrush` over
`ControlFillBrush`. **Paths are always monospaced.** A field in error changes its border to
`DangerBorderBrush` and nothing else; an empty field writes its placeholder in `TextDisabledBrush`.

### Badges

Pills of 11 px bold, `10,3` padding:

- **Available** — `PositiveSurfaceBrush` with `PositiveBorderBrush` ink.
- **Unavailable** — `WarningSurfaceBrush` with `WarningBorderBrush` ink, and the warning triangle.
- **Film** and **Series** — a neutral tint with the kind's glyph. On a rail cover only the glyph
  goes; the word only fits in the library grid.

And the three progress symbols, which are literals in the tree and stay: `○` not started, `◐` in
progress, `●` watched. The accessible name comes from its key, never from the symbol.

## Containers and typography

### List row

**A `1fr auto` grid, never a horizontal row.** A horizontal `StackPanel` offers infinite width to its
children, and that is what drew "Remove" at x = 2146 inside a 1600 px window. `13,11` padding,
`CornerRadiusMedium`.

### Card and hairline

Two tokens and not one: `ShellHairlineBrush` separates surfaces and `ButtonBorderBrush` bounds what
can be pressed. A card carries `CardSurfaceBrush`, a hairline, `CornerRadiusMedium` and
`ElevationShadow`.

### Overlaid panel

**Both dimensions bounded and the alignment explicit.** Bounding only the width left a dialogue
running off the top and the bottom; with no alignment, a panel measured 1280 × 1400.

### Typography

| Role | Size | Weight | Token |
| --- | --- | --- | --- |
| Display | 32 | 300 | `FontSizeDisplay` |
| Subtitle | 20 | 600 | `FontSizeSubtitle` |
| Body | 14 | 400 | `FontSizeBody` |
| Caption | 12 | 400 | `FontSizeCaption` |
| Overline | 10.5 · .18em | 400 | `hero-overline` |

Paths and codes go monospaced. Weight 300 is spent only on screen titles and on Home's hero.

## The icons

They all come from the same function of the prototype, `icon(n, s)`: an SVG of 24 × 24 with
`fill:none`, `stroke:currentColor`, `stroke-width:1.6` and round caps and joins. **They are not a
pictogram font**, and they are not mixed with one: a Segoe Fluent glyph is solid and comes from
another drawing tradition.

They live in `Theme/Icons.axaml` as geometries, converted rather than redrawn: the `path` elements go
verbatim and the prototype's `rect` and `circle` became the arcs that draw them. The thickness is not
one number — Avalonia scales the geometry to the control's bounds and then strokes it — so each size
carries its own: `1.6 × size ÷ 24`.

The sizes the prototype spends, and the only ones there are: 14 for a chevron, 16 for a menu row, 18
for a banner, 20 for a rail destination, 22 for the play toggle.

Two shapes are this application's own and say so: `IconStop`, because its transport has a stop where
the prototype has a single toggle, and `IconChevronUp`, which is `IconChevronDown` upside down.

## Vertical alignment

The problem that keeps coming back, and why it comes back. **This section was rewritten on
2026-08-29**, against the first measurement here that read pixels rather than boxes; what it used to
say is at the end, because the error matters more than the correction.

A typeface has no symmetric ascent and descent, so a label centred to its box draws its ink below the
middle. **On screen that error is 1 px**, measured by rasterising a real button with
`CaptureRenderedFrame()`.

The compensation is **1 px and it goes on the button's content** — the label when that is all there
is, the panel when there is an icon beside it — never on the label alone when it shares a row with an
icon:

- A margin on the label **grows the panel the label sits in**, and that panel is centred as a whole.
  So it moves the icon too, by half as much and the other way: on screen it left the icon at the
  button's centre and the word **2 px above** it.
- A margin on the content moves everything the button draws by the same amount, and so cannot
  separate two pieces that belong together. It is the only shape that keeps them aligned to each
  other **and** centred in the button.

`Path.icon` still carries no compensation: a shape is centred by its geometry, and a button whose
whole content is an icon needs none.

**What this section said until 2026-08-29, and why it was false.** It said the compensation was
**5 px**, derived from a **2.43 px** asymmetry computed from the font's metrics, and that a margin on
the `TextBlock` moved "only the word". All three were wrong:

- **2.43 px is the model's number, not the screen's.** Rasterising says 1.
- **5 px moved the word by 3**, from 1 px low to 2 px high: it overcorrected past the middle.
- **A margin on the label does not move only the word.** It moves the panel, and the icon with it.
  That premise is what left **53 buttons** with their icon and word 2 px apart.

And why nobody saw it: `ButtonInkTests` measured the box and `ButtonOpticalCentreTests` measured ink
**computed** from metrics. Neither rendered anything. The second is retired — its method assumed a
descender **always**, and over «Guardar el informe», which has none, it answered 2.43 px where
rasterising measures 0.0 — and `ButtonPixelCentreTests` replaces it, rasterising and carrying the
word as a **parameter**: the middle of the ink is a property of the string and not of the font,
ranging from +0.62 («Guardar el informe») to +3.82 («ppp») with the descender.

## The concessions, with their reason

What this tree draws differently from the prototype, and why.

- **Controls measure 36 and not 32 or 38.** 36 px is the smallest target WCAG 2.2 accepts at AA. A
  scale with three control heights is three chances for a row not to line up.
- **The pill radius is 999 and not half the height.** The drawing clamps to half the short side, so a
  square target comes out round and a wide one comes out a pill with a single number.
- **The focus border's accent moves one step away from the theme's accent** when the two coincide. It
  is written into the test rather than asserted downwards.
- **In the two high contrasts the fill says nothing** and the second sign says it: the pill's glyph,
  the rail's bar, the menu row's border.
