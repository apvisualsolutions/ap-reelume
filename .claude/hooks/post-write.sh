#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later
#
# PostToolUse de Write, Edit y MultiEdit.
#
# Avisa por stderr saliendo con codigo 2, que es el unico canal que llega al
# agente que esta escribiendo el archivo: un systemMessage no lo ve la persona
# —medido el 2026-08-29 con el propietario delante— ni entra en el contexto del
# agente, asi que emitia para nadie.
#
# Vive en un archivo y no dentro de settings.json porque el harness imprime el
# comando ENTERO DOS VECES delante del texto del aviso: en linea costaba 2.712
# caracteres de contexto por aviso.
#
# No dispara escribiendo por Bash. Sigue siendo un adelanto de aviso; las
# puertas son dotnet format con IDE0073 y eng/verify-docs.ps1.
set -u

f=$(jq -r '.tool_input.file_path // empty' | tr -d '\r')
[ -z "$f" ] && exit 0

# Cierto si el archivo difiere de HEAD, y tambien si git no lo sigue: un archivo
# sin seguir es invisible para git diff HEAD, y callar sobre un documento recien
# creado es justo el caso que hay que avisar.
changed () {
  git ls-files --error-unmatch "$1" >/dev/null 2>&1 || return 0
  git diff --quiet HEAD -- "$1" >/dev/null 2>&1 && return 1 || return 0
}

case "$f" in
  *.cs|*.axaml)
    grep -q 'SPDX-License-Identifier: GPL-3.0-or-later' "$f" 2>/dev/null && exit 0
    printf 'Regla 1: LA ESCRITURA NO FALLO, el archivo esta en disco. Le falta la cabecera SPDX-License-Identifier: GPL-3.0-or-later, que exige IDE0073 (CLAUDE.md). No reintentes la misma escritura: anade la cabecera y escribelo otra vez.\n' >&2
    exit 2
    ;;
  *docs*.es.md)
    en="${f%.es.md}.en.md"
    if [ ! -f "$en" ]; then
      printf 'Regla 4: LA ESCRITURA NO FALLO, el archivo esta en disco. No tiene pareja .en.md y eng/verify-docs.ps1 la exige. No reintentes la misma escritura: crea la pareja en ingles.\n' >&2
      exit 2
    fi
    if git rev-parse --verify HEAD >/dev/null 2>&1 && changed "$f" && ! changed "$en"; then
      printf 'Regla 4: LA ESCRITURA NO FALLO, el archivo esta en disco. Su pareja .en.md sigue como en HEAD, y los dos idiomas van juntos. No reintentes la misma escritura: actualiza el .en.md.\n' >&2
      exit 2
    fi
    ;;
esac
exit 0
