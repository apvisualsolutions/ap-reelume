# El canario no puede pedir un puerto que el sistema tiene reservado / The canary cannot ask for a port the system has reserved

Evidencia del rojo que apareció el **2026-08-14** durante la verificación de `ARQ-012`, en un archivo
que ese trabajo **no toca**. / Evidence for the red that appeared on **2026-08-14** during the
`ARQ-012` verification, in a file that work does not touch.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El rojo, y por qué mentía / The red, and why it lied

```
GetRecommendationsTests.Nothing_reaches_a_canary_server_while_recommendations_are_computed [FAIL]
  System.ObjectDisposedException : Cannot access a disposed object.
  Object name: 'System.Net.HttpListener'.
     at System.Net.HttpListener.get_Prefixes()
     at …CanaryServer..ctor()
```

Un objeto desechado en su propio constructor no dice nada de lo que estaba mal. La causa de ese
síntoma es que **un `HttpListener` cuyo `Start` lanza se cierra a sí mismo**, así que el bucle de
reintento —que reutilizaba el mismo objeto— moría en la segunda vuelta con un error que hablaba del
arnés y no del sistema. / A disposed object in its own constructor says nothing about what went
wrong: an `HttpListener` whose `Start` throws closes itself, so a retry loop that reused it died on
the second lap with an error about the harness rather than about the host.

Corregido eso, el error pasó a ser honesto —«ningún puerto del rango estaba libre»— y esa frase ya se
puede medir. / Fixed, the error became honest, and an honest error can be measured.

## La medición / The measurement

```
> netsh int ipv4 show excludedportrange protocol=tcp
50996       51095
```

El rango fijo de la prueba era **51000-51049**, contenido **entero** dentro de la exclusión que
Windows mantiene para su pila de virtualización. Un puerto reservado rechaza al oyente con el mismo
error que uno ocupado, así que los cincuenta fallaban por la misma razón y ninguna era «el puerto
está ocupado». No es intermitente: mientras esa reserva exista, esa prueba **no puede** pasar en esta
máquina. / The test's fixed range sat entirely inside the range Windows reserves for its
virtualisation stack. A reserved port refuses a listener with the same error a busy one gives, so all
fifty failed for one reason — and it is not intermittent: while the reservation exists, that test
cannot pass on this machine.

Que pasara ayer y no hoy no lo convierte en una carrera: las exclusiones se asignan al arrancar el
anfitrión, así que lo que cambió fue el anfitrión, no el código. / It passing yesterday and not today
does not make it a race: the exclusions are assigned when the host boots.

## La corrección / The fix

Se le pregunta al sistema en vez de adivinar: un `TcpListener` en el puerto **cero** devuelve uno que
el sistema declara libre —y por construcción nunca uno reservado—, se cierra, y ese número es el que
recibe el `HttpListener`, con ocho intentos por si alguien lo toma en el hueco entre la pregunta y la
respuesta. Es el idioma que **ya usa `FakeReleaseServer`** en este mismo repositorio: el arnés que lo
hacía bien estaba a dos carpetas. / The system is asked instead of guessed: a `TcpListener` on port
zero returns a port the system declares free — and never a reserved one — with eight attempts for the
gap between question and answer. It is the idiom `FakeReleaseServer` already uses here.

## Lo que deja escrito / What this leaves written down

- **Un rango de puertos fijo es una apuesta contra el anfitrión**, y la resuelve con qué arrancó la
  máquina. En un runner hospedado se pierde igual.
- **Un error que habla del arnés esconde el del sistema.** Aquí la primera corrección no arregló la
  prueba: la hizo decir la verdad, y la verdad se pudo medir en un comando.
  / A harness error hides the host's; the first fix here did not make the test pass, it made it
  honest — and honest could be measured.

## Verde / Green

| Puerta / Gate | Resultado / Result |
|---|---|
| `ApSolutions.LocalMedia.Application.Tests` | 204 de 204 / of 204 |
| `eng/verify.ps1` completo / full | verde / green |
