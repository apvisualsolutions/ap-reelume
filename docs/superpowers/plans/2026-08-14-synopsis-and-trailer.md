# Sinopsis y tráiler / Synopsis and trailer

Bloque nuevo, pedido por el propietario el 2026-08-14: «las películas y series deben tener sinopsis
y, a ser posible, tráiler; debe ser auto-actualizable». Se ejecuta **después** de la cola de la
auditoría, que quedó cerrada ese mismo día, y **antes** de la documentación, porque el manual se
escribe desde la aplicación construida. / A new block, asked for on 2026-08-14; it runs before the
documentation block because the manual is written from the built application.

## Lo que ya estaba, medido antes de decidir nada / What was already there, measured first

- **La sinopsis existe de punta a punta y no se ve.** `MetadataDetails.Overview` (puerto de dominio),
  columna `overview` en SQLite, el proveedor TMDB la lee, `MetadataMergePolicy` la fusiona con su
  campo bloqueable, y el editor la muestra con cadenas ES/EN (`MetadataOverviewLabel`). Lo que falta
  es el **camino de lectura**: `CatalogItem` —lo que las vistas de detalle reciben— no la lleva, y
  ninguna de las dos vistas la pinta. / The synopsis exists end to end and is not shown: what is
  missing is the read path.
- **El tráiler no existe en ninguna parte.** Cero apariciones en `src/`, `tests/` y `docs/`.
- **Nada se refresca solo.** `RefreshMetadata` se invoca **únicamente** desde el editor de metadatos,
  y el propósito de red declarado para TMDB dice, con esas palabras, «the metadata a person
  explicitly asked to identify or refresh». La caché tiene un techo duro de retención de 180 días que
  los términos de TMDB imponen. / Nothing refreshes on its own, and the declared network purpose says
  "explicitly asked".

## La decisión que ordena el bloque / The decision that shapes the block

El tráiler **dentro de la aplicación** sólo cuando el tráiler es un **archivo local** junto a la
película, al modo de Plex y Jellyfin. Reproducir el de TMDB dentro incumpliría los términos de
YouTube —que sólo amparan su reproductor o su incrustación oficial—, y la incrustación oficial pediría
un WebView con hosts que `NetworkPurposeRegistry` no declara, publicidad y cookies en una aplicación
que promete no tener telemetría, y una dependencia nativa enorme. Cuando sólo hay clave de YouTube,
se abre el **navegador**, que es el uso que sus términos sí amparan. / In-app trailers only for a
local file; a YouTube key opens the browser, which is the use their terms allow.

## Las cuatro partes, en orden / The four parts, in order

- [x] **LIB-013 — la sinopsis se lee en las fichas.** Sin conexión nueva: el dato ya está guardado.
      **Hecho el 2026-08-14**, con la forma decidida: el cargador lee y las fichas reciben,
      `CatalogItem` intacto. Evidencia en
      [audit-lib013-synopsis.md](../../evidence/stable/audit-lib013-synopsis.md).
      La medición de «cuántos títulos tienen resumen» no llegó a hacer falta: las pruebas construyen
      la metadata, así que la vista se ejerce con y sin resumen sin depender de ninguna biblioteca.
      - **Forma**: el cargador de detalles de `CompositionRoot` pide la metadata del título a
        `ICatalogMetadataRepository` —que ya resuelve ahí— y se la pasa a `Apply`; las dos vistas de
        detalle ganan un bloque de texto con nombre accesible, reutilizando la cadena bilingüe que ya
        existe. `CatalogItem` **no** se toca: es la proyección de la consulta del catálogo y cargar
        la sinopsis en cada fila de la biblioteca sería pagar por lo que sólo la ficha muestra.
      - **Primera medición**: cuántos títulos de una biblioteca identificada tienen `overview` no
        vacío. Si es cero, la vista se prueba con una metadata construida, no con la biblioteca.
      - **Aceptación**: prueba de modelo de vista (hay sinopsis / no hay), prueba de que el marcado
        la enlaza y la anuncia, y prueba de cableado de que el cargador la lee. Ninguna conexión
        nueva: `NetworkPurposeRegistry` no cambia.
