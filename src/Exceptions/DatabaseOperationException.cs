using System;

namespace PollingOutboxPublisher.Exceptions;

public class DatabaseOperationException(string message, Exception exception) : Exception(message, exception); 