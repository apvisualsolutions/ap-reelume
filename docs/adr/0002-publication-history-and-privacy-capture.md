# ADR-0002 — Historial de publicación y captura de privacidad / Publication History and Privacy Capture

- Estado / Status: `ACCEPTED`
- Fecha / Date: 2026-08-03
- Decisor / Decision owner: Engineering, delegado por el Product Owner / delegated by the Product Owner
- Relacionado / Related: [`FEATURES.md` — `PRI-001`, `PRI-002`](../FEATURES.md),
  [C6](../evidence/mvp/C6-experience-gate.md), Tarea 38 y Tarea 41 del
  [plan](../superpowers/plans/2026-08-01-ap-reelume-windows-mvp-implementation.md)

Este ADR contiene primero la decisión en español y después su traducción inglesa. Ambas partes deben actualizarse juntas.

This ADR contains the Spanish decision first and its English translation second. Both parts must be updated together.

---

## Español

### Contexto

Dos preguntas quedaron abiertas al cerrar I5 y ninguna se resuelve por intuición.

**Primera: el historial de Git.** El árbol de trabajo quedó saneado en `2ca2169` —fuera el recuento
de archivos, el volumen en bytes, dos términos de búsqueda reales y el título de una serie de la
biblioteca personal—, pero el historial anterior sigue conteniéndolos. Medido:

| Medida | Valor |
|---|---:|
| Commits en la rama | 46 |
| Commits que contienen el volumen en bytes | 2 |
| Commits que contienen el título real | 3 |
| Commits que contienen un término de búsqueda real | 2 |
| SHA distintos citados en la documentación | 28 |
| Archivos de documentación que citan un SHA | 26 |

Los commits afectados son de I1 e I2, es decir, casi al principio. Reescribir el historial cambiaría
el SHA de prácticamente los 46 commits y de `main`.

**Segunda: cómo capturar el tráfico en T38.** El plan pide «recorrido 30 min con proxy de captura» y
«escanear payload/logs/PCAP». Un proxy que descifre TLS exige instalar un certificado raíz en el
almacén del usuario, que es una modificación de la configuración de seguridad del equipo.
`pktmon` viene con Windows pero exige elevación, y este entorno no la tiene.

### Decisión

**1. El historial de Git no se reescribe.** El repositorio público se creará como repositorio nuevo
a partir del árbol saneado, no como copia del historial privado. El privado conserva su historial
íntegro y sus 28 referencias de commit siguen siendo verificables en él. El mecanismo exacto de
publicación se ejecuta en T41.

**2. La captura de privacidad de T38 no usa un proxy que descifre TLS.** Se compone de cuatro
piezas, todas sin elevación, sin instalar nada y sin tocar el almacén de certificados:

- Un `EventListener` sobre los `EventSource` de `System.Net.Http`, `System.Net.Sockets` y
  `System.Net.NameResolution` **dentro del proceso**, que registra cada petición con su URI, cada
  conexión y cada resolución de nombre **antes del cifrado**.
- Un servidor señuelo local que cuenta solicitudes, como en T32.
- Muestreo continuo de conexiones TCP por proceso, como en C6.
- Escaneo directo del archivo de diagnóstico y de los registros, que son locales y no viajan por la
  red en el MVP.

### Consecuencias

- **Las 28 referencias de commit de la documentación son registro interno.** En el repositorio
  público apuntarán a commits que allí no existen. T41 debe añadir una nota que lo diga, o
  sustituirlas por referencias a las evidencias, que sí viajan.
- **Se pierde el historial de desarrollo en público.** Es aceptable: el proceso —RED, GREEN,
  cobertura, puertas— está documentado en `docs/evidence/`, que sí se publica, y no en los mensajes
  de commit.
- **No se fuerza ningún push ni se toca `main`.** Reescribir habría exigido ambas cosas sobre una
  rama ya publicada.
