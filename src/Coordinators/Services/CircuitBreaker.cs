using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.ConfigOptions;

namespace PollingOutboxPublisher.Coordinators.Services;

public class CircuitBreaker(IOptions<CircuitBreakerSettings> settings, ILogger<CircuitBreaker> logger)
    : ICircuitBreaker
{
    private readonly int _threshold = settings.Value.Threshold;
    private readonly int _durationSc = settings.Value.DurationSc;
    private readonly int _halfOpenMaxAttempts = settings.Value.HalfOpenMaxAttempts;
    private readonly bool _isEnabled = settings.Value.IsEnabled;

    private int _failureCount;
    private DateTime? _lastFailureTime;
    private int _halfOpenAttempts;
    private bool _wasOpen;
    private bool _currentOpen;

    private bool HasMetFailureThreshold => _failureCount >= _threshold;

    private bool HasWaitDurationPassed
    {
        get
        {
            if (!_lastFailureTime.HasValue) return false;
            var timeSinceLastFailure = DateTime.UtcNow - _lastFailureTime.Value;
            return timeSinceLastFailure.TotalMilliseconds >= _durationSc*1000;
        }
    }

    private bool CanAttemptHalfOpen => _halfOpenAttempts < _halfOpenMaxAttempts;

    public bool IsOpen
    {
        get
        {
            if (!_isEnabled)
            {
                _currentOpen = false;
                LogStateChangeIfNeeded();
                return false;
            }
    
            if (!HasMetFailureThreshold)
            {
                _currentOpen = false;
                LogStateChangeIfNeeded();
                return false;
            }
    
            if (!HasWaitDurationPassed || !CanAttemptHalfOpen)
            {
                _currentOpen = true;
                LogStateChangeIfNeeded();
                return true;
            }
    
            _halfOpenAttempts++;
            logger.LogInformation("Circuit breaker entering half-open state. Attempt {Attempt} of {MaxAttempts}",
                _halfOpenAttempts, _halfOpenMaxAttempts);
            _currentOpen = false;
            LogStateChangeIfNeeded();
            return false;
        }
    }

    private void LogStateChangeIfNeeded()
    {
        if (_wasOpen == _currentOpen) return;

        _wasOpen = _currentOpen;
        logger.LogInformation("Circuit breaker state changed to: {State}.",
            _currentOpen ? "Open" : "Closed");
    }

    public void RecordFailure()
    {
        if (!_isEnabled) return;

        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;
        _halfOpenAttempts = 0;

        logger.LogWarning("Circuit breaker consecutive failure recorded. Count: {FailureCount}/{Threshold}",
            _failureCount, _threshold);
    }

    public void Reset()
    {
        if (!_isEnabled) return;

        var hadFailures = _failureCount > 0;
        _failureCount = 0;
        _lastFailureTime = null;
        _halfOpenAttempts = 0;

        if (hadFailures)
        {
            logger.LogInformation("Circuit breaker reset. Consecutive failure count cleared.");
        }
    }
}