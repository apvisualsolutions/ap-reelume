# El actualizador dice la versión que existe / The updater announces a version that exists

Evidencia de **ARQ-014**: el User-Agent anunciaba `AP-Reelume-Updater/1.0` mientras el producto
declaraba `0.1.0`. / Evidence for **ARQ-014**: the User-Agent announced `AP-Reelume-Updater/1.0`
while the product declared `0.1.0`.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-14.

## La medición previa / The measurement first

El plan pedía comprobar antes si el texto estaba fijado en más de un sitio, porque entonces el cambio
sería de dos. Medido sobre `src/` y `tests/`: / The plan asked whether the string was pinned
anywhere else, because then the change would be in two places. Measured:

```
src/…/Updates/GitHubReleaseUpdateProvider.cs:76   ProductInfoHeaderValue("AP-Reelume-Updater", "1.0")
(ninguna otra aparición / no other occurrence — ni en NetworkPurposeRegistry ni en prueba alguna)
```

Un solo sitio, y **ninguna prueba lo miraba**: por eso pudo quedarse en `1.0` mientras la versión del
producto avanzaba. / One place, and no test looked at it, which is how it stayed at `1.0`.

## El rojo / The red

```
UpdaterIdentityTests.The_updater_announces_the_version_the_product_declares [FAIL]
  Assert.Equal() Failure: Strings differ
  Expected: "AP-Reelume-Updater/0.1.0"
  Actual:   "AP-Reelume-Updater/1.0"
```

La prueba afirma sobre **la cabecera que sale de verdad** —la petición llega al servidor falso y se
lee de sus cabeceras—, no sobre una constante del código. Una aserción contra la constante habría
pasado sin decir nada, que es el defecto de origen repetido en la prueba. / The test asserts on the
header that actually leaves, read from the fake server's request, rather than on a constant in the
code: asserting the constant would repeat the very defect.

Y la versión esperada tampoco se escribe: se lee de `Directory.Build.props`, que es la única fuente
de la versión en este repositorio. Una copia aquí sería el mismo defecto con otro nombre. / The
expected version is read from `Directory.Build.props` rather than written down.

## La corrección / The fix

La marca se queda —es el nombre público— y el número sale del ensamblado
(`AssemblyInformationalVersionAttribute`), cortado en `+`: lo que va después identifica un commit, y
a la otra punta se le está diciendo **qué versión pregunta**. Si el atributo faltara, cae a la
versión del ensamblado y luego a `0.0.0`, porque una identidad que lanza al construirse convertiría
un fallo de metadatos en un actualizador que no arranca. / The brand stays and the number comes from
the assembly, cut at `+`; a missing attribute falls back rather than throwing, because an identity
that throws would turn a metadata problem into an updater that cannot start.

## Verde / Green

| Puerta / Gate | Resultado / Result |
|---|---|
| `UpdaterIdentityTests` | 1 de 1 / of 1 |
| `ApSolutions.LocalMedia.IntegrationTests` | 420 de 421, 1 omitida / 1 skipped |
| `eng/verify.ps1` completo / full | verde / green |
