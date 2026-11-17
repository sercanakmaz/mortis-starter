# Keys

The type of an aggregate's identity.

## keys.default

The key type new aggregates use unless one overrides it.

**By preset:** lean `guid`, standard `guid`, advanced `guid`.

### guid

**Choose when:** ids are made in the program before saving; works everywhere.

**Costs:** ids are larger and not in insert order unless time-ordered ones are used.

### int

**Choose when:** an existing system or a person-readable number is required.

**Costs:** the database makes the id, so it is unknown until the insert and the client cannot send one.

**Rules:** R12.

### long

**Choose when:** the same as `int` with room for very many rows.

**Costs:** as `int`.

**Rules:** R12.

Example:

```
| Key | Value | Why |
| keys.default | guid |  |
```
