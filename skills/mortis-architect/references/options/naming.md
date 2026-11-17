# Naming

Identifier casing is decided by the language and is never asked. These four keys are the project's choices where an existing system must be matched. They are asked only when the description mentions an existing database, API or consumer.

## naming.database

How table and column names are written.

**By preset:** lean `snake_case`, standard `snake_case`, advanced `snake_case`.

### snake_case

**Choose when:** a new database.

**Costs:** a mapping rule instead of the code's own names.

### as-code

**Choose when:** the database already uses the code's casing.

**Costs:** quoted identifiers in SQL.

Example:

```
| Key | Value | Why |
| naming.database | snake_case |  |
```

## naming.json

How JSON property names are written in requests and responses.

**By preset:** lean `camelCase`, standard `camelCase`, advanced `camelCase`.

### camelCase

**Choose when:** a JavaScript or mobile front end.

**Costs:** none.

### snake_case

**Choose when:** existing clients already send snake case.

**Costs:** none.

Example:

```
| Key | Value | Why |
| naming.json | camelCase |  |
```

## naming.routes

How URL path segments are written.

**By preset:** lean `kebab-case`, standard `kebab-case`, advanced `kebab-case`.

### kebab-case

**Choose when:** new APIs.

**Costs:** none.

### lowercase

**Choose when:** existing clients use one lowercase word.

**Costs:** multi-word routes get harder to read.

Example:

```
| Key | Value | Why |
| naming.routes | kebab-case |  |
```

## naming.events

How event names appear on the wire. This changes wire names, so it cannot be changed once consumers exist.

**By preset:** lean `bare`, standard `bare`, advanced `bare`.

### bare

**Choose when:** a new system.

**Costs:** two aggregates that raise an event of the same name need distinct namespaces.

### prefixed

**Choose when:** existing consumers expect the aggregate name in the event name.

**Costs:** longer names, and a breaking change from `bare` later.

Example:

```
| Key | Value | Why |
| naming.events | bare |  |
```
