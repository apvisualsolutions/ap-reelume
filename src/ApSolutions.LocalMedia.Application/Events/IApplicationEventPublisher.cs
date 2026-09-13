// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: LicenseRef-APSolutions

namespace ApSolutions.LocalMedia.Application.Events;

public interface IApplicationEventPublisher
{
    Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default)
        where TEvent : notnull;
}
