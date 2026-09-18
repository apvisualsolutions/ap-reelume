#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# PreToolUse de Bash y PowerShell: durante un cierre, deniega el push de la rama
# si lleva algo que no sea el relevo.
#
# Por que existe. El 2026-09-18 el propietario mando cerrar y el agente siguio
# arreglando lo que salia —un rojo de cobertura, las puertas que encontro el
# auditor— con tres pushes mas, cada uno con su CI de unos cuarenta minutos y con
# el contexto ya lleno, que es justo por lo que se manda cerrar. Era la segunda
# vez que se quejaba de lo mismo, y una regla olvidada dos veces no es una nota:
# es un hook que falta.
#
# Como sabe que hay un cierre. /cierre, en su paso 0, crea la marca
# «ap-closing» dentro del directorio de git —no en el arbol, para que ningun
# `git add -A` pueda llevarsela en un commit— y la retira al terminar. Sin marca
# este hook no hace nada.
#
# Que deniega, y por que no mas. Solo el push cuyo rango de commits sin subir
# toque algo fuera de docs/ o de un .md de la raiz: codigo, pruebas, eng/, el
# flujo. El relevo es documentacion y tiene que poder salir, y el fast-forward de
# main (refspec que termina en «main») solo lleva lo que CI ya verifico. Un
# guardian que salta de mas acaba desactivado, asi que no bloquea todo push.
#
# La via de escape esta en el propio mensaje: si el propietario pide otra cosa,
# se borra la marca.
set -u

. "$(dirname "$0")/lib-git-push.sh"

cmd=$(jq -r '.tool_input.command // empty' | tr -d '\r')
[ -z "$cmd" ] && exit 0

repo="${CLAUDE_PROJECT_DIR:-.}"
git_dir=$(git -C "$repo" rev-parse --absolute-git-dir 2>/dev/null) || exit 0
marker="$git_dir/ap-closing"
[ -f "$marker" ] || exit 0

stripped=$(strip_heredocs "$cmd")
is_git_push "$stripped" || exit 0

refspec=$(push_refspec "$stripped")
case "$refspec" in
  main|*:main|*:refs/heads/main) exit 0 ;;
esac

upstream=$(git -C "$repo" rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>/dev/null) \
  || upstream="origin/$(git -C "$repo" rev-parse --abbrev-ref HEAD 2>/dev/null)"
changed=$(git -C "$repo" log --name-only --format= "$upstream..HEAD" 2>/dev/null | sort -u)

outside=$(printf '%s\n' "$changed" | grep -v '^$' | grep -Ev '^docs/|^[^/]+\.md$' | head -5)
[ -z "$outside" ] && exit 0

list=$(printf '%s' "$outside" | tr '\n' ',' | sed 's/,$//; s/,/, /g')
reason="CIERRE EN CURSO: no se sube codigo ni pruebas; solo el relevo. Este push lleva cambios fuera de docs/ ($list). Lo que falte va al prompt de la sesion siguiente, que lo hara con el contexto limpio. Si el propietario ha pedido otra cosa, borra la marca: rm \"$marker\"."
jq -cn --arg r "$reason" '{hookSpecificOutput:{hookEventName:"PreToolUse",permissionDecision:"deny",permissionDecisionReason:$r}}'
exit 0
