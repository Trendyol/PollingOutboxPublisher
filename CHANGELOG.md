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