#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: GPL-3.0-or-later
#
# PostToolUse de Bash y PowerShell: tras un `git push`, exige armar el Monitor.
#
# Por que existe. El ciclo de CLAUDE.md dice «push a la rama, CI verifica, y solo
# con el verde LEIDO el fast-forward a main», y para mirar CI manda Monitor con
# eng/watch-ci.ps1 y nunca un bucle a mano. Pero eso era una frase, y una frase
# no dispara: el 2026-08-30 el propietario tuvo que pedirlo. Un push sin vigia es
# un verde que nadie lee, y main se queda parado o —peor— avanza a ciegas.
#
# Avisa por stderr saliendo con codigo 2, que es el unico canal que llega al
# agente: un systemMessage no lo ve la persona —medido el 2026-08-29 con el
# propietario delante— ni entra en el contexto del agente.
#
# Vive en un archivo y no dentro de settings.json porque el harness imprime el
# comando ENTERO DOS VECES delante del texto del aviso: en linea costaba 2.712
# caracteres de contexto por aviso.
#
# Suena SIEMPRE que hay push, incluido el fast-forward a main que no dispara el
# flujo. Es a proposito: distinguirlo pedia adivinar la rama de destino, y una
# guarda que se equivoca callando es indistinguible de una que no corrio.
#
# El SHA va ENTERO, no --short. Desde el 2026-09-02 el vigia busca el run por
# commit, y `gh run list --commit` exige los cuarenta caracteres: con un prefijo
# contesta `[]` y sale 0, que se lee igual que «aun no hay run». Medido ese dia
# contra este repositorio. El guion resuelve un prefijo por su cuenta, pero un
# comando que ya llega correcto no depende de que pueda.
#
# Y NO emite -Branch, a proposito. La rama local de un worktree no es la rama a
# la que se empuja —ese fue el falso negativo del 2026-09-02, con el vigia
# afirmando que el push no habia disparado el flujo mientras el run corria— y
# sacar la rama de destino de la linea del push pedia adivinar el refspec, que
# es lo que el parrafo de arriba ya decidio no hacer. Un run pertenece al
# commit, asi que se pregunta por el commit y no queda nada que adivinar.
set -u

cmd=$(jq -r '.tool_input.command // empty' | tr -d '\r')
[ -z "$cmd" ] && exit 0

# `git push` tiene que ser un COMANDO, no texto. La primera version buscaba la
# cadena suelta y sono en su propio commit: el mensaje, escrito con un heredoc,
# hablaba de «after any git push». Un aviso que suena cuando no toca ensena a
# ignorarlo, que es peor que no avisar.
#
# Asi que primero se tiran los heredocs —todo lo que va entre <<DELIM y la linea
# DELIM, que es donde viven los mensajes de commit— y luego se exige que la
# cadena este en posicion de comando: inicio de linea, o detras de ; & | ( o &&.
#
# El [[:space:]]* detras de << llego el 2026-09-01 y NO es cosmetico: sin el, el
# regex solo reconocia el heredoc pegado, y la forma con espacio —que es la que
# usa este repositorio en cada commit— pasaba como si no fuera un heredoc.
# Reproducido por tuberia con un documento que citaba una orden de push dentro
# de uno: sono. El defecto que el parrafo de arriba dice haber corregido seguia
# aqui, escondido en un espacio.
stripped=$(printf '%s\n' "$cmd" | awk '
  /^[[:space:]]*[A-Za-z_][A-Za-z0-9_]*[[:space:]]*$/ && skip && $1 == delim { skip = 0; next }
  skip { next }
  {
    if (match($0, /<<-?[[:space:]]*[\047"]?[A-Za-z_][A-Za-z0-9_]*[\047"]?/)) {
      d = substr($0, RSTART, RLENGTH)
      gsub(/^<<-?[[:space:]]*[\047"]?|[\047"]?$/, "", d)
      delim = d
      skip = 1
    }
    print
  }')

printf '%s\n' "$stripped" | grep -Eq '(^|[;&|(]|&&|\|\|)[[:space:]]*git[[:space:]]+push([[:space:]]|$)' || exit 0

sha=$(git -C "${CLAUDE_PROJECT_DIR:-.}" rev-parse HEAD 2>/dev/null || printf '<sha>')

printf 'EL PUSH NO FALLO. Falta el vigia: CI se mira con Monitor y eng/watch-ci.ps1, nunca con un bucle a mano, ni con gh run list repetido, ni esperando a que salga solo (CLAUDE.md).\n' >&2
printf 'Armalo ahora, antes de seguir:\n' >&2
printf '  Monitor(command: "pwsh -NoProfile -File eng/watch-ci.ps1 -Sha %s 2>&1", timeout_ms: 3600000)\n' "$sha" >&2
printf 'Un run tarda 42-53 min y el guion late cada 30, asi que su silencio inicial es normal y NO prueba que este armado. Sin el verde leido, main no avanza.\n' >&2
exit 2
