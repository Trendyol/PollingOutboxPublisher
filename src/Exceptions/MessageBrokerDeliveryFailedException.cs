using System;
using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.Exceptions;

[ExcludeFromCodeCoverage]
public class MessageBrokerDeliveryFailedException : Exception;