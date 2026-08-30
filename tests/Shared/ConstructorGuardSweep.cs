// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.CodeDom.Compiler;
using System.Reflection;
using System.Runtime.CompilerServices;

using NSubstitute;

namespace ApSolutions.LocalMedia.TestSupport;

/// <summary>
/// TST-001: the null-guard sweep. Ninety of the two hundred and five files short of the coverage
/// bar were short of it for one repeated reason — a constructor writing
/// <c>?? throw new ArgumentNullException</c> that no test ever handed a null. The throw is a branch,
/// an untaken branch is half a branch pair, and eight Application files sat at exactly 100/50
/// because of it. Chasing them one file at a time would be ninety test methods saying the same
/// sentence, so this sweep says it once and points it at a whole assembly.
/// </summary>
/// <remarks>
/// <para>
/// For each constructor parameter that could hold a null, the sweep builds the type with a stand-in
/// in every other position and a null in that one, and requires <see cref="ArgumentNullException"/>
/// naming that parameter. Anything else — a successful build, or a different exception — is
/// reported, because a guard that throws the wrong thing is not a guard.
/// </para>
/// <para>
/// Three exclusions, all structural rather than a list, because a list is a thing to maintain:
/// records, which are this repository's data carriers and legitimately validate nothing
/// (<c>InstalledRelease</c>, <c>StagedUpdate</c> and companions were 190 of the 319 reference
/// parameters the first measurement found); exceptions, whose message and inner exception are
/// nullable by .NET convention; and types the compiler wrote, such as the
/// <c>JsonSerializerContext</c> the source generator emits, which are not this repository's code to
/// hold to anything.
/// </para>
/// </remarks>
public static class ConstructorGuardSweep
{
    /// <summary>What one sweep of an assembly saw.</summary>
    /// <param name="Guarded">Parameters that refused a null by name. The sweep's own floor.</param>
    /// <param name="Unguarded">Types whose constructor accepted a null in at least one position.</param>
    /// <param name="Unbuildable">Parameters no stand-in could be made for, or whose constructor
    /// threw something other than <see cref="ArgumentNullException"/>. Never expected, and reported
    /// rather than swallowed: a sweep that quietly skips what it cannot build is a sweep that
    /// measures less every time the code changes.</param>
    public sealed record Sweep(
        IReadOnlyList<string> Guarded,
        IReadOnlyList<string> Unguarded,
        IReadOnlyList<string> Unbuildable);

    /// <summary>Hands every constructor in <paramref name="assembly"/> a null, one position at a time.</summary>
    public static Sweep Run(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var nullability = new NullabilityInfoContext();
        var guarded = new List<string>();
        var unguarded = new SortedSet<string>(StringComparer.Ordinal);
        var unbuildable = new List<string>();

        foreach (var type in assembly.GetTypes().OrderBy(candidate => candidate.FullName, StringComparer.Ordinal))
        {
            if (!IsGuardableType(type))
            {
                continue;
            }

            foreach (var constructor in type.GetConstructors())
            {
                var parameters = constructor.GetParameters();
                for (var index = 0; index < parameters.Length; index++)
                {
                    var parameter = parameters[index];
                    if (parameter.ParameterType.IsValueType)
                    {
                        continue;
                    }

                    // A parameter declared nullable asks for no guard, and demanding one would be
                    // demanding a throw the signature promises will not happen.
                    if (nullability.Create(parameter).WriteState == NullabilityState.Nullable)
                    {
                        continue;
                    }

                    var label = $"{type.FullName}.ctor({parameter.Name})";
                    object?[] arguments;
                    try
                    {
                        arguments = parameters
                            .Select((other, position) => position == index ? null : StandInFor(other.ParameterType))
                            .ToArray();
                    }
                    catch (Exception error)
                    {
                        unbuildable.Add($"{label}: no stand-in for a sibling parameter -- {error.GetType().Name}: {error.Message}");
                        continue;
                    }

                    try
                    {
                        var built = constructor.Invoke(arguments);
                        (built as IDisposable)?.Dispose();
                        unguarded.Add(type.FullName ?? type.Name);
                    }
                    catch (TargetInvocationException wrapped)
                        when (wrapped.InnerException is ArgumentNullException refusal
                            && refusal.ParamName == parameter.Name)
                    {
                        guarded.Add(label);
                    }
                    catch (Exception error)
                    {
                        var actual = error.InnerException ?? error;
                        unbuildable.Add($"{label}: expected ArgumentNullException naming '{parameter.Name}' but got {actual.GetType().Name}: {actual.Message}");
                    }
                }
            }
        }

        return new Sweep(guarded, [.. unguarded], unbuildable);
    }

    private static bool IsGuardableType(Type type)
    {
        if (!type.IsClass || type.IsAbstract || type.IsNested || type.IsSubclassOf(typeof(MulticastDelegate)))
        {
            return false;
        }

        if (type.GetCustomAttribute<CompilerGeneratedAttribute>() is not null
            || type.GetCustomAttribute<GeneratedCodeAttribute>() is not null)
        {
            return false;
        }

        if (typeof(Exception).IsAssignableFrom(type))
        {
            return false;
        }

        // A record is recognised by the clone method every record declaration gets, which is the
        // only mark that survives to metadata.
        return type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) is null;
    }

    /// <summary>
    /// Something non-null to hold the other positions. It is never used — the constructor under test
    /// throws before it can be — so it only has to exist and be of the right type. Uninitialised
    /// objects cover every concrete class without running a line of its constructor, which keeps the
    /// sweep from waking a type up as a side effect of measuring another one.
    /// </summary>
    private static object StandInFor(Type type)
    {
        if (type == typeof(string))
        {
            return "stand-in";
        }

        if (type.IsValueType)
        {
            return Activator.CreateInstance(type)!;
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType()!, 0);
        }

        if (type.IsInterface || type.IsAbstract || typeof(Delegate).IsAssignableFrom(type))
        {
            return Substitute.For([type], []);
        }

        return RuntimeHelpers.GetUninitializedObject(type);
    }
}
