using System;
using System.Diagnostics.CodeAnalysis;

namespace PollingOutboxPublisher.Exceptions;

[ExcludeFromCodeCoverage]
public class MissingConfigurationException(string key) : Exception($"Missing configuration key: {key}");