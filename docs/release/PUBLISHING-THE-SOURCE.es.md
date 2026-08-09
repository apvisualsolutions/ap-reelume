# Antes de hacer público el repositorio

Publicar no se deshace: el código queda copiado, indexado y archivado por terceros el mismo día. Esta
es la revisión que se hizo antes de que esa decisión se tome, y lo que queda por decidir. La versión
inglesa está en [PUBLISHING-THE-SOURCE.en.md](PUBLISHING-THE-SOURCE.en.md).

## Qué se revisó

Todo el historial, no sólo el árbol actual: **76 commits** y **696 archivos** distintos añadidos
alguna vez, en todas las ramas.

| Se buscó | Resultado |
|---|---|
| Nombre de cuenta del desarrollador | 0 apariciones |
| Nombre del equipo | 0 apariciones |
| Rutas locales del desarrollador | 0 apariciones |
| Claves privadas, tokens de GitHub, credenciales de nube | 0 apariciones |
| Archivos de vídeo, bases de datos, certificados, `.env` | ninguno, en ningún commit |
| Correos electrónicos | sólo el de la cuenta que firma los commits |
| Rutas de ejemplo en pruebas | todas ficticias, con nombres inventados |

Dos coincidencias resultaron ser lo contrario de una fuga: una es la lista de patrones del propio
escáner de secretos, y otra es una cabecera JWT de ejemplo cuyo cuerpo dice literalmente
`body.signature`, usada para comprobar que los diagnósticos redactan credenciales.

## Lo que sí apareció

**El título de una serie de la biblioteca personal, como dato de ejemplo en pruebas.** Ya se había
redactado una vez, en `docs: redact the personal library from evidence and fixtures`, sustituyéndolo
por un nombre inventado. Esa redacción **estaba incompleta**: cambió el título en español y dejó el
inglés, porque el patrón que se buscaba estaba escrito en español. Ahora está completa en el árbol
actual.

**El historial conserva las dos formas.** Redactar en un commit posterior no borra los anteriores.
Quitarlo del pasado exige reescribir el historial, lo que cambia todos los identificadores de commit.

**La rama por defecto también contaba.** La redacción vivía sólo en la rama de trabajo: el árbol de
`main` siguió mostrando el título hasta el **2026-08-08**, y `main` es lo primero que enseña un
repositorio público. Ese día se avanzó `main` hasta la rama en fast-forward — sin reescribir nada —
y desde entonces `prepare-release.ps1` bloquea cualquier publicación con `main` por detrás, para
que esto no vuelva a depender de que alguien se acuerde.

## Lo que queda por decidir

**Si merece la pena reescribir el historial.** Lo que quedaría expuesto es el título de una serie muy
conocida usado como ejemplo de nombre de archivo. No revela una ruta, ni una cuenta, ni un
inventario: revela que alguien, en algún momento, usó ese título como caso de prueba. Reescribir
invalidaría los identificadores de los 76 commits y cualquier copia existente.

**El correo de la cuenta que firma los commits será público.** Es una dirección de la organización,
no personal, pero conviene saberlo antes y no después.

## La comprobación ya no depende de que alguien se acuerde

`RepositoryPrivacyTests` recorre los archivos versionados en cada ejecución de la suite y falla si
encuentra el nombre de cuenta, el del equipo, la ruta del perfil, la ruta del repositorio o el nombre
de cualquiera de las carpetas que git ignora junto al repositorio — que son, precisamente, la
biblioteca personal.

**Ningún dato personal está escrito en esa prueba.** Todos los valores se leen de la máquina donde se
ejecuta, así que funciona para cualquiera y no añade al repositorio lo que pretende sacar de él.

Lo que no puede hacer es reconocer una traducción: una serie nombrada en un idioma en una carpeta y
en otro dentro de una prueba le resulta invisible, que es exactamente cómo sobrevivió la anterior.
Eso sigue siendo criterio humano, y decirlo es mejor que dar a entender que la comprobación lo cubre
todo.
