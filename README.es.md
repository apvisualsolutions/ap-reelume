# AP Reelume

Una biblioteca de vídeo local y su reproductor, para Windows 11 x64. Cataloga los vídeos **donde
están**, los identifica, los reproduce y recuerda dónde se quedó. Sin cuenta, sin suscripción y sin
enviar nada a ninguna parte.

*Read this in [English](README.en.md).*

## Qué es

Una persona, un PC, sus archivos. AP Reelume lee las carpetas que usted le indica —locales, USB o
NAS por UNC— y construye un catálogo a partir de ellas. **Nunca copia ni mueve un vídeo** para
catalogarlo.

- **Biblioteca.** Escaneo inicial, al iniciar, manual e incremental, cancelable y reanudable, sobre
  10.000 archivos sin bloquear la interfaz.
- **Identificación.** Detecta película, serie, temporada y episodio por nombre y carpeta, y consulta
  TMDB en español con idioma alternativo. Lo dudoso va a una bandeja de revisión en lugar de
  clasificarse solo.
- **Reproducción.** LibVLC integrado, con apertura externa como alternativa. Contenedores y códecs
  habituales, incluidos H.264, HEVC y AV1; HDR10 con conversión de tono a SDR cuando la pantalla no
  lo admite.
- **Continuidad.** Guarda el progreso cada cinco segundos y en pausa, búsqueda y cierre; reanuda
  dentro de ±5 s incluso tras un cierre inesperado.
- **Suyo.** Favoritos, ver más tarde, valoración y recomendaciones locales que se explican y se
  pueden desactivar.

## Qué no es

No hay cuentas, ni sincronización, ni nube. No convierte ni edita vídeo. No reproduce varios a la
vez. La lista completa, con sus identificadores, está en la
[hoja de ruta](docs/roadmap/README.es.md).

## Privacidad

Cero telemetría sin consentimiento, y el consentimiento es reversible. La identificación remota sólo
funciona si usted pone un token de TMDB a mano en `AP_LOCALMEDIA_TMDB_TOKEN`: **el artefacto no lleva
ninguno**, así que sin ese acto deliberado la aplicación no abre ninguna conexión de metadatos. El
comprobador de actualizaciones es la otra conexión posible, también bajo su control y desactivado de
fábrica; la tabla completa de destinos está en la declaración de privacidad. Los diagnósticos
son opt-in, se sanitizan y retirar el consentimiento borra el informe.

El detalle está en la [declaración de privacidad](docs/privacy/PRIVACY.es.md).

## Instalación

Descargue el ZIP de la publicación, descomprímalo donde quiera y ejecute
`ApSolutions.LocalMedia.Windows.exe`. No requiere instalación, ni permisos de administrador, ni
escribe en el registro.

**Windows mostrará un aviso de SmartScreen.** Es correcto: esta publicación **no está firmada**, y no
afirmamos lo contrario. Qué comprobar en su lugar —el hash publicado y la compilación reproducible—
está en [SMARTSCREEN.es.md](docs/release/SMARTSCREEN.es.md).

Sus datos van a `%LOCALAPPDATA%\APSolutions\LocalMedia`, salvo que nombre otra carpeta con
`AP_LOCALMEDIA_DATA_ROOT`. Para desinstalar, borre la carpeta que descomprimió; sus datos siguen
donde estaban.

## Documentación

| | |
|---|---|
| [Manual de uso](docs/user-guide/README.es.md) | Cómo hacer cada cosa |
| [Solución de problemas](docs/troubleshooting/README.es.md) | Qué hacer cuando algo no va |
| [Hoja de ruta](docs/roadmap/README.es.md) | Qué viene y qué no se hará |
| [Matriz de funcionalidades](docs/FEATURES.md) | El registro canónico del alcance |
| [Privacidad](docs/privacy/PRIVACY.es.md) | Qué se guarda y qué no sale de aquí |
| [Cambios](docs/CHANGELOG.es.md) | Qué cambió en cada versión |
| [Guía de desarrollo](docs/development/README.es.md) | Cómo compilar y verificar |
| [Publicación](docs/release/RELEASING.es.md) | Cómo se corta una versión |
| [Decisiones](docs/adr) | Por qué el proyecto es como es |

## Licencia

GPL-3.0-or-later. Vea [LICENSE](LICENSE), [NOTICE](NOTICE) y los
[avisos de terceros](docs/release/THIRD-PARTY-NOTICES.es.md). Este producto usa la API de TMDB pero
no está avalado ni certificado por TMDB.
