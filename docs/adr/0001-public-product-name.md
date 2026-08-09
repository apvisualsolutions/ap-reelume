# ADR-0001 — Nombre público de AP Reelume / AP Reelume Public Product Name

- Estado / Status: `ACCEPTED`
- Fecha / Date: 2026-08-01
- Decisor / Decision owner: Product Owner
- Relacionado / Related: [`FEATURES.md` — `REL-004`](../FEATURES.md), [especificación de diseño](../superpowers/specs/2026-08-01-local-media-library-design.md)

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be updated together.

---

## Español

### Contexto

La aplicación necesitaba un nombre público corto, inventado e internacional que combinara una sensación cinematográfica y elegante con una identidad tecnológica minimalista. También debía vincularse con AP Solutions sin convertir los identificadores técnicos internos en dependientes de la marca.

Se compararon, entre otras, las alternativas **AP Reelume** y **AP Reevora**. Reelume comunica mejor la idea de cine, luz y reanudación del contenido; Reevora resulta más abstracto y menos inmediato al escribirlo o recordarlo.

### Decisión

- Nombre de producto: **AP Reelume**.
- Presentación completa: **AP Reelume by AP Solutions**.
- Firma editorial: **by AP Solutions**.
- El nombre visible en la aplicación será **AP Reelume**; la firma aparecerá en superficies de marca apropiadas como Acerca de, instalador, ficha de Store y documentación pública.
- Los IDs de paquete, espacios de nombres, esquema de base de datos y demás identificadores persistentes se definirán de forma estable y no dependerán del nombre público.

### Consecuencias

- Toda documentación, interfaz, arte promocional y publicación nueva debe usar exactamente estas mayúsculas y esta redacción.
- No se utilizarán **Reelume** sin el prefijo **AP** como nombre oficial ni **AP Reevora** como alternativa paralela.
- La selección del nombre no equivale a una autorización jurídica. Antes de reservar o publicar la ficha de Microsoft Store se documentará una comprobación formal de marcas, nombres comerciales, dominios y colisiones en tiendas.
- La comprobación preliminar del 2026-08-01 no encontró coincidencias exactas en Microsoft Store o GitHub y observó `reelume.app` sin registro en RDAP. Estos indicios pueden cambiar y no sustituyen el informe final exigido por `REL-004`.

---

## English

### Context

The application needed a short, invented, international public name combining a cinematic, elegant feel with a minimalist technology identity. It also needed to connect to AP Solutions without making stable internal technical identifiers depend on branding.

The alternatives compared included **AP Reelume** and **AP Reevora**. Reelume communicates cinema, light, and content resumption more clearly; Reevora is more abstract and less immediate to spell or remember.

### Decision

- Product name: **AP Reelume**.
- Full presentation: **AP Reelume by AP Solutions**.
- Publisher signature: **by AP Solutions**.
- The visible application name is **AP Reelume**; the signature appears on appropriate brand surfaces such as About, installer, Store listing, and public documentation.
- Package IDs, namespaces, database schema, and other persistent identifiers will be defined for stability and will not depend on the public name.

### Consequences

- All new documentation, UI, promotional artwork, and releases must use this exact capitalization and wording.
- **Reelume** without the **AP** prefix is not the official name, and **AP Reevora** will not be used as a parallel alternative.
- Selecting the name does not constitute legal clearance. A formal trademark, trade-name, domain, and store-collision review must be documented before reserving or publishing the Microsoft Store listing.
- The preliminary 2026-08-01 check found no exact Microsoft Store or GitHub match and observed `reelume.app` as unregistered in RDAP. These signals can change and do not replace the final clearance report required by `REL-004`.
