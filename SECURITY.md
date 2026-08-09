# Seguridad / Security

AP Reelume es una aplicación local: cataloga y reproduce vídeos que ya están en tu disco. No hay
cuentas, no hay telemetría y la red sólo se usa para los propósitos declarados en
[PRIVACY](docs/privacy/PRIVACY.es.md) — cada host permitido está en una lista que una prueba
mantiene sincronizada con el código. / AP Reelume is a local application: it catalogs and plays
videos already on your disk. There are no accounts, no telemetry, and the network is used only for
the declared purposes in [PRIVACY](docs/privacy/PRIVACY.en.md) — every allowed host sits on a list
a test keeps in sync with the code.

## Cómo reportar una vulnerabilidad / How to report a vulnerability

Usa **GitHub → Security → Report a vulnerability** (aviso privado) en este repositorio. No abras
un issue público con los detalles: un aviso privado nos deja corregir antes de exponer a quien ya
tiene la aplicación instalada. Respondemos en el propio aviso; si el hallazgo se confirma, la
corrección sale en la siguiente publicación con su nota en el changelog. / Use **GitHub →
Security → Report a vulnerability** (private advisory) on this repository. Please do not open a
public issue with the details: a private advisory lets us fix before exposing whoever already has
the application installed. We reply in the advisory itself; a confirmed finding ships in the next
release with its changelog note.

## Versiones con soporte / Supported versions

| Versión / Version | Soporte / Supported |
|---|---|
| 0.1.x (MVP) | ✔ correcciones en la siguiente publicación / fixes in the next release |
| anteriores / earlier | ✘ |

## La clave de firma de publicaciones / The release signing key

**Qué cubre.** Cada publicación firma `SHA256SUMS.txt` con una clave **minisign** (Ed25519,
implementada sobre BouncyCastle). La clave **pública** viaja embebida en el binario (y en
[`eng/release-signing.pub`](eng/release-signing.pub)); el actualizador verifica la firma antes de
creer ninguna huella, y rechaza (`UnsignedChecksums`) toda versión cuyas huellas no verifiquen. El
hash esperado sale únicamente del bloque firmado — nunca del JSON sin firmar de la release. / Each
release signs `SHA256SUMS.txt` with a **minisign** key (Ed25519 over BouncyCastle). The **public**
key ships embedded in the binary (and in `eng/release-signing.pub`); the updater verifies the
signature before believing any checksum and refuses (`UnsignedChecksums`) any version whose
checksums do not verify. The expected hash comes only from the signed block — never from the
release's unsigned JSON.

**Qué NO cubre.** Esta capa autentica el paquete **ante el actualizador**, no ante Windows: el
artefacto no lleva firma Authenticode, así que SmartScreen mostrará su aviso en la primera
ejecución, tal y como documenta [SMARTSCREEN](docs/release/SMARTSCREEN.es.md). Son capas distintas
a propósito. / **What it does NOT cover.** This layer authenticates the package **to the
updater**, not to Windows: the artifact carries no Authenticode signature, so SmartScreen will
warn on first run, exactly as [SMARTSCREEN](docs/release/SMARTSCREEN.en.md) documents. They are
separate layers on purpose.

**Dónde vive la privada.** Fuera del repositorio, siempre: como secreto de GitHub Actions
(`RELEASE_SIGNING_SECRET_KEY`) para la tubería, y como copia custodiada del propietario para
firmar en local (`RELEASE_SIGNING_KEY_FILE`). Ningún archivo versionado ni ningún log la contiene;
`prepare-release` bloquea una publicación sin firma. / **Where the private key lives.** Outside
the repository, always: as a GitHub Actions secret for the pipeline and as the owner's custodied
copy for local signing. No versioned file and no log contains it; `prepare-release` blocks an
unsigned release.

**Cómo se rota.** Rotar (o revocar tras una sospecha de compromiso) es publicar: se genera un par
nuevo, la clave pública embebida se sustituye en el código, y la versión siguiente viaja con ella.
Las instalaciones existentes verifican esa versión con la clave vieja (la última firmada por
ella); a partir de ahí, todo verifica contra la nueva. Si la privada se viera comprometida, además
del reemplazo se publica un aviso de seguridad nombrando qué versiones firmó. / **How it
rotates.** Rotating (or revoking after suspected compromise) is releasing: a new pair is
generated, the embedded public key is replaced in the code, and the next version ships with it.
Existing installations verify that version with the old key (the last one it signed); from then
on, everything verifies against the new one. If the private key were compromised, the replacement
ships together with a security advisory naming which versions it signed.

## Qué endurecimiento ya es permanente / What hardening is already permanent

- Toda dependencia va anclada con lockfiles y auditada en cada build (`NuGetAuditLevel=moderate`
  falla el build). / Every dependency is lockfile-pinned and audited on every build.
- Las acciones de CI van ancladas por SHA de commit y sólo se mueven por revisión (dependabot). /
  CI actions are pinned by commit SHA and move only by review.
- El actualizador limita tamaños de respuesta, confina redirecciones a la allowlist de hosts y
  borra descargas parciales que no verifican. / The updater caps response sizes, confines
  redirects to the host allowlist, and deletes partials that fail verification.
- El SBOM se genera en cada verificación con cero huecos declarados. / The SBOM is generated on
  every verification with zero declared gaps.
