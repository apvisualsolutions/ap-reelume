# La herramienta que firma no compilaba / The tool that signs did not compile

Evidencia de un rojo que **habría aparecido en la primera publicación**, encontrado el 2026-08-14
desde fuera de este repositorio. / Evidence for a red that **would have surfaced at the first
publication**, found on 2026-08-14 from outside this repository.

Rama / Branch: `codex/ap-reelume-mvp-x64`.

## El rojo / The red

```
> dotnet build eng/tools/ReleaseSigning -c Release
eng\tools\ReleaseSigning\Program.cs(1,1): error IDE0073: Falta un encabezado obligatorio en un
archivo de código fuente.
    1 Errores
```

`release.yml` ejecuta esa herramienta en el paso que **verifica la firma** de `SHA256SUMS.txt`:

```yaml
dotnet run --project eng/tools/ReleaseSigning -- verify $sums "$sums.minisig" eng/release-signing.pub
```

`dotnet run` compila antes de ejecutar. Con el encabezado ausente y `IDE0073` en severidad de error,
**la publicación habría fallado ahí** — después de construir los artefactos y de firmarlos, en el
paso que comprueba que la firma sirve. / `dotnet run` builds before it runs, so the publication would
have failed at the step that checks the signature is good.

## Por qué ninguna puerta lo vio / Why no gate saw it

Porque **el proyecto no estaba en la solución**, y todas las puertas construyen la solución:

```
> rg -c "ReleaseSigning" ApSolutions.LocalMedia.sln
(sin coincidencias / no matches)
```

`dotnet build ApSolutions.LocalMedia.sln -warnaserror` no puede fallar por un archivo que no compila.
Es el defecto característico de esta casa trasladado a la construcción: **existe, algo depende de
ello, y ninguna puerta lo alcanza**. Lo primero que iba a compilar ese archivo era una publicación
real. / The gates build the solution, and the solution did not contain the project: this repository's
characteristic defect, moved into the build.

## La corrección, y la regla / The fix, and the rule

Dos mitades, y la segunda es la que importa: / Two halves, and the second is the one that matters:

1. El encabezado SPDX en `Program.cs`, que es la corrección mínima.
2. **El proyecto entra en la solución**, con lo que todas las puertas que ya existen pasan a cubrirlo
   sin escribir ninguna nueva, y una regla —`SolutionCoverageTests`— falla en cuanto aparezca otro
   `.csproj` fuera de ella. La línea que traza es la misma que ya usan las puertas de documentación:
   lo que empieza por un punto en la raíz es del entorno, no del proyecto.

La regla se estrenó cazando a su propia prueba: `SolutionCoverageTests` nombraba el archivo de
solución para leerlo, y la regla del ancla de `ARQ-012` la señaló por ello. Tenía razón — el nombre
del ancla vive en un sitio—, así que la capa compartida expone ahora `SolutionPath` para quien lo
lee **como documento** en vez de como ancla. / The new rule was caught by an older one on its first
run, and the older one was right.

## Cómo apareció / How it surfaced

No lo encontró una puerta de este repositorio: lo encontró **otra sesión** que fue a comprobar la
custodia de la clave de firma y necesitó ejecutar la herramienta. Tuvo que desactivar el análisis de
estilo para poder correrla, y eso fue el síntoma. Vale la pena escribirlo: la comprobación que este
proyecto tenía pendiente desde hacía días —restaurar el respaldo y firmar con él— destapó de rebote
un defecto que ninguna prueba iba a ver. / A gate did not find this: another session did, while
checking the signing key's custody, and had to switch off style analysis to run the tool.

## Verde / Green

| Puerta / Gate | Resultado / Result |
|---|---|
| `dotnet build eng/tools/ReleaseSigning -c Release` | 0 avisos / 0 errores |
| `SolutionCoverageTests` | 1 de 1 / of 1 |
| `ApSolutions.LocalMedia.ArchitectureTests` | 26 de 26 / of 26 |
| `dotnet build …sln -warnaserror` (ya con la herramienta dentro / tool now included) | 0 / 0 |
| `eng/verify.ps1` completo / full | verde / green |
