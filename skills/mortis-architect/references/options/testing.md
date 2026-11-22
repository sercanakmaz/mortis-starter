# Testing

How much of the project is tested, and how. Each level includes the ones before it. Levels above `unit` are written but not generated yet (decision 49); the C# is on `testing-levels.md`.

```
unit          every class has its test, with fakes and builders; no I/O                      (the core)
handlers      + a validator test per command; scenarios: a command through the real container,
                dispatcher, pipeline and validators, persistence ports faked, ValidateOnBuild
integration   + a second test project against real PostgreSQL and the broker (containers, or
                MORTIS_TEST_POSTGRES / MORTIS_TEST_RABBITMQ); no database -> skipped, never passed
architecture  + layer references, every command and sync handler registered, no private method
                in a *Service; the queue events' contracts pinned in a golden file; a coverage gate
```

The core's handler tests use no I/O, so they belong to `unit`: every handler has its test at every level.

## testing

The rung of testing.

**By preset:** lean `unit`, standard `integration`, advanced `architecture`.

### unit

**Choose when:** a prototype.

**Costs:** behaviour that needs a real database is checked by hand.

### handlers

**Choose when:** a real project without a container in CI.

**Costs:** a validator test per command and a scenario per main command; still no database.

### integration

**Choose when:** repositories and the outbox must be proven against the real database.

**Costs:** Docker (or a test database) in CI, a second test project, slower runs; tests are skipped without a database, so CI must make sure one is there.

### architecture

**Choose when:** many developers: layer rules and event contracts must not drift.

**Costs:** dependency and registration rules, a golden file of event contracts updated deliberately, an 80% line-coverage gate.

Example:

```
| Key | Value | Why |
| testing | integration |  |
```
