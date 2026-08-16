# La bandeja de revisión, decidida con el ratón / The review inbox, decided with the mouse

Cuatro controles pulsados, **dos defectos del producto** y uno del arnés. El botón «Buscar» de la
bandeja no se podía ni pulsar, y si se hubiera podido, no habría hecho nada. / Four controls pressed,
two product defects and one harness defect: the inbox's Search button could not be pressed, and had it
been, nothing would have happened.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 55 | **59** |
| Pendientes / Pending | 73 | **69** |

## El primer defecto: un botón que nunca se habilita / A button that never enables

`ManualSearchAction is on screen but cannot be pressed: visible=True, enabled=False`

El comando de «Buscar» y el de limpiar la selección los daba una clase privada con
`CanExecuteChanged { add { } remove { } }`: **el evento no tenía cuerpo**, así que un botón preguntaba
una vez, al construirse, y no volvía a preguntar nunca. Escribir en la caja de búsqueda dejaba el
botón deshabilitado para siempre. `ARQ-004` sustituyó veinticuatro clases así por `AsyncRelayCommand`;
este par se quedó. / A private command class whose `CanExecuteChanged` had an empty add and remove: a
button asked once and never again.

## El segundo: un evento que nadie escucha / An event nobody listens to

`ManualSearchRequested` se declaraba, se disparaba y **ningún sitio de `src/` se suscribía**. Es el
defecto característico de la casa —registrado y nunca alimentado— vestido de evento en vez de
registro. La única suscripción del repositorio estaba **en una prueba**, que por eso pasaba en verde.
/ The event was declared, raised, and subscribed to nowhere in `src/` — the only subscriber was a test.

**Lo que hace ahora**: `SearchForMatch`, la contraparte manual de `IdentifyScannedFiles`. Las palabras
escritas se leen igual que se lee un nombre de archivo —el analizador separa título y año—, el
proveedor contesta, el puntuador ordena, y los candidatos sustituyen a los que el archivo tenía. Si la
respuesta no deja dudas se **aplica sin preguntar**, exactamente como hace el escaneo; si las deja, se
queda en la bandeja. Y el botón exige ahora las dos mitades —texto y candidato seleccionado—, porque
sin candidato no hay archivo sobre el que buscar. / What the button reaches now is the manual
counterpart of the scan's identification.

## El tercero, del arnés: el clic de control seleccionaba / The control click selected

La escena pasó en verde **aceptando un candidato que nadie había pulsado**. El clic «al lado» —el que
prueba que el botón es el que hace el trabajo— cae fuera de todo control de mando, y una tarjeta de
candidato no es un botón: aterrizaba sobre ella y **la seleccionaba**, así que Aceptar decidía
`movie:9000026` mientras el paseo había pulsado `movie:900001`. La sonda decía «hay un candidato
aceptado» y era cierto. / The beside click landed on a candidate card and selected it, so Accept
decided a different candidate than the one clicked — and "a candidate is accepted" was true either way.

Dos cambios: una fila seleccionable cuenta como ocupada, y la escena pregunta **cuál** quedó decidido,
no cuántos. / A selectable row counts as occupied, and the scene asks which candidate carries the
decision.

Y una regla más del arnés, medida aquí: **la lista virtualiza contra la ventana, no contra su propia
altura**. Con 26 candidatos en una ventana de 2000 px, las ocho primeras tarjetas estaban recicladas y
sólo existía de la novena en adelante; y revelar un control recicla lo que había materializado más
abajo, así que una tarjeta resuelta antes de mover la página no tiene posición cuando llega el clic
(«Border has no position in the window»). El paseo vuelve arriba **y luego** elige una tarjeta que
esté en pantalla, que es lo que hace una persona. / The list virtualises against the window; the walk
returns to the top and then picks a card it can see.

## Lo que la escena mide / What the scene measures

- **Cargar más**: 26 candidatos sembrados, 25 en la primera página, y el botón trae el que falta.
- **Aceptar** y **Rechazar**: la sonda es la **fila del catálogo**, no la tarjeta que desaparece, y
  pregunta cuál lleva la decisión. Después se comprueba que sale de la bandeja.
- **Buscar**: la respuesta sale de la caché del proveedor —sin token no hay conexión, que es el camino
  publicado—, y el efecto se lee en el catálogo: el título almacenado pasa a ser `La llegada`. Los
  candidatos equivocados desaparecen con ello.

## Las puertas / The gates

`dotnet format --verify-no-changes --severity warn`, compilación con `-warnaserror` (0 avisos),
aplicación (228), arquitectura (26), interfaz (439) y `eng/check-walk-coverage.ps1`: **129 controles
declarados en 128 identidades; 59 pulsados, 69 pendientes**. / All green.