- [x] **LIB-014 — el tráiler local se reproduce dentro.** **Hecho el 2026-08-14.** La medición evitó
      inventar un camino: `OpenLooseFile` ya valida la extensión aprobada, comprueba que el archivo
      está y da un identificador de sesión que nunca es fila de catálogo, así que la forma quedó en
      una política pura que **nombra** el candidato. Sólo películas: una serie no tiene un archivo
      único al lado del que colgarlo. Evidencia en
      [audit-lib014-local-trailer.md](../../evidence/stable/audit-lib014-local-trailer.md).
      - **Forma**: una política de dominio pura —`TrailerDiscoveryPolicy`— que, dadas la ruta de la
        película y los nombres hermanos, devuelve el tráiler si sigue la convención
        (`<nombre>-trailer.<ext>` o `Trailers/<algo>.<ext>`) y su extensión está en la lista aprobada
        de `MediaFileExtensions`. Nada de rutas fuera de la carpeta de la película.
      - **Primera medición**: qué convención usan de verdad las bibliotecas —Plex, Jellyfin y Kodi
        documentan las suyas— y cuáles de esos nombres pasan hoy la lista de extensiones.
      - **Aceptación**: la política se prueba con nombres, no con disco; el botón sólo existe cuando
        hay tráiler; la sesión de reproducción es la única que ya existe, sin segunda instancia.
- [x] **LIB-015 — el tráiler remoto abre el navegador. Hecho el 2026-08-14**, en el orden que fija
      esta entrada. Tres mediciones cambiaron el trabajo: el lanzador endurecido que se iba a
      reutilizar **no existía** —ninguno de los tres `Process.Start` del árbol abre una dirección—;
      la clave de la caché **no incluye la dirección**, así que `append_to_response` habría servido
      el payload anterior como si fuera la respuesta nueva, y subir `ProviderVersion` habría dejado
      filas que nada volvería a leer y que por tanto nada podría borrar nunca —el techo de 180 días
      se aplica al leer esa misma clave—, de modo que la migración vacía lo de TMDB; y la política
      sin validar aceptaba **quince** formas, entre ellas un `javascript:` y un `https://` enteros.
      Evidencia en
      [audit-lib015-provider-trailer.md](../../evidence/stable/audit-lib015-provider-trailer.md).
      - **Qué se guarda**: la **clave**, nunca una URL. Campo `TrailerKey` en `MetadataDetails`,
        columna `trailer_key` en `catalog_metadata`, migración **`0017_trailer_key.sql`** con su
        entrada en `Manifest.json` y su SHA-256 — el manifiesto se recalcula, no se escribe a mano, o
        el arranque rehúsa antes de escribir.
      - **No es campo bloqueable en esta pasada**, y se dice por qué: `MetadataField` es la lista de
        lo que una persona puede editar y proteger, y no hay superficie que edite una clave de vídeo.
        La fusión es la de un dato del proveedor: lo remoto gana salvo que venga vacío. El día que
        exista un editor para ella, pasa a bloqueable **y a la lista de `MetadataMergePolicy`**.
      - **Qué se pide a TMDB**: `append_to_response=videos` sobre la petición de detalles que **ya se
        hace** — ni una llamada más, ni un host nuevo. De la lista se elige, en este orden:
        `type=Trailer` + `site=YouTube` + `official=true` en el idioma de la interfaz; luego el mismo
        sin `official`; luego cualquiera con `type=Trailer` y `site=YouTube`. Si no hay ninguno, no
        hay clave y no hay botón.
      - **La decisión de seguridad, que es la que importa**: una política de dominio
        —`TrailerLinkPolicy`— valida la clave contra `^[A-Za-z0-9_-]{11}$` **antes** de construir
        nada, y compone `https://www.youtube.com/watch?v=<clave>`. Una cadena del proveedor no puede
        construir una dirección arbitraria, y el lanzador externo endurecido —único sitio con
        `Process.Start`— sigue exigiendo `https`. Dos capas, porque el dato viene de fuera.
      - **La aplicación no abre ninguna conexión a YouTube**: la abre el navegador de quien pulsa. Por
        eso `NetworkPurposeRegistry` **no cambia**, y conviene decirlo en la evidencia para que nadie
        lo "arregle" añadiendo un propósito que no existe.
      - **Aceptación**: la política rechaza clave vacía, con longitud distinta de 11, con caracteres
        fuera del alfabeto y con cualquier cosa que parezca una URL; el botón sólo existe con clave;
        y una prueba fija la dirección exacta que se abre.
      - **Orden dentro de la entrada**: primero la migración con su prueba de esquema, después el
        proveedor, después la política, y la interfaz al final. Así ningún commit deja la base por
        delante del código que la lee.
