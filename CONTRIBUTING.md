# Cómo contribuir / Contributing

Gracias por mirar el código. AP Reelume es software libre bajo `GPL-3.0-or-later`, y este documento
dice qué esperar antes de que inviertas tiempo. / Thanks for looking at the code. AP Reelume is free
software under `GPL-3.0-or-later`, and this document says what to expect before you spend time.

## Antes que nada: el proyecto tiene un alcance cerrado / First: the project has a closed scope

Esto es una biblioteca de medios **local**, para **Windows 11**, sin cuentas, sin telemetría y sin
servidor. [FEATURES.md](docs/FEATURES.md) es el registro canónico de lo que existe y en qué estado, y
[la hoja de ruta](docs/roadmap/README.es.md) dice también **qué no se hará**. Una propuesta que
convierta el programa en un servidor, un servicio o un cliente de streaming se agradecerá y se
rechazará. / This is a **local** media library for **Windows 11**, with no accounts, no telemetry, and
no server. The roadmap also says what will not be done.

## Reportar un problema / Reporting a problem

- **¿Es una vulnerabilidad?** No abras un issue. Sigue [SECURITY.md](SECURITY.md): aviso privado por
  GitHub. / **A vulnerability?** Do not open an issue; follow SECURITY.md.
- **¿Es un problema legal o de atribución?** Mismo canal privado, y se corrige sin discutir la
  corrección. El contexto está en [el estado legal](docs/legal/LEGAL.es.md). / **A legal or
  attribution problem?** Same private channel.
- **¿Cualquier otra cosa?** Abre un issue con la plantilla que corresponda. Un fallo sin pasos para
  reproducirlo es una intuición, y una intuición no se puede arreglar. / **Anything else?** Open an
  issue with the matching template.

## Preparar la máquina / Setting up

Necesitas Windows 11, el SDK de .NET que fija [`global.json`](global.json) —hoy 10.0.302— y FFmpeg
en el `PATH` (o en `FFMPEG_PATH`) si vas a tocar las pruebas de medios. La guía larga está en
[docs/development](docs/development/README.es.md). / You need Windows 11, the .NET SDK pinned in
`global.json`, and FFmpeg on `PATH` if you touch the media tests.

```powershell
git clone https://github.com/apvisualsolutions/ap-reelume.git
cd ap-reelume
./eng/verify.ps1 -Configuration Release -Runtime win-x64
```

Si `verify.ps1` pasa en tu máquina, tu entorno está bien. Si no, arréglalo antes de escribir código:
un rojo preexistente convierte cualquier cambio tuyo en sospechoso. / If `verify.ps1` passes, your
environment is fine. Fix it before writing code if it does not.

## El ciclo que sigue este repositorio / The cycle this repository follows

No es negociable, y es la razón de que la matriz de funcionalidades signifique algo:

1. **Rojo primero.** Escribe la prueba que falla y **archiva su salida**. Un arreglo sin un rojo
   previo no demuestra nada: demuestra que el código pasa las pruebas que se escribieron después de
   escribirlo. / **Red first**, and archive the failure output.
2. **La corrección mínima.** Nada de refactores de paso; van en su propio commit.
   / **The minimal fix.** No drive-by refactors.
3. **Verde y puertas.** `dotnet format --verify-no-changes`, build con `-warnaserror`, las suites
   afectadas con `-m:1 --settings eng/test.runsettings`, `eng/verify-docs.ps1`, y la puerta de
   cobertura si añades archivos. / **Green plus the gates.**
4. **Evidencia.** Lo que mediste, con números, en `docs/evidence/`. «Funciona» no es evidencia.
   / **Evidence** with numbers.
5. **Changelog en los dos idiomas**, describiendo el efecto para quien usa el programa, no el diff.
   / **Changelog in both languages**, describing the effect on the person using the program.
6. **Un commit.** Mensaje en inglés, en imperativo, diciendo qué cambia para el usuario.
   / **One commit**, in English, imperative, saying what changes for the user.
7. **Push a la rama, y `main` sólo con CI en verde.** Quien verifica de verdad es CI: corre
   `eng/verify.ps1` **y además** las dos pasadas de accesibilidad y de recuperación y la puerta del
   paseo, así que cubre estrictamente más que cualquier ejecución local. El fast-forward a `main`
   espera a ese verde, y por eso `main` no vuelve a verificar lo que ya se verificó.
   / **Push to the branch; `main` only once CI is green** — CI runs strictly more than any local run.

Las pruebas se llaman como frases porque se leen como el informe de lo que el programa promete.
`Content_kept_longer_than_the_TMDB_retention_limit_is_neither_served_nor_kept` dice más que
`TestCacheExpiry`. / Tests are named as sentences because they read as the report of what the program
promises.

## Lo que la revisión va a mirar / What review will look at

- **Cabecera de licencia.** Todo archivo fuente nuevo lleva `SPDX-License-Identifier:
  GPL-3.0-or-later`. La puerta de formato lo exige, así que lo verás antes que nadie.
  / **Licence header** on every new source file; the formatting gate demands it.
- **Bilingüismo.** Cadenas de interfaz y documentos públicos, en español y en inglés. Una prueba
  compara la estructura de los dos. / **Both languages** for user-facing strings and public
  documents.
- **Accesibilidad.** Toda superficie nueva se recorre con teclado y se nombra para el lector de
  pantalla. Hay una puerta que lo comprueba. / **Accessibility**: keyboard and screen-reader names.
- **Privacidad.** Ninguna conexión de red que no esté declarada en `NetworkPurposeRegistry`, porque
  una prueba recorre el árbol buscando hosts no declarados y falla. Ningún dato personal en
  diagnósticos: la lista de campos permitidos es cerrada. / **Privacy**: no undeclared network host,
  no personal data in diagnostics.
- **Nada personal en el repositorio.** Ni rutas de tu máquina, ni nombres de tu biblioteca, ni
  capturas con tus archivos. / **Nothing personal** in the repository.

## Lo que aceptamos con más ganas / What is most welcome

Correcciones con su prueba, mejoras de accesibilidad, traducciones que arreglen una frase torpe,
y hallazgos de seguridad o de licencia. Lo que más cuesta revisar —y por tanto lo que más tarda— es
una funcionalidad grande que nadie discutió antes: abre un issue primero y ahorra el viaje.
/ Fixes with their test, accessibility work, translation corrections, and security or licence
findings. Discuss a large feature in an issue first.

## Licencia de tus aportaciones / Licensing of your contributions

Al enviar un pull request aceptas que tu aportación se publique bajo `GPL-3.0-or-later`, la misma
licencia del proyecto. No se pide firmar un CLA ni ceder el copyright: conservas el tuyo.
/ By opening a pull request you agree that your contribution is published under
`GPL-3.0-or-later`. There is no CLA and no copyright assignment: you keep yours.
