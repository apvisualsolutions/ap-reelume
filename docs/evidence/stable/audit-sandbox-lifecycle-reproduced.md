# El ciclo de vida de Windows, reproducible desde el repositorio / The Windows lifecycle, reproducible from the repository

La medición caducada del `DES-001` vuelve a estar vigente, y esta vez **el repositorio sabe
producirla**: las fases que Windows posee dejan de ser manuales. / The expired lifecycle measurement
is current again, and this time the repository knows how to produce it.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## Lo que estaba caducado, y lo que no / What had expired, and what had not

| Informe / Report | Huella archivada / Archived digest | Estado / State |
|---|---|---|
| `docs/evidence/mvp/windows-lifecycle.json` | `402ae30c…` | **Caducado** — el manifiesto es `5e341b5f…` |
| `docs/evidence/stable/updater-handover.json` | `5e341b5f…` | Vigente / Current |

`verify-package.ps1` acepta el informe **sólo** mientras su `version` y su `manifestSha256` describan
el paquete que se está verificando, así que las cinco fases nativas volvían a `Blocked` en cada
ejecución con este aviso: / The gate accepts the report only while it describes this package, so the
five native phases went back to blocked on every run:

```
WARNING: The archived Windows lifecycle report describes a different package; the native phases stay blocked.
```

**El informe archivado ya traía las nueve fases.** Lo que faltaba no era la medición: era que el
**guion versionado** supiera hacerla. `sandbox-handover.ps1` instalaba y lanzaba, y las cuatro fases
del ciclo seguían siendo «manuales» según el propio `README-sandbox.md` — que es exactamente la
condición que hacía depender la medición de que alguien se acordara. / The archived report already
had all nine phases; what was missing was the versioned script being able to produce them.

## Lo que se añadió / What was added

Al guion que corre **dentro** del sandbox:

- `file-association` — recorre las extensiones que **el manifiesto instalado declara**, nunca una
  lista escrita por segunda vez, y busca la entrada «Abrir con» donde Windows la pone.
- `windows-upgrade` — instala el paquete de la versión siguiente y mide la base **a los dos lados**,
  porque una biblioteca sustituida por otra vacía del mismo nombre también existe.
- `windows-downgrade-refused` — reinstala el anterior y guarda la negativa **entera**: una negativa
  que un día deje de ser `0x80073D06` es otra negativa, y el informe debe poder decirlo.
- `windows-repair` — vuelve a registrar desde el manifiesto que **Windows tiene**, no desde la
  carpeta compartida; reinstalar desde el recurso compartido mediría el recurso compartido.
- `windows-uninstall` — pregunta por la carpeta de datos **antes y después**, porque «la carpeta está
  ahí» no dice nada si no estaba antes.
- Y `windows-launch` comprueba ahora que la base **no** acabó en la carpeta virtualizada del paquete:
  quien busque su biblioteca en la ruta documentada tiene que encontrarla allí.

Al guion del **anfitrión**: el paquete de la versión siguiente se obtiene **resellando** el actual con
la versión subida —`0.1.0.0` → `0.2.0.0`, misma identidad, mismo editor— en vez de construir la
aplicación dos veces. Lo que Windows lee para decidir si una instalación es una actualización es la
versión del manifiesto y nada más, así que reconstruir el payload variaría algo que la medición no
trata. / The next-version package is resealed rather than rebuilt, because the manifest version is
the whole of what Windows reads.

**Una sola ejecución escribe los dos informes.** Un segundo ciclo instalaría el paquete dos veces
para medir una instalación, y el Windows del segundo ya no sería el limpio que vio el primero. / One
run writes both reports; a second cycle's Windows would no longer be the clean one.

## El resultado / The result

```
windows lifecycle: adopted the archived Sandbox run for 0.1.0.
```

Las doce fases de `lifecycle.json`, siete sustitutas y cinco nativas, en verde:

| Fase / Phase | Lo medido / What was measured |
|---|---|
| `windows-install` | `APSolutions.LocalMedia 0.1.0.0` registrado |
| `file-association` | **8 de 8** contenedores con entrada «Abrir con» |
| `windows-launch` | Base en la ruta documentada, **no** en la virtualizada |
| `windows-upgrade` | `0.1.0.0` → `0.2.0.0`, la base sobrevive: **372 736 bytes antes y después** |
| `windows-downgrade-refused` | `0x80073D06`, y Windows conserva `0.2.0.0` |
| `windows-repair` | Vuelto a registrar `0.2.0.0` desde el paquete que Windows tiene |
| `windows-uninstall` | Paquete fuera; **la biblioteca sigue ahí** |

`PackagingTests`: 152 pruebas en verde. `MsixLifecycleTests` exige ahora `Passed` en las cinco
nativas, porque el entorno del informe declara máquina limpia, elevación y firma — la sustitución
deja de estar permitida en cuanto puede ejecutarse de verdad.

## Una alarma falsa que conviene no repetir / A false alarm worth not repeating

El campo de la negativa se leyó como `versi�n` y pareció evidencia estropeada — el defecto que este
mismo guion ya había tenido una vez con un título de ventana. **Los bytes decían otra cosa**: / The
refusal field read as mojibake and looked like corrupted evidence; the bytes said otherwise:

```
b'ya hay instalada una versi\xc3\xb3n superior de este paquete.'
```

`\xc3\xb3` es `ó` en UTF-8 **correcto**. La corrupción estaba en la consola que lo imprimió, no en el
archivo. Una corrección «obvia» habría cambiado un guion que no tenía nada que corregir. / The
corruption was in the console that printed it, not in the file.

## Cómo se reproduce / How to reproduce

```powershell
pwsh ./eng/package-x64.ps1
pwsh ./eng/run-sandbox-handover.ps1 -SandboxTimeoutSeconds 1500
```

El plazo se subió de 900 s porque el ciclo hace ahora seis operaciones de despliegue sobre un paquete
de 102 MB en vez de una. El certificado desechable se crea en el almacén del usuario y **se retira al
terminar**; el artefacto que se publica sigue sin firmar. / The deadline was raised because the cycle
now performs six deployment operations instead of one.
