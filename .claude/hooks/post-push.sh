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
set -u

cmd=$(jq -r '.tool_input.command // empty' | tr -d '\r')
[ -z "$cmd" ] && exit 0

case "$cmd" in
  *"git push"*) ;;
  *) exit 0 ;;
esac

sha=$(git -C "${CLAUDE_PROJECT_DIR:-.}" rev-parse --short HEAD 2>/dev/null || printf '<sha>')

printf 'EL PUSH NO FALLO. Falta el vigia: CI se mira con Monitor y eng/watch-ci.ps1, nunca con un bucle a mano, ni con gh run list repetido, ni esperando a que salga solo (CLAUDE.md).\n' >&2
printf 'Armalo ahora, antes de seguir:\n' >&2
printf '  Monitor(command: "pwsh -NoProfile -File eng/watch-ci.ps1 -Sha %s 2>&1", timeout_ms: 3600000)\n' "$sha" >&2
printf 'Un run tarda 42-50 min y el guion late cada 30, asi que su silencio inicial es normal y NO prueba que este armado. Sin el verde leido, main no avanza.\n' >&2
exit 2
