using System.Collections.Generic;
using System.Threading.Tasks;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.MissingCoordinator.Services.Interfaces;

public interface IMissingEventCleaner
{
    Task CleanMissingEventsHaveOutboxEventAsync(MissingEvent[] missingEvents, List<MappedMissingEvent> results);
    Task CleanMissingEventsNotHaveOutboxEventAsync(MissingEvent[] missingEvents);
    Task HandleNonMatchedMissingEventsAsync(OutboxEvent[] outboxEvents, MissingEvent[] retryableMissingEvents);
}