- **La captura de T38 observa el URI pero no el cuerpo cifrado.** No es una limitación real para lo
  que hay que demostrar: el único tráfico legítimo es TMDB, y lo que se envía se demuestra en el
  código —DTO cerrado, sin serialización reflexiva de entidades, registro de propósito por cada
  `HttpClient`— y en el escaneo del payload local. Un proxy MITM habría añadido el cuerpo descifrado
  a cambio de instalar un certificado raíz, que es exactamente el tipo de cambio que una tarea de
  privacidad no debería introducir.
- **El `EventListener` cubre lo que hace el proceso .NET, no lo que hace el sistema.** Es el alcance
  correcto de la afirmación —«la aplicación no produce tráfico no solicitado»— y debe declararse así
  en la evidencia de T38 en lugar de presentarse como una captura de red completa.
- Si en el futuro se quiere una captura a nivel de paquete, `pktmon` está disponible en el sistema y
  sólo requiere una sesión elevada; no hace falta instalar nada.

---

## English

### Context

Two questions were left open at the end of I5, and neither is settled by intuition.

**First, the Git history.** The working tree was cleaned in `2ca2169` — file count, byte volume, two
real search terms, and one real series title from the personal library are gone — but the earlier
history still holds them. Measured:

| Measure | Value |
|---|---:|
| Commits on the branch | 46 |
| Commits holding the byte volume | 2 |
| Commits holding the real title | 3 |
| Commits holding a real search term | 2 |
| Distinct SHAs cited in documentation | 28 |
| Documentation files citing a SHA | 26 |

The affected commits are from I1 and I2, near the beginning. Rewriting would change the SHA of
practically all forty-six commits and of `main`.

**Second, how to capture traffic in T38.** The plan asks for a thirty-minute proxy-captured journey
and a payload/log/PCAP scan. A TLS-decrypting proxy requires installing a root certificate in the
user's store, which is a change to the machine's security configuration. `pktmon` ships with Windows
but requires elevation, which this environment does not have.

### Decision

**1. The Git history is not rewritten.** The public repository will be created fresh from the
cleaned tree rather than as a copy of the private history. The private repository keeps its full
history, where its twenty-eight commit references remain verifiable. The exact publication mechanism
is executed in T41.

**2. T38's privacy capture does not use a TLS-decrypting proxy.** It is four pieces, all without
elevation, without installing anything, and without touching the certificate store:

- An `EventListener` over the `System.Net.Http`, `System.Net.Sockets`, and
  `System.Net.NameResolution` event sources **inside the process**, recording every request with its
  URI, every connection, and every name resolution **before encryption**.
- A local canary server counting requests, as in T32.
- Continuous per-process TCP connection sampling, as in C6.
- A direct scan of the diagnostics file and the logs, which are local and never travel in the MVP.

### Consequences

- **The twenty-eight commit references are an internal record.** In the public repository they will
  point at commits that do not exist there. T41 must add a note saying so, or replace them with
  references to the evidence files, which do travel.
- **The development history is lost in public.** That is acceptable: the process — RED, GREEN,
  coverage, gates — is documented in `docs/evidence/`, which is published, not in commit messages.
- **No force push and no change to `main`.** Rewriting would have required both on an already
  published branch.
- **T38's capture observes the URI but not the encrypted body.** That is not a real limitation for
  what must be shown: the only legitimate traffic is TMDB, and what is sent is demonstrated in the
  code — closed DTOs, no reflective serialization of entities, a purpose registry per `HttpClient` —
  and in the scan of the local payload. A MITM proxy would have added the decrypted body in exchange
  for installing a root certificate, exactly the kind of change a privacy task should not introduce.
- **The `EventListener` covers what the .NET process does, not what the system does.** That is the
  correct scope of the claim — "the application produces no unrequested traffic" — and T38's evidence
  must state it that way rather than presenting it as a full network capture.
- If packet-level capture is ever wanted, `pktmon` is already on the system and only needs an
  elevated session; nothing has to be installed.
