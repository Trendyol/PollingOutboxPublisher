using System.Threading.Tasks;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services.Interfaces;

public interface IMissingDetector
{
    Task DetectAsync(OutboxEventsBatch outboxEventsBatch);
}