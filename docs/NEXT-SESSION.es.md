# Dónde retomar

Estado del proyecto al cerrar la sesión del **2026-08-10**, la primera con el repositorio ya público.
La versión inglesa está en [NEXT-SESSION.en.md](NEXT-SESSION.en.md). El registro canónico del alcance
sigue siendo [FEATURES.md](FEATURES.md); el trabajo pendiente de la auditoría vive en
[2026-08-08-audit-remediation.md](superpowers/plans/2026-08-08-audit-remediation.md). Esto es sólo el
punto de retomada.

## Verificación de arranque

```powershell
$env:DOTNET_ROOT="$env:USERPROFILE\.dotnet"; $env:PATH="$env:DOTNET_ROOT;$env:PATH"
dotnet --version                                        # 10.0.302
git status --short --branch                             # limpio, sobre origin/codex/ap-reelume-mvp-x64
git merge-base --is-ancestor main HEAD; $LASTEXITCODE   # 0
```

## Dónde vive el código ahora

`apvisualsolutions/ap-reelume` es **público** desde el 2026-08-10, con un corte fresco de un solo
commit raíz. El historial completo de desarrollo quedó en `apvisualsolutions/ap-reelume-archive`
(privado), de modo que **los SHA que citan los documentos de evidencia resuelven en el archivo, no
aquí**. Los remotos locales son `origin` (público) y `archive` (el histórico), y las ramas viejas se
conservan como `archived/main` y `archived/codex/…` apuntando al archivo.

La CI corre en runners hospedados, gratuitos en repositorios públicos. **La facturación dejó de
importar**: era el bloqueo de la sesión anterior y ya no existe. El runner self-hosted sigue instalado
en `.runner/` (ignorado por git) pero **apagado**, y el workflow ya no tiene forma de llamarlo: la
puerta de escape por variable de repositorio se retiró en esta sesión precisamente porque un
repositorio público la convierte en un riesgo.

## Qué está terminado en esta sesión

- **Auditoría de seguridad completa sobre el repositorio público.** Quince fases, dos exploraciones
  independientes y verificación de cada hallazgo. **Cero críticos, cero altos.** Tres medios, los tres
  aplicados o programados: la puerta al runner self-hosted (retirada), ffmpeg sin anclar en las dos
  tuberías (anclado a una versión concreta) y dependabot sin cubrir NuGet (cubierto en WP-9, abajo).
  Lo que la auditoría encontró **limpio** merece anotarse porque costó construirlo: SQL enteramente
  parametrizado, triple defensa contra zip-slip, verificación del actualizador en el orden correcto y
  allowlist de host aplicada en **cada** salto de redirección. El informe está en
  `.gstack/security-reports/2026-08-10-comprehensive.json` (local, ignorado por git).
- **Revisión legal completa, corrigiendo.** Los 624 archivos fuente llevan ahora cabecera SPDX y la
  puerta de formato la exige; los avisos de terceros pasaron de nombrar 8 componentes a nombrar los 30
  que el paquete transporta de verdad, con una prueba que lo mantiene; la atribución de TMDB dice la
  frase exacta que sus términos exigen; y **nada de TMDB se conserva más de 180 días**, que era una
  desviación real de sus términos. Evidencia en
  [audit-legal-public.md](evidence/stable/audit-legal-public.md), estado en
  [LEGAL.es.md](legal/LEGAL.es.md).
- **WP-9 completo.** `CONTRIBUTING.md`, `CLAUDE.md` en la raíz, plantillas de issue y de pull request,
  `CODEOWNERS` y dependabot cubriendo NuGet con grupos para Avalonia y las herramientas de prueba.
- **Una puerta que mentía, arreglada.** `PinnedDependencyTests` escaneaba `*.csproj` desde la raíz sin
  filtrar y fallaba en cualquier máquina que tuviese el runner instalado dentro del árbol, mientras
  seguía verde en CI. Roja en local y verde en la tubería es la peor forma de que una puerta esté
  equivocada.

## Lo que sigue (en este orden)

1. **ARQ-006 pasos 2-3**: módulos `AddData`/`AddPlayback`/…, y extraer `WindowsFilePickers`,
   `DatabaseStartup` y `WindowLifecycle` de `CompositionRoot`. Después ARQ-001/004/005/010.
2. **La deuda de cobertura** nombrada en [TST1-coverage-gate.md](evidence/stable/TST1-coverage-gate.md)
   (ramas de error de tres archivos): se salda cuando se toque esa zona; la puerta no la exige
   retroactivamente.
3. **Endurecimientos opcionales que la auditoría dejó anotados como no explotables**, si algún día se
   toca esa zona: acotar la copia del ZIP de backup al tamaño declarado (hoy los topes se apoyan en un
   dato que el archivo declara de sí mismo) y revalidar la extensión en
   `ShellExternalPlaybackLauncher` en vez de confiar en que todos los llamantes ya filtran.

## Pendiente tuyo (sólo lo que un agente no puede hacer)

Aquí no van tareas de revisión técnica: ésas se hacen y se deciden en la sesión. Los PR de dependabot
de esta tanda —`checkout` 7.0.1, `setup-dotnet` 6.0.0 y `upload-artifact` 7.0.1— se revisaron
comprobando cada SHA contra su etiqueta y leyendo los cambios de ruptura de cada salto mayor, y se
aplicaron en la rama de trabajo, que es donde la convención de la casa los quiere; dependabot cierra
los suyos al ver la dependencia ya actualizada.


- **Añadir el secreto `RELEASE_SIGNING_SECRET_KEY` al repositorio público.** No se pudo copiar —los
  secretos no se leen—, y **sin él la tubería de publicación falla a propósito**: `release.yml`
  comprueba que `SHA256SUMS.txt.minisig` existe y verifica, y se detiene si no. Es lo único que separa
  al proyecto de poder cortar su primera versión pública. La copia está donde la dejaste (ver
  `SECURITY.md`).
- El **paseo físico manual de diez minutos**
  ([audit-physical-walk.md](evidence/stable/audit-physical-walk.md)).
- La **copia de seguridad cifrada** de la clave de firma.
- **El dictamen jurídico profesional** (`REL-004`) y los cinco puntos que
  [LEGAL.es.md](legal/LEGAL.es.md) nombra: los complementos de VideoLAN, el logotipo de TMDB, la
  notificación de exportación por la criptografía que el paquete lleva, y marca y dominio.
- Las decisiones económicas de siempre: certificado Authenticode, Store, hardware ARM64.

## Cosas aprendidas que conviene no volver a aprender

- **Una puerta verde en CI y roja en local no es un incordio: es la puerta estando equivocada.** Si
  falla sólo en tu máquina, mira si escanea desde la raíz sin filtrar lo que git ignora.
- **`dotnet format` sabe poner cabeceras de licencia.** `file_header_template` en `.editorconfig` más
  `IDE0073` convierte una puerta que ya existía en la que exige la cabecera; no hizo falta inventar
  ninguna.
- **El compilador de XAML de Avalonia acepta un comentario antes del elemento raíz.** Se comprobó
  compilando un archivo antes de tocar los otros cincuenta, que es la única forma de saberlo.
- **Un límite de caché no es un límite de retención.** El TTL decide cuándo volver a preguntar; la
  retención decide cuándo el dato ya no puede existir. Los caminos degradados —sin credencial, sin
  red— son exactamente donde el segundo se olvida.
- **Los avisos de terceros escritos a mano se quedan atrás en silencio.** Sólo se supo cuánto
  contrastando tres fuentes que tenían que coincidir: el SBOM, el cierre del lock file y los binarios
  que de verdad viajan en el paquete.
