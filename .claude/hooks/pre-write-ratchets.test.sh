#!/usr/bin/env bash
# SPDX-FileCopyrightText: 2026 AP Solutions
# SPDX-License-Identifier: LicenseRef-APSolutions
#
# Bateria por tuberia de pre-write-ratchets.sh (ENG-015).
#
# Vive en el arbol y no en un directorio temporal a proposito: un hook que calla
# NO deja rastro en el registro de la sesion —solo se anota cuando produce
# salida—, asi que un silencio observado en la aplicacion no prueba que la
# guarda corriera. Lo unico que lo prueba es ejecutar el comando literal por
# tuberia con un caso que debe sonar al lado del que debe callar, y eso hay que
# poder repetirlo el dia que alguien toque el hook.
#
#   bash .claude/hooks/pre-write-ratchets.test.sh
#
# La variable HOOK apunta a otro guion para probar un mutante: una version que
# nunca deniegue tiene que romper los siete casos que suenan, y una que cuente
# los comentarios como filas tiene que romper al menos dos de los que callan.
# Sin esa comprobacion la bateria no verifica nada — y el 2026-09-20 el primer
# mutante salio IDENTICO al original porque el sed no caso, de modo que su
# 12 de 12 no media nada. Por eso se compara antes de creerse el resultado.
set -u

raiz=${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}
HOOK=${HOOK:-$raiz/.claude/hooks/pre-write-ratchets.sh}
WP=$raiz/eng/walk-pending.txt
CD=$raiz/eng/coverage-debt.txt
export CLAUDE_PROJECT_DIR=$raiz

ok=0; mal=0

correr () {
  local nombre=$1 esperado=$2 payload=$3 salida veredicto
  salida=$(printf '%s' "$payload" | bash "$HOOK" 2>/dev/null)
  if printf '%s' "$salida" | grep -q '"permissionDecision":"deny"'; then
    veredicto=deny
  else
    veredicto=pass
  fi
  if [ "$veredicto" = "$esperado" ]; then
    printf '  OK    %-50s %s\n' "$nombre" "$veredicto"; ok=$((ok+1))
  else
    printf '  FALLO %-50s esperaba %s y dio %s\n' "$nombre" "$esperado" "$veredicto"; mal=$((mal+1))
  fi
}

edit () { jq -nc --arg f "$1" --arg o "$2" --arg n "$3" \
  '{tool_name:"Edit",tool_input:{file_path:$f,old_string:$o,new_string:$n}}'; }
write () { jq -nc --arg f "$1" --rawfile c "$2" \
  '{tool_name:"Write",tool_input:{file_path:$f,content:$c}}'; }

tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT

# Tres contenidos de mentira para los casos de Write: uno que solo cambia un
# comentario, uno con una fila de mas y uno con una de menos.
sed 's/and it only shrinks/and it only ever shrinks/' "$WP" > "$tmp/solo-comentarios.txt"
cp "$WP" "$tmp/una-mas.txt"; echo 'AudioOutputView#AudioLayoutInventado' >> "$tmp/una-mas.txt"
grep -v 'AudioOutputView#AudioOutputLayoutStereo' "$WP" > "$tmp/una-menos.txt"

echo "== Deben DENEGAR =="
correr "Edit anade una fila nueva"          deny "$(edit "$WP" 'AudioOutputView#AudioLayoutSurround71' 'AudioOutputView#AudioLayoutSurround71
AudioOutputView#AudioLayoutInventado')"
correr "Edit borra una fila"                deny "$(edit "$WP" 'AudioOutputView#AudioOutputLayoutStereo' '')"
correr "Edit cambia el texto de una fila"   deny "$(edit "$WP" 'AudioOutputView#AudioLayoutSurround51' 'AudioOutputView#AudioLayoutSurround52')"
correr "Edit corta a media fila"            deny "$(edit "$WP" 'Surround51
AudioOutputView' 'Surround99
AudioOutputView')"
correr "Write con una fila de mas"          deny "$(write "$WP" "$tmp/una-mas.txt")"
correr "Write con una fila de menos"        deny "$(write "$WP" "$tmp/una-menos.txt")"
correr "Edit sobre coverage-debt.txt"       deny "$(edit "$CD" 'a' 'b')"

echo "== Deben DEJAR PASAR =="
correr "Edit corrige una cifra del comentario" pass "$(edit "$WP" 'ps1 is 23, and it only shrinks.' 'ps1 is 23 today, and it only shrinks.')"
correr "Edit corrige un comentario entero"  pass "$(edit "$WP" '## This file is NOT produced by CI.' '## This file is NOT emitted by CI.')"
correr "MultiEdit, tres comentarios"        pass "$(jq -nc --arg f "$WP" '{tool_name:"MultiEdit",tool_input:{file_path:$f,edits:[
  {old_string:"and it only shrinks",new_string:"and it only ever shrinks"},
  {old_string:"## This file is NOT produced by CI.",new_string:"## This file is NOT emitted by CI."},
  {old_string:"with the measured reason written here.",new_string:"with the measured reason written down here."}]}}')"
correr "Write con los comentarios cambiados" pass "$(write "$WP" "$tmp/solo-comentarios.txt")"
correr "Edit sobre otro fichero cualquiera" pass "$(edit "$raiz/docs/TAREAS.md" 'AudioOutputView#AudioLayoutSurround51' 'x')"

echo
echo "  $ok bien, $mal mal"
[ "$mal" -eq 0 ]
