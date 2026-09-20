#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# PreToolUse de Write, Edit y MultiEdit sobre los dos trinquetes del arbol.
#
# Vive en un archivo y no dentro de settings.json por el motivo ya medido el
# 2026-08-29 con post-write.sh: el harness imprime el comando ENTERO DOS VECES
# delante del texto, y en linea eso costaba 2.712 caracteres de contexto por
# aviso contra 488 desde un archivo.
#
# Los dos trinquetes se tratan distinto, y confundirlos ya costo una correccion:
#
#   eng/coverage-debt.txt  LO PRODUCE CI. Se copia del artefacto coverage-debt
#                          de un run, por consola, y nunca se edita. Aqui no hay
#                          nada que distinguir: se deniega siempre.
#
#   eng/walk-pending.txt   NO lo produce nadie mas que este arbol. Es una lista
#                          que solo puede encoger, PERO lleva encima sus propias
#                          cabeceras explicando por que subio cada vez, y esas
#                          cabeceras caducan.
#
# ENG-015, y es la razon de que este archivo exista: hasta el 2026-09-20 la
# guarda denegaba TODA escritura sobre walk-pending.txt sin distinguir entre
# anadir una fila —que es lo que debe impedir— y corregir un comentario
# caducado. Resultado: tres cifras desfasadas en su cabecera que no se podian
# arreglar con las herramientas de edicion, y el propio guardian sosteniendo la
# contradiccion que la tarea denunciaba. Un guardian que impide arreglar su
# propia documentacion acaba con alguien buscando como esquivarlo.
#
# El criterio: se deniega si el cambio toca una FILA; se deja pasar si solo toca
# COMENTARIOS. Una fila es una linea que no empieza por «##» y no esta vacia; en
# este archivo todas tienen la forma «VistaView#NombreControl», asi que llevan
# almohadilla y un comentario nunca puede confundirse con una.
set -u

payload=$(cat)
f=$(printf '%s' "$payload" | jq -r '.tool_input.file_path // empty' | tr -d '\r')
[ -z "$f" ] && exit 0

deny () {
  jq -nc --arg r "$1" \
    '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"deny",permissionDecisionReason:$r}}'
  exit 0
}

case "$f" in
  *coverage-debt.txt)
    deny "eng/coverage-debt.txt LO PRODUCE CI: se copia del artefacto coverage-debt de un run, y nunca se edita a mano ni se genera con una ejecucion local (CLAUDE.md). Editarlo a mano relaja en silencio la cobertura de todo el arbol. Para copiarlo usa la consola: gh run download <id> -n coverage-debt, y normaliza CRLF a LF o el diff son 402 lineas en vez de una."
    ;;
  *walk-pending.txt) ;;
  *) exit 0 ;;
esac

# A partir de aqui, solo walk-pending.txt.

razon="eng/walk-pending.txt NO lo produce CI: ci.yml no lo emite y el archivo vive en este arbol. Es el trinquete del paseo y SOLO PUEDE ENCOGER. Subirlo exige medir por que un control no se puede pulsar y escribir esa razon en su cabecera, como el 2026-08-25. Esta guarda SI deja corregir los comentarios «##» de la cabecera —para eso existe ENG-015—, pero este cambio toca una FILA, y eso no."

# La ruta llega en forma de Windows, que Git Bash no abre con las barras
# invertidas. Si aun asi no resuelve, se cae al archivo del proyecto antes que
# denegar por no encontrarlo: una guarda que se equivoca por no leer es
# indistinguible de una que no corrio.
disk=$(printf '%s' "$f" | tr '\\' '/')
[ -f "$disk" ] || disk="${CLAUDE_PROJECT_DIR:-.}/eng/walk-pending.txt"
[ -f "$disk" ] || deny "$razon"

# Las filas de hoy: ni comentarios ni lineas en blanco.
rows=$(grep -v '^##' "$disk" | grep -v '^[[:space:]]*$')

tiene_fila () {
  # Cierto si alguna linea COMPLETA del fragmento es una fila: no empieza por
  # «##», no esta vacia, y lleva la almohadilla que separa vista de control.
  local texto=$1 linea
  while IFS= read -r linea; do
    case "$linea" in
      '##'*) continue ;;
    esac
    [ -z "${linea//[[:space:]]/}" ] && continue
    case "$linea" in
      *'#'*) return 0 ;;
    esac
  done <<< "$texto"
  return 1
}

corta_fila () {
  # Cierto si el fragmento empieza o termina A MEDIA FILA. Sin esto, un cambio
  # que arrancara en «Surround51» y siguiera hacia los comentarios pasaria: esa
  # primera linea no lleva almohadilla, asi que tiene_fila la da por texto.
  # El minimo de cuatro caracteres evita que un fragmento de uno o dos case con
  # media lista; por debajo de eso decide tiene_fila, y un Edit tan corto falla
  # antes por no ser unico.
  local texto=$1 sonda
  for sonda in "$(printf '%s' "$texto" | head -n1)" "$(printf '%s' "$texto" | tail -n1)"; do
    [ ${#sonda} -lt 4 ] && continue
    case "$sonda" in
      '##'*) continue ;;
    esac
    printf '%s\n' "$rows" | grep -qF -- "$sonda" && return 0
  done
  return 1
}

# Write trae el archivo entero, asi que se compara por efecto: si las filas del
# contenido propuesto son las mismas y en el mismo orden, solo cambian
# comentarios.
if [ "$(printf '%s' "$payload" | jq -r 'if (.tool_input.content|type) == "string" then "si" else "no" end')" = si ]; then
  nuevas=$(printf '%s' "$payload" | jq -r '.tool_input.content' | grep -v '^##' | grep -v '^[[:space:]]*$')
  [ "$nuevas" = "$rows" ] && exit 0
  deny "$razon"
fi

# Edit y MultiEdit traen las dos caras del cambio. Se miran ambas: insertar
# texto dentro de una fila es tan cambio como borrarla. Van en base64 porque una
# de ellas puede llevar saltos de linea dentro.
for trozo in $(printf '%s' "$payload" | jq -r '
    (if (.tool_input.edits | type) == "array" then .tool_input.edits[] else .tool_input end)
    | (.old_string // ""), (.new_string // "")
    | @base64'); do
  s=$(printf '%s' "$trozo" | base64 -d)
  [ -z "$s" ] && continue
  tiene_fila "$s" && deny "$razon"
  corta_fila "$s" && deny "$razon"
done

exit 0
