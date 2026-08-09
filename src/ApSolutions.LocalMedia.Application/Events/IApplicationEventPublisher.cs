namespace ApSolutions.LocalMedia.Application.Events;

public interface IApplicationEventPublisher
{
    Task PublishAsync<TEvent>(TEvent applicationEvent, CancellationToken cancellationToken = default)
        where TEvent : notnull;
}
