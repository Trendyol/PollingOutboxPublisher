using System.Threading.Tasks;
using PollingOutboxPublisher.Models;

namespace PollingOutboxPublisher.Coordinators.OutboxCoordinator.Services.Interfaces;

public interface IOffsetSetter
{
    Task SetLatestOffset(OutboxEvent[] items);
}