# Exceptions

How much structure failures carry.

## exceptions

The rung of exception handling.

**By preset:** lean `minimal`, standard `coded`, advanced `catalogued`.

### minimal

**Choose when:** a prototype.

**Costs:** a unique or foreign-key violation in the database is an unexpected failure (500) instead of `duplicate` or `reference_violation`; translated messages are not possible. Statuses and the error body are the same as at the other levels.

**Rules:** R5.

### coded

**Choose when:** a real API: clients and tests rely on a stable code.

**Costs:** a code to keep for every rule; the database's unique and foreign-key errors are translated to `duplicate` (409) and `reference_violation` (400).

### catalogued

**Choose when:** many developers or translated messages: every code must be declared once with its default message.

**Costs:** a catalogue to keep in step with the code, and a test that fails when a rule code is missing from it.

Example:

```
| Key | Value | Why |
| exceptions | coded | clients branch on the code |
```
