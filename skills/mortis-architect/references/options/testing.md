# Testing

How much of the project is tested, and how.

## testing

The rung of testing.

**By preset:** lean `unit`, standard `integration`, advanced `architecture`.

### unit

**Choose when:** a prototype.

**Costs:** behaviour that needs a real database is checked by hand.

### handlers

**Choose when:** a real project without a container in CI.

**Costs:** handler and host tests use fakes, not a database.

### integration

**Choose when:** repositories and the outbox must be proven against the real database.

**Costs:** a database container in CI and slower runs.

### architecture

**Choose when:** many developers: layer rules and event contracts must not drift.

**Costs:** dependency and contract tests to maintain; a coverage gate.

Example:

```
| Key | Value | Why |
| testing | integration |  |
```
