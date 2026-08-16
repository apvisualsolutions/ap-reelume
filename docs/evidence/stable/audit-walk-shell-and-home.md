# El shell y la pantalla de inicio, pulsados con el ratón / The shell and home, pressed with the mouse

Catorce controles, y **el botón principal de toda la aplicación no hacía nada**. «Continuar», en la
pantalla de inicio, estaba construido sin manejador. / Fourteen controls, and the primary action of
the whole application did nothing: Continue, on the home surface, was built with no handler at all.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 62 | **76** |
| Pendientes / Pending | 66 | **52** |

Es el mayor salto de una sola tanda desde que existe el trinquete. / The largest single-batch step
since the ratchet existed.

## El defecto grave: «Continuar» estaba cableado a nada / Continue was wired to nothing

```
onResume: null,
```

`HomeViewModel` acepta el manejador como opcional, y el contenedor le pasaba **nulo**. El resultado
es exactamente lo que parece: la pantalla de inicio ofrecía «Continuar», el botón **se habilitaba
solo** porque había progreso al que volver, y pulsarlo **no hacía nada en absoluto**. Es el defecto
característico de esta casa —registrado y nunca alimentado— en la primera pantalla que ve cualquiera,
y en la única acción que se pulsa sin mirar. / Home offered Continue, the button enabled itself
because there was progress to return to, and pressing it did nothing.

La medición, con el clic llegando al control y sin efecto ocho veces seguidas:

```
clicking Continue never opened the session it offers. 8 presses, the last at 1482, 120,
where a click reaches PART_ContentPresenter inside ResumeHeroAction inside Grid inside Border
```

**Corrección**: el manejador abre la sesión con la **versión de la que salió la posición** —
`watch_state` guarda ese archivo para exactamente esto — en la posición que la política de progreso
permite. El shell se lee **en el momento de pulsar** y no al construir, porque este modelo se
construye mientras el shell aún se está montando; es el mismo patrón que ya usaba «Reproducir» en la
ficha de película. / What it opens is the version the position was read from, at the position the
progress policy allows.

## El segundo: dos de los tres controles del reproductor estaban fuera de la pantalla / Two of the three player controls sat off-screen

```
Mini reproductor at 1737, 34 sized 242, 36 is surrounded by other command controls […]
```

**x = 1737 en una ventana de 1600**, y «Pantalla completa» más allá todavía. La causa es la misma que
en la reasignación de esta misma jornada —un `StackPanel` horizontal ofrece a sus hijos **ancho
infinito**— pero aquí es peor: la columna del reproductor mide **320 px por definición**
(`ColumnDefinitions="*,320"`), así que los tres botones, que suman unos 800, **nunca** cabían, a
ninguna anchura de ventana. Ni agrandando la pantalla se alcanzaban. / The player column is 320 px
wide by definition, so the three buttons never fitted at any window size.

**Corrección**: un `WrapPanel`, como el de la pantalla de copias. Tercera vez en el día que el mismo
contenedor esconde un control: **un `StackPanel` horizontal con contenido de anchura desconocida es
un control que se sale**. / A WrapPanel, like the backup actions. Third time in one day that this
container hid a control.

## Lo que la escena mide / What the scene measures

- **El carril de navegación entero**, tomado de `ShellViewModel.Routes` y no de una lista escrita en
  la prueba: un destino que se añada mañana lo pulsa esta escena el día que se añade. La ruta con la
  que abre el shell **va la última**, porque pulsar el destino en el que ya estás no tiene efecto que
  observar — y el paseo prueba un control por su efecto.
- **Las tres acciones de la ficha**: editar y renombrar sobre una ficha abierta, y «Ver duplicados»
  en la única escena que tiene un grupo de versiones de verdad. Pedir la previsualización **no
  renombra nada**, y eso se comprueba en el disco.
- **Los tres del reproductor**: exactamente un modo está en vigor a la vez, así que cada pulsación se
  lee del modo del shell. Cerrar es el único que **termina** la sesión, y después el shell no
  sostiene ningún reproductor y el modo vuelve a donde empieza el siguiente.
- **La pantalla de inicio**: el interruptor de recomendaciones se lee del **ajuste almacenado**
  —apagado, el carril se vacía en vez de esconder un resultado, y las dos cosas se ven igual—, y
  «Continuar» se lee de la sesión que abre.

## Una decisión sobre la lista de vigilados / One decision about the watched list

`CompositionRoot.Library.cs` quedó en **97,14 % de líneas y 100 % de ramas** —por encima del suelo de
96/96— y **no entra en la lista**. No es por comodidad: los seis vigilados son archivos que
**deciden** algo (la política de reconciliación, la identidad de un archivo, la versión que se
reproduce, dónde viven los datos, qué sale hacia el navegador, la bandeja de revisión). Un archivo de
composición no decide: declara. Su red propia es `ServiceConsumptionTests`, que exige un consumidor
para cada registro. / The composition file is above the floor and stays off the list, because the
watched list is for files that decide something.

Y conviene anotar **lo que esa red no caza**: un gancho opcional que el contenedor deja en su valor
por defecto no es un registro sin consumidor, así que ninguna prueba de arquitectura lo veía. Lo que
lo vigila desde hoy es **el paseo**: si «Continuar» vuelve a quedarse sin manejador, la escena falla.
/ An optional hook left at its default is not a registration without a consumer, so no architecture
test saw it. The walk watches it now.

## Las puertas / The gates

`dotnet format --verify-no-changes --severity warn`, compilación con `-warnaserror` (0 avisos), las
**diez suites** —2 082 pruebas, ninguna roja— y `eng/check-walk-coverage.ps1`: **129 controles
declarados en 128 identidades; 76 pulsados, 52 pendientes**. / All green.
