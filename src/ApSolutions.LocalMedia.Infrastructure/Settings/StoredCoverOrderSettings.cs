// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

using ApSolutions.LocalMedia.Application.Metadata;
using ApSolutions.LocalMedia.Application.Settings;
using ApSolutions.LocalMedia.Domain.Metadata;

namespace ApSolutions.LocalMedia.Infrastructure.Settings;

/// <summary>
/// Remembers which origin a cover is looked for in first, between sessions. An order that does not
/// name every origin is repaired on the way in and on the way out, so a hand-edited file cannot
/// leave a cover that exists impossible to draw.
/// </summary>
/// <remarks>
/// It is stored as the text <see cref="CoverOrderPolicy.Format"/> writes rather than as a list of
/// enum values: the store would serialize a list by name just as well, but reading it back would
/// then need its own repair beside the one <see cref="CoverOrderPolicy.TryParse"/> already does —
/// two places deciding the same thing is how they come to disagree.
/// </remarks>
public sealed class StoredCoverOrderSettings : ICoverOrderSettings
{
    private const string OrderKey = "covers.order";

    private readonly ISettingsStore _store;

    public StoredCoverOrderSettings(ISettingsStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public IReadOnlyList<CoverOrigin> Current =>
        CoverOrderPolicy.TryParse(_store.Read<string?>(OrderKey), out var order)
            ? order
            : CoverOrderPolicy.Default;

    public void Save(IReadOnlyList<CoverOrigin> order)
    {
        ArgumentNullException.ThrowIfNull(order);
        _store.Write(OrderKey, CoverOrderPolicy.Format(order));
    }
}
