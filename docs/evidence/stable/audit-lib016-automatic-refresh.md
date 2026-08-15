# El refresco que sólo se ofrece cuando puede / The refresh that is only offered when it can happen

`LIB-016`. El refresco automático de las fichas más antiguas, apagado por defecto, subordinado a la
conexión consentida y contenido por un tope. / The automatic refresh of the oldest entries, off by
default, subordinate to the consented connection and held by a cap.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## La primera medición cambió lo que había que creerse / What the first measurement changed

Estaba decidido —y no se re-delibera— que un `refreshed_utc` nulo cuenta como rancio y va **primero**
en el orden. Lo que la medición añade es que **hoy no tiene sujeto**. Ejecutando los caminos de
producción sobre una base real: / Running the production paths against a real database:

```
MEASURED rows=2 identified=1 identifiedWithNoDate=0 noDate=1
MEASURED afterEdit provider=tmdb key=movie/603 refreshed=2026-01-27T07:55:53Z
```

- `ApplyIdentification` y `RefreshMetadata` son los **únicos** que escriben `provider_key`, y ambos
  escriben también el momento. El editor **conserva** los tres campos al guardar encima.
- La única fila sin fecha es la que el editor crea para un título que **nadie identificó**, y ésa no
  es candidata: sin `provider_key` no hay ficha del proveedor que consultar.

Así que el orden con los nulos delante es **la guarda para una fila que ninguna ruta actual
escribe**, no un caso del campo. Se dice aquí con el número para que la próxima lectura no lo tome
por una necesidad medida. / The nulls-first order is the guard for a row nothing currently writes.

## Lo que se decide, y dónde / What is decided, and where

| Decisión / Decision | Dónde vive / Where it lives |
| --- | --- |
| Rancio a los 90 días; un nulo es lo más rancio que hay | `MetadataRefreshPolicy` (dominio) |
| 20 fichas por pasada | `MetadataRefreshPolicy.MaximumPerPass` |
| Cuáles, en qué orden y cuántas | Una sentencia SQL, medida contra SQLite y no contra un doble |
| Apagado por defecto | `StoredAutoRefreshSettings`, la ausencia del ajuste significa no |
| Sin conexión consentida no hay interruptor | `PrivacySettingsViewModel.CanRefreshAutomatically` |

**El texto del propósito de red cambió con el código**, no después:
`NetworkPurposeRegistry` declara ahora que TMDB recibe también «las fichas guardadas más antiguas,
como mucho veinte por arranque, y sólo mientras el refresco automático esté encendido». / The declared
purpose changed with the code.

**Los 90 días quedan por debajo del techo de 180 de retención de la caché**, y hay una prueba de la
desigualdad porque los dos números viven en capas distintas y nada más notaría que se cruzan. / A test
holds the inequality.

## La aceptación, medida y no leída / Acceptance, measured rather than read

- **Apagado: cero conexiones.** No es lectura del código: el canario de red ejecuta la pasada en un
  proceso hijo con el interruptor apagado y cuenta **0** peticiones; después, en el **mismo** hijo,
  lo enciende y cuenta **2**, una por ficha rancia. Un cero que nunca ha llegado a uno no dice nada.
- **Encendido: sólo las rancias, y nunca más de veinte.** Con 32 fichas guardadas —30 pasadas de los
  90 días, una fresca y una sin identificar— la consulta devuelve **20**, empezando por la que no
  tiene fecha y siguiendo de más antigua a menos. La fresca y la no identificada no aparecen.
- **El interruptor no existe sin consentimiento.** No deshabilitado: ausente. El consentimiento es el
  mismo que ya usa la búsqueda de candidatos — el token puesto, que es el acto deliberado y
  revocable.
- **Cede el paso.** Un vídeo abierto o un escaneo en marcha detienen la pasada, y se pregunta **antes
  de cada ficha**, no una vez al principio: una pasada dura más que el instante en que empezó.
  `ScanCoordinator` cuenta ahora sus escaneos en vuelo, y es esa misma instancia la que responde.

## Lo que queda verde / What is green

| Suite | Resultado |
| --- | --- |
| `Domain.Tests` (política) | 8 / 8 |
| `Application.Tests` (la pasada) | 6 / 6 |
| `IntegrationTests` (la consulta) | 4 / 4 |
| `IntegrationTests` (canario de red) | apagado 0, encendido 2 |
| `UiTests` (privacidad) | 19 / 19 |
