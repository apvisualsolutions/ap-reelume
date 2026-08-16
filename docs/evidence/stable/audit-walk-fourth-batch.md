# La cuarta tanda: los ajustes, enteros / The fourth batch: settings, complete

Veinte controles en siete vistas, todos pulsados con el ratón. Es la primera tanda sobre una página
**más alta que la ventana**, y por eso costó dos hallazgos del arnés y un cambio de producción. / Twenty
controls across seven views, all pressed with the mouse.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El número / The number

| Medida / Measure | Antes / Before | Después / After |
|---|---|---|
| Pulsados con ratón / Pressed with a mouse | 33 | **53** |
| Pendientes / Pending | 95 | **75** |

El trinquete de `eng/check-walk-coverage.ps1` **va a cero**, y eso ya está escrito en el guion: esta
aplicación se publica gratis y nadie la va a probar a mano, así que un control que el paseo no pulsa
es un control que nadie pulsa antes de que alguien la instale. / The ratchet's destination is zero,
and the script says so.

## Lo que se pulsó / What was pressed

- **Apariencia (5)** — los tres temas y los dos idiomas.
- **Escaneo (1)** y **detección de segmentos (1)** — la segunda leída del caso de uso, no de la
  casilla: una casilla que sólo mueve su propio `bool` se ve igual.
- **Ciclo de vida (5)** — la bandeja, cerrar a ella, y el arranque con Windows: se pide, **se
  deniega**, se vuelve a pedir y **se concede**.
- **Privacidad (4)** — diagnóstico, refresco automático, previsualización y **exportar**, esta última
  leída del **archivo en el disco**.
- **Recomendaciones (3)** — el interruptor, el deslizador del umbral y Aplicar.
- **Atajos (1)** — restaurar los valores por defecto, después de reasignar uno: restaurar sin haber
  cambiado nada es indistinguible de no hacer nada.

## El cambio de producción que hizo falta / The production change it needed

Conceder el arranque con Windows **escribe en el registro**, y la aplicación ensamblada nombraba la
clave que Windows lee al iniciar sesión. Pulsar ese botón en una suite habría registrado el binario
recién compilado para arrancar en la máquina de quien la ejecutase — por eso las suites tenían
prohibido acercarse a esa clave, y por eso el control **no se podía cubrir**. / Granting writes to the
registry, and the assembled application named the key Windows reads at sign-in.

`IAppDataPaths` gana `StartupRegistrySubKey`, por la misma razón por la que ya nombra carpetas: es un
sitio donde la aplicación escribe en esta máquina. La regla la decide **la raíz de datos resuelta**, no
quién pregunta: / The rule is decided by the resolved data root, not by who is asking:

- La ejecución que es dueña del perfil —incluida la que mueve sus datos con la variable de entorno,
  porque sigue siendo quien inicia sesión aquí— escribe donde Windows lee.
- Cualquier otra —un arnés, el paseo, una comprobación de ciclo de vida— escribe bajo una clave con
  el nombre de su propia raíz, y el paseo la borra al terminar la escena.

La misma raíz escrita con otra caja o con barra final da **la misma clave**: una ejecución que se
reinicia tiene que encontrar su entrada en vez de dejar una segunda. / The same root spelt differently
gives the same key.

## Y dos reglas del arnés, medidas / And two harness rules, measured

**`Reveal`: se vuelve arriba y sólo se desplaza si el control no cabe.** Los ajustes miden 3680 px en
una ventana de 2000. El arnés empujaba el desplazamiento mientras buscaba y no lo devolvía, así que
tras tres pulsaciones el control siguiente —más arriba— quedaba en **y = -102**, fuera por arriba, y
bajar más lo alejaba. Ahora una pulsación no depende de cuál se hizo antes. / The page returns to the
top; a press no longer depends on which press came before it.

**Los botones de tema se pulsan los últimos.** Aplicar un tema reconstruye los recursos con los que se
dibuja la página, y después **ocho pulsaciones seguidas en el mismo punto no llegaron** a un control
situado por encima, con la página arriba del todo. Con los idiomas antes que los temas, la escena pasa
a la primera. / Themes are pressed last.

**Y el impacto no decide.** Contestó `under=True` y `under=False` en el **mismo punto y el mismo
desplazamiento** en dos momentos distintos, así que no puede autorizar ni prohibir un clic: sólo la
sonda del efecto decide. Por eso una pulsación que no cambia nada **se repite hasta ocho veces**, y la
queja nombra el punto y la cadena de controles bajo él. Un `ScrollViewer` desnudo en una ventana
desnuda se comporta bien, lo que descartó a Avalonia. / Hit testing decides nothing; only the effect
probe does.

## Las puertas / The gates

`dotnet format --verify-no-changes`, compilación con `-warnaserror`, accesibilidad (90),
integración (444), aplicación (223), arquitectura (26) y `eng/check-walk-coverage.ps1`. / All green.
