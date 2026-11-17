# Exceptions

How much structure failures carry.

## exceptions

The rung of exception handling.

**By preset:** lean `minimal`, standard `coded`, advanced `catalogued`.

### minimal

**Choose when:** a prototype.

**Costs:** failures have a kind and a message but no stable code; translated messages are not possible.

**Rules:** R5.

### coded

**Choose when:** a real API: clients and tests rely on a stable code.

**Costs:** a code to invent and keep for every failure; database errors are mapped by hand.

### catalogued

**Choose when:** many modules, translated messages with parameters, log level by status class.

**Costs:** a catalogue to maintain and a wrapper for unexpected failures.

Example:

```
| Key | Value | Why |
| exceptions | coded | clients branch on the code |
```
