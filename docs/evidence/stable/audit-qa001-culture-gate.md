# La cultura la vigila el compilador / The compiler watches the culture

Evidencia de **QA-001**: donde el formato es dato y no idioma, la cultura tiene que estar dicha. /
Evidence for **QA-001**: where a format is data rather than language, the culture has to be stated.

Rama / Branch: `codex/ap-reelume-mvp-x64`. Fecha / Date: 2026-08-14.

## La medición previa, que invirtió la tarea / The measurement, which turned the task around

El plan decidió no escribir un regex casero y encender los analizadores; la primera medición debía
contar los avisos **por proyecto**, y ese número decidía si se saldaba de una vez o por capas. Con
`CA1305`, `CA1304` y `CA1310` en `warning`, sobre la solución entera y sin compilación incremental:
/ The plan decided to turn the analyzers on rather than hand-roll a regex, and to count the warnings
per project first. With the three at `warning`, over the whole solution:

```
avisos CA1305 / CA1304 / CA1310      0
advertencias totales                 0
errores                              0
```

**Cero.** No hay nada que corregir: el código ya nombra su cultura donde importa. La tarea no era
saldar deuda, era **poner la puerta** — y la puerta hacía falta igual, porque las tres reglas están
**apagadas por defecto** en `latest-recommended`, así que hasta hoy no vigilaba nada. / Zero. There
was no debt to pay: the task was to put the gate up, and the gate was needed all the same, because
the three rules are off by default and nothing was watching.

## El cero se comprobó antes de creérselo / The zero was checked before being believed

Un cero puede significar «no hay violaciones» o «la regla no llegó a ejecutarse», y las dos se ven
igual desde fuera. Así que se compiló un canario deliberado con una violación de cada una: /

```csharp
public static string Upper(string value) => value.ToUpper();          // CA1304
public static bool Starts(string value) => value.StartsWith("a");     // CA1310
public static string Number(int value) => value.ToString();           // CA1305
```

```
CultureCanary.cs(8,49):  error CA1304  "string.ToUpper()" … Reemplace … por "string.ToUpper(CultureInfo)"
CultureCanary.cs(10,48): error CA1310  "string.StartsWith(string)" … por "string.StartsWith(string, StringComparison)"
CultureCanary.cs(12,47): error CA1305  "int.ToString()" … por "int.ToString(IFormatProvider)"
```

Las tres saltaron, y el canario se retiró. El cero es una medición, no un silencio. / All three
fired, and the canary was removed. The zero is a measurement, not a silence.

## La corrección / The fix

Las tres reglas quedan en `error` en `.editorconfig`. `error` y no `warning` a propósito: con
`TreatWarningsAsErrors` puesto, un aviso ya rompe la compilación, pero la puerta no debe depender de
que esa propiedad siga ahí. / The three rules are `error` in `.editorconfig` — `error` rather than
`warning` so the gate does not depend on `TreatWarningsAsErrors` staying put.

El criterio para cuando salte —invariante para lo que se guarda, se compara o viaja; cultura de la
interfaz para lo que lee una persona— queda escrito en el plan y no hay ningún caso todavía al que
aplicarlo. / The criterion for when it fires is written in the plan, and there is no case to apply it
to yet.

## Verde / Green

| Puerta / Gate | Resultado / Result |
|---|---|
| `dotnet build -warnaserror` (solución entera, sin incremental) | 0 avisos / 0 errores |
| `dotnet format --verify-no-changes --severity warn` | limpio / clean |
| `eng/verify.ps1` completo / full | verde / green |
