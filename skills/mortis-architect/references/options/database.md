# Database

Where aggregates are stored.

## database

The one database the aggregates live in.

**Asked in the interview;** no default.

### postgres

**Choose when:** transactions make the outbox simple; optimistic concurrency comes from the database.

**Costs:** migrations to keep; a relational mapping for child entities.

### mongo

**Choose when:** an aggregate fits one document and you do not want a relational mapping.

**Costs:** a multi-document transaction needs a replica set, so any outbox makes the compose file start one; integer keys are not supported.

**Rules:** R8, R12.

Example:

```
| Key | Value | Why |
| database | postgres | relational data, transactions |
```
