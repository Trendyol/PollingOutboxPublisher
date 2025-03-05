using System;
using Microsoft.Extensions.Options;
using PollingOutboxPublisher.ConfigOptions;

namespace PollingOutboxPublisher.Coordinators.Services;

public class CircuitBreaker(IOptions<CircuitBreakerSettings> settings) : ICircuitBreaker
{
    private readonly int _threshold = settings.Value.Threshold;
    private readonly int _durationMs = settings.Value.DurationMs;
    private readonly int _halfOpenMaxAttempts = settings.Value.HalfOpenMaxAttempts;
    private readonly bool _isEnabled = settings.Value.IsEnabled;

    private int _failureCount;
    private DateTime? _lastFailureTime;
    private int _halfOpenAttempts;
    private bool _isInOpenState;

    private bool HasMetFailureThreshold => _failureCount >= _threshold;

    private bool HasWaitDurationPassed
    {
        get
        {
            if (!_lastFailureTime.HasValue) return false;
            var timeSinceLastFailure = DateTime.UtcNow - _lastFailureTime.Value;
            return timeSinceLastFailure.TotalMilliseconds >= _durationMs;
        }
    }

    private bool CanAttemptHalfOpen => _halfOpenAttempts < _halfOpenMaxAttempts;

    public bool IsOpen
    {
        get
        {
            if (!_isEnabled || !HasMetFailureThreshold)
            {
                _isInOpenState = false;
                return false;
            }

            if (HasWaitDurationPassed && CanAttemptHalfOpen)
            {
                _halfOpenAttempts++;
                _isInOpenState = false;
                return false;
            }

            _isInOpenState = true;
            return true;
        }
    }

    public void RecordFailure()
    {
        if (!_isEnabled) return;

        _failureCount++;
        _lastFailureTime = DateTime.UtcNow;
    }

    public void Reset()
    {
        if (!_isEnabled || !_isInOpenState) return;

        _failureCount = 0;
        _lastFailureTime = null;
        _halfOpenAttempts = 0;
        _isInOpenState = false;
    }
}