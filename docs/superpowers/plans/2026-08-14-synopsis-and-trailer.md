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
- [ ] **LIB-014 — el tráiler local se reproduce dentro.**
      - **Forma**: una política de dominio pura —`TrailerDiscoveryPolicy`— que, dadas la ruta de la
        película y los nombres hermanos, devuelve el tráiler si sigue la convención
        (`<nombre>-trailer.<ext>` o `Trailers/<algo>.<ext>`) y su extensión está en la lista aprobada
        de `MediaFileExtensions`. Nada de rutas fuera de la carpeta de la película.
      - **Primera medición**: qué convención usan de verdad las bibliotecas —Plex, Jellyfin y Kodi
        documentan las suyas— y cuáles de esos nombres pasan hoy la lista de extensiones.
      - **Aceptación**: la política se prueba con nombres, no con disco; el botón sólo existe cuando
        hay tráiler; la sesión de reproducción es la única que ya existe, sin segunda instancia.
- [ ] **LIB-015 — el tráiler remoto abre el navegador.**
      - **Forma**: TMDB ya devuelve los vídeos con `append_to_response=videos`; se guarda **la clave**
        (no una URL construida a mano) y el botón la abre con el lanzador externo endurecido, que es
        el único sitio de este repositorio con `Process.Start`.
      - **Coste real, que decide si entra**: campo nuevo en `MetadataDetails`, **migración** de la
        base, y un cambio en la petición a TMDB. No hay host nuevo: la clave viaja en la respuesta
        que ya se pide.
      - **Aceptación**: lo que se abre es la dirección oficial del reproductor de YouTube y nada
        construido con datos del proveedor sin validar; una prueba fija esa forma.
- [ ] **LIB-016 — el refresco automático, y sólo si lo enciendes.**
      - **Forma**: ajuste **apagado por defecto**; una política pura decide cuándo una ficha está
        rancia (contra el techo de 180 días que ya existe, nunca por encima); el trabajo respeta el
        consentimiento de red que ya hay y **el texto del propósito declarado cambia**, porque hoy
        dice «explicitly asked» y dejaría de ser verdad.
      - **Primera medición**: cuántas peticiones costaría una biblioteca real al encenderlo, contra
        el limitador de TMDB que ya existe.
      - **Aceptación**: con el ajuste apagado, **cero** conexiones —lo comprueba la prueba de canario
        de red que ya existe—; con él encendido, sólo las fichas rancias.
