# Dónde retomar — 2026-09-20 (cierre)

> Se **sobrescribe** en cada cierre y no pasa de 80 líneas ni de 6 KB por idioma; lo mide
> `eng/check-handoff.ps1`. La historia hasta el 2026-09-19 está congelada en
> [NEXT-SESSION-HISTORY.es.md](NEXT-SESSION-HISTORY.es.md). **El árbol manda sobre este documento**:
> `git log --oneline -1`, `git log --oneline -1 main` y `gh run list --limit 3` antes que nada.

## Estado

`main` quedó en el commit de `ENG-042`, con su CI leído en verde, y ahí se queda.

**El commit de `ENG-011` dio ROJO y no se arregló aquí**, que es la regla del paso 0. La causa está
medida y es de dos minutos: las **diez suites pasaron** —891, 406, 55, 684, 1.474, 155, 201, 202,
118, 17— y lo único que falló fue la puerta de cobertura con «1 improved»:
`CompositionRoot.cs now reaches 90/65`, porque el cableado nuevo le añadió una rama cubierta y su
suelo dice 90/64. **Lo primero de mañana**: `gh run download 35515823303 -n coverage-debt`, copiar el
artefacto sobre `eng/coverage-debt.txt` normalizando CRLF a LF, comprobar que la única diferencia es
esa fila, y empujar. Con ese verde, `main` avanza a `ENG-011`.

## Lo que se hizo

· **`ENG-044` cerrada, y con ella `ENG-010`**: la vigilancia de carpetas se enciende, se gobierna
  desde Ajustes y surte efecto sin reiniciar. `LIB-003` vuelve a `VERIFIED` con su bloqueo retirado.
· **`ENG-042` cerrada**: los tres documentos que viajan con el paquete dicen `plugin`.
· **`ENG-011` cerrada**: la velocidad de reproducción se guarda y se aplica al abrir.
· El trinquete de deuda bajó a **185**: `FallbackScanScheduler.cs` llegó a 100/100 y salió.

## Las trampas medidas

· **Dos defectos del mismo tipo el mismo día, y es el de la casa**: la vigilancia y la velocidad
  estaban construidas, persistidas y resueltas, y **nadie las llamaba**. El `grep` que lo destapa es
  el de quién LEE el valor resuelto, con un control positivo al lado — `resolved.Picture` daba uno y
  `resolved.SpeedMultiplier` daba cero.
· **El barrido de respaldo tampoco corría para nadie**, por la misma bandera que la vigilancia. Eso
  dejaba sin recuperación a USB y NAS —la otra mitad de `LIB-003`— y sin reintento a un vigilante
  caído. No estaba en ninguna fila: salió al terminar `ENG-044`.
· **Dos guardas ciegas**, encontradas mutando: una prueba del `Dispose` verde sin el código que decía
  comprobar, y una rama que nada podía provocar. Y **una aserción textual** —`RootWatchWiringTests`
  buscando una constante en el fuente— estuvo verde semanas mientras lo que nombraba no corría.
· **Un cero se mide dos veces, y hoy fallé tres**: leí el código de salida de `tail` tras una
  tubería y acusé a un guardián de IT; medí dos estados como uno porque otra sesión cambió la máquina
  entre mis lecturas; y di por ausente un cajón buscándolo en los ficheros que por diseño dan cero.

## Lo primero de la sesión siguiente

· **Leer el CI de `ENG-011` y avanzar `main` si es verde.** Es lo único pendiente de publicar.
· **Repetir el marcador de cierre y el doctor del sistema común** en cuanto IT publique la **0.10.1**
  — ver abajo.

## Lo que espera al propietario

· **El sistema común está roto sobre el NAS y este cierre se hizo SIN su marca.** La herramienta que
  pone la marca sale 2 porque sus guiones resuelven rutas con `(Resolve-Path X).Path`, que sobre una
  unidad de red devuelve el PSPath con prefijo de proveedor y git rechaza con 128; con
  `.ProviderPath` sale 0.
  Aislado con control positivo y negativo, y reproducido por IT en su lado: afecta a **todos** los
  proyectos desde la migración. Su `grep` dio **33 usos del patrón, 9 en producción**. Arreglo
  encargado como **0.10.1**; avisarán al publicar. **Consecuencia de este cierre**: las dos puertas
  del plugin no miraron el acta y el acta no pudo calcularse.
· **El cajón del segundo cerebro existe pero NO llegó a esta sesión**, así que no se escribió punto
  de control; IT lo reengancha. **Y ojo con cómo se comprueba**: el conector vive en la configuración
  de usuario por proyecto, no en `.mcp.json` ni en el ajuste local —decisión de IT tras publicar un
  nombre interno en un repositorio público—, así que esos dos dan cero **por diseño**. Se pregunta si
  la sesión tiene las herramientas del cajón, y sólo eso; su nombre no se escribe aquí, por lo mismo
  que `ENG-043`. Ya está reenganchado a la ruta nueva: compruébalo y escribe el punto de control de
  la tanda anterior. Aquí se dio un cero por una ausencia y era el patrón mirando donde no era.
· **`ENG-015` tiene un hallazgo que la bloquea**: el hook que protege `eng/walk-pending.txt` deniega
  toda escritura sin distinguir entre añadir una fila y corregir un comentario caducado, así que sus
  tres cifras desfasadas **no se pueden arreglar** con las herramientas de edición. Y `CLAUDE.md`
  también se contradice ahí: hay que reconstruir la secuencia real antes de reescribir nada.
· **`ENG-002`** (una sesión con el Narrador) y **`ENG-005`** (abrir una ventana cuando no trabaje).

El filtro de privacidad queda en 1 hallazgo, y es **falso positivo aceptado por IT**: la IP pública
de VideoLAN citada en el commit de `ENG-042` al documentar un fallo de red. No se reescribe historial
(`ENG-032`). Convención mientras lo afinan: citar a un tercero por su nombre de host, nunca por su IP.

## Lo pendiente no está aquí

Se lee con `pwsh -NoProfile -File eng/list-pending.ps1` y en [TAREAS.md](TAREAS.md). Hoy: 24 abiertas
de 75.
