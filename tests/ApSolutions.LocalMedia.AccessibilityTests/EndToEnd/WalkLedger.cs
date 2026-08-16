// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

using System.Text.Json;
using ApSolutions.LocalMedia.TestSupport;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xunit;

namespace ApSolutions.LocalMedia.AccessibilityTests.EndToEnd;

/// <summary>
/// What the walk pressed, written down as it presses it.
/// </summary>
/// <remarks>
/// <para>
/// The record is made <b>in execution</b> rather than by reading the walk's source for
/// <c>Press(...)</c> calls, and that is the whole design. Three suites in this repository have
/// already broken by reading source as text — <c>CompositionSourceText</c> and
/// <c>CompositionGraph</c> both went stale the moment code moved — and a text count would also be a
/// lie in a way those two were not: it would count a call that was written, not a control that was
/// reached. A press that scrolled to nothing, landed on a disabled button or was swallowed by an
/// overlay still reads as a call in the file.
/// </para>
/// <para>
/// A control's identity is its <b>view plus its anchor</b>, and the view half is taken from the
/// control that was actually clicked — the nearest <c>UserControl</c> above it — so the same
/// resource key used on two different cards counts as the two controls it is.
/// <c>DetailsTrailerLinkAction</c> is exactly that: one key, one on the film card and one on the
/// series card, backed by different view models, and pressing one proves nothing about the other.
/// </para>
/// </remarks>
public static class WalkLedger
{
    private const string ResultsVariable = "APSOLUTIONS_LOCALMEDIA_WALK_RESULTS";

    private static readonly Lock Gate = new();
    private static readonly SortedSet<string> PressedControls = new(StringComparer.Ordinal);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>Where the gate looks for the report, honouring the run's own results directory.</summary>
    public static string Directory =>
        Environment.GetEnvironmentVariable(ResultsVariable) is { Length: > 0 } configured
            ? configured
            : Path.Combine(RepositoryLayout.Root, "artifacts", "walk");

    /// <summary>
    /// The view a control is declared in, read <b>before</b> it is pressed.
    /// </summary>
    /// <remarks>
    /// Before, because a control may not survive its own press. The review inbox's Confirm button
    /// lives inside the offer it decides, so confirming removes the item and with it the button's
    /// place in the tree: asked afterwards, it sat under no view at all. What it was is what it was
    /// when it was pressed.
    /// </remarks>
    public static UserControl? ViewOf(Control control)
    {
        ArgumentNullException.ThrowIfNull(control);
        return control.GetSelfAndVisualAncestors().OfType<UserControl>().FirstOrDefault();
    }

    /// <summary>
    /// Notes one control as pressed, and flushes. Flushing on every press rather than at the end is
    /// deliberate: a suite that fails halfway must still leave behind what it did prove.
    /// </summary>
    public static void Record(UserControl? view, string anchor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anchor);
        Assert.True(
            view is not null,
            $"The control pressed for {anchor} sits under no view, so there is nothing to record it "
                + "against. Every command control in this application is declared inside a UserControl.");

        lock (Gate)
        {
            _ = PressedControls.Add($"{view!.GetType().Name}#{anchor}");
            Flush();
        }
    }

    private static void Flush()
    {
        System.IO.Directory.CreateDirectory(Directory);
        File.WriteAllText(
            Path.Combine(Directory, "pressed.json"),
            JsonSerializer.Serialize(PressedControls, SerializerOptions));
    }
}