- [ ] **LIB-016 — el refresco automático, y sólo si lo enciendes. Decidido entero el 2026-08-14.**
      ~~BLOQUEADO por lo que destapó su primera medición~~ **DESBLOQUEADO el 2026-08-15**: la cadena
      que faltaba está construida y `catalog_metadata` ya guarda `provider`, `provider_key` y
      `refreshed_utc`, que son exactamente los dos datos por título que esta entrada necesitaba y no
      existían. Ver [audit-apply-identification.md](../../evidence/stable/audit-apply-identification.md)
      y [audit-refresh-resolves-itself.md](../../evidence/stable/audit-refresh-resolves-itself.md).
      - **Decidido el 2026-08-15: un `refreshed_utc` nulo cuenta como rancio.** Una ficha sin fecha
        es una que nunca se refrescó, así que es la más rancia que hay, y ordenarla al final sería
        dejar fuera precisamente a las que más lo necesitan. Toda biblioteca identificada antes de
        esta versión entra por ahí, y quien contiene esa primera pasada es el tope de 20 por pasada,
        no el umbral. `ORDER BY refreshed_utc IS NOT NULL, refreshed_utc` —nulos primero— y una
        prueba que lo fije, porque el orden **es** la política.
      - **Apagado por defecto**, en Ajustes → Privacidad, junto al consentimiento de red que ya
        existe y **subordinado a él**: si no hay consentimiento, el interruptor ni siquiera se ofrece.
        Cadena bilingüe nueva, como todo lo visible.
      - **Qué es «rancio»: 90 días.** Ni 30 —una sinopsis no cambia cada mes y sería tráfico por
        deporte— ni 180, que es el **techo duro de retención** que los términos de TMDB imponen a la
        caché: el umbral tiene que quedar **por debajo** del techo para que el refresco ocurra antes
        de que el dato caduque, no después. Constante en el dominio, junto al techo, con una prueba
        que afirme la desigualdad `rancio < techo` — porque el día que alguien toque uno de los dos
        números, el otro tiene que enterarse.
      - **Cuánto se refresca de una vez: 20 fichas por pasada**, las más rancias primero, y sólo
        títulos ya identificados. El limitador de TMDB ya existe y se respeta; el tope está para que
        una biblioteca de miles no se convierta en una ráfaga la primera vez que se enciende.
      - **Cuándo**: al arrancar, después de que la ventana esté pintada, en el trabajo de fondo que
        ya cede el paso a la reproducción. Nunca durante un escaneo ni con una sesión de vídeo
        abierta.
      - **El texto del propósito declarado cambia con el código, no después.** Hoy
        `NetworkPurposeRegistry` dice «Fetches the metadata a person explicitly asked to identify or
        refresh», y con esto deja de ser verdad. Pasa a nombrar las dos formas —lo que se pide a mano
        y el refresco automático que alguien encendió—, y `NetworkPurposeDocumentationTests` obliga a
        que el documento y el registro digan lo mismo.
      - **Primera medición, antes de escribir el trabajo de fondo**: cuántas fichas de una biblioteca
        identificada tendrían más de 90 días, y cuántas peticiones son con el tope de 20 por pasada.
        Ese número va a la evidencia antes de encender nada.
      - **Aceptación**: con el ajuste apagado, **cero** conexiones —lo demuestra el canario de red que
        ya existe, no una lectura del código—; con él encendido, sólo las rancias y nunca más de 20;
        y una prueba de que el interruptor no aparece sin consentimiento de red.
