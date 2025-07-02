## v1.6.0 (July 2, 2025)

### Added:
- Db Credentials file feature. 
- This feature allows a database connection to be created in cases where database credentials need to be read from a file. Currently only active on PostgreSQL.
- It can be activated with the new config value `UseDbCredentialsFile`.
- This feature requires a new config section called `DbCredentialsFileSettings` with the following options:
  - `FileName`: File name containing database credentials (should be full path)
  - `Host`: Host information of the database
  - `Database`: Db name of the database
  - `ApplicationName`: App name information on database connection
  - `Port`: Port of the database
  - `AdditionalParameters`: Additional parameters to be added on Connection. Flags such as `Pooling`, `TrustServerCertificate` can be added.


## v1.5.0 (March 6, 2025)

### Added:
- Circuit breaker implementation for handling consecutive database failures
- New configuration section `CircuitBreakerSettings` with the following options:
    - `IsEnabled`: Controls circuit breaker functionality
    - `Threshold`: Maximum number of consecutive failures
    - `DurationSc`: Duration in seconds for circuit open state
    - `HalfOpenMaxAttempts`: Maximum attempts in half-open state

### Changed:
- Enhanced error handling in offset setting operations
- Improved logging for database operation failures

## v1.4.0 (December 17, 2024)

### Added:
- `ReloadOnChange` flag to Kafka configuration. This allows you to change the configuration without restarting the application.

## v1.3.0 (October 26, 2024)

### Changed:
- The default value of Kafka.SaslMechanism removed from configuration.
- The default value of Kafka.SecurityProtocol removed from configuration.

> [!WARNING]
> The default values for `Kafka.SaslMechanism` and `Kafka.SecurityProtocol` have been removed. Please ensure to set these values in your configuration to avoid any issues.

## v1.2.0 (June 11, 2024)

- Project is now open source 🥳

#### Changed:
- Project name changed to "Polling Outbox Publisher". It was "Message Publisher" before.
- Kafka and Redis Config keys are changed.
- Serilog is used for logging, instead of NLog.
#### Added:
- Couchbase support.
- PostgreSQL support developed by ([tolgakisin](https://github.com/tolgakisin)).