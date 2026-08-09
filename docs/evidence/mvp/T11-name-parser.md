# T11 — Analizador seguro de nombres / Safe media-name parser

- Fecha / Date: 2026-08-01
- Rama / Branch: `codex/ap-reelume-mvp-x64`
- Commit base / Base commit: `234845c238674377adae6c83e4955928f6bdf695`
- Commit de tarea / Task commit: `feat: parse movie and episode filenames safely`
- Entorno / Environment: Windows 11 x64, .NET SDK 10.0.302
- IDs: `LIB-005=VERIFIED`, `UX-008=OUT_OF_SCOPE`

## RED y GREEN / RED and GREEN

Las pruebas y el fixture se escribieron antes del código de producción. El RED
falló únicamente porque no existían el espacio de nombres de identificación ni
`MediaNameParser`; la salida se conserva en
`artifacts/test-results/T11/red/T11-red.log`. / Tests and the fixture were
written before production code. RED failed only because the identification
namespace and `MediaNameParser` did not exist; the output is retained at the
path above.

El filtro GREEN conserva su TRX en
`artifacts/test-results/T11/green/T11-green.trx`: 16/16 pruebas, cero fallos,
incluidos el fixture completo, rutas Unicode largas y dos propiedades FsCheck
de 10.000 casos. / The GREEN filter retains its TRX at the path above: 16/16
tests, zero failures, including the full fixture, long Unicode paths, and two
10,000-case FsCheck properties.

| Propiedad / Property | Semilla replay / Replay seed | Casos / Cases |
|---|---:|---:|
| Nunca lanza y conserva el original / Never throws and preserves source | `314159,271829` | 10.000 |
| Rangos inválidos nunca autoclasifican / Invalid ranges never auto-classify | `161803,398875` | 10.000 |

## Corpus y resultado / Corpus and result

El fixture `media-name-cases.json` cubre `S01E02`, `s1e2`, `1x02`,
`Cap.803→S08E03` con contexto, `Cap.800` dudoso/especial, temporadas escritas
en español e inglés, películas con año, tags `[1080p][HEVC]`, `WEB-DL`, HDTV,
Unicode (`Amélie`, `東京物語`), año inválido y entrada malformada. / The fixture
covers the stated episode forms, context-sensitive compact episodes, written
Spanish/English seasons, year-tagged movies, noisy tags, Unicode, invalid year,
and malformed input.

`Cap.803` solo se resuelve cuando la carpeta confirma la temporada 8;
`Cap.800` y `Cap.803` sin ese contexto conservan `AmbiguousCompactEpisode` y
nunca se autoclasifican. Temporadas >99, episodios >999 y años fuera de
1888–2100 permanecen desconocidos. / Compact parsing requires supporting
folder context; ambiguous compact and out-of-range values remain unknown and
cannot be automatically classified.

La tokenización es acotada y las expresiones regulares usan el motor
`NonBacktracking` con timeout de 100 ms. El corpus completo, incluidas dos
propiedades de 10.000 casos, termina en menos de 2 s. / Tokenization is bounded
and every regular expression uses the non-backtracking engine with a 100 ms
timeout. The full corpus, including both 10,000-case properties, finishes in
under two seconds.

## Cobertura y alcance / Coverage and scope

Cobertura focal Cobertura para el código nuevo: `MediaNameParser` 100 % de
líneas y 91,42 % de ramas; los modelos de contrato alcanzan 100/100 %. / Focused
Cobertura for new code reports 100% lines and 91.42% branches for the parser,
with 100/100% for its contract models.

Una búsqueda de producción confirma que T11 no crea marcadores, notas,
capturas ni otros modelos personales de línea de tiempo. `UX-008` sigue
`OUT_OF_SCOPE`: esta evidencia demuestra ausencia, no una implementación. /
A production-source review confirms T11 creates no timeline bookmarks, notes,
captures, or related personal models. `UX-008` remains `OUT_OF_SCOPE`; this is
negative-scope evidence, not implementation.

El parser es puro, no realiza I/O, no consulta red y no copia, mueve, renombra
ni elimina archivos. Todos los tipos nuevos permanecen bajo
`ApSolutions.LocalMedia.Domain.Identification`. / The parser is pure, performs
no I/O or network access, and never copies, moves, renames, or deletes files.
All new types remain under the stable internal namespace.
