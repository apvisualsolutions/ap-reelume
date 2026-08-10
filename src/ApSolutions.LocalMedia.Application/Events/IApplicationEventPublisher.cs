// SPDX-FileCopyrightText: 2026 AP Solutions
// SPDX-License-Identifier: GPL-3.0-or-later

namespace ApSolutions.LocalMedia.Application.Events;

public interface IApplicationEventPublisher
{
    Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default)
        where TEvent : notnull;
}
