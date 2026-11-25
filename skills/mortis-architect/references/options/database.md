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

**Costs:** a multi-document transaction needs a replica set, so the compose file starts a single-node one and a standalone server is not supported; integer keys are not supported as the default; no foreign keys; unique indexes instead of migrations (`architecture/documents.md`). Built and verified in C# on the standard preset (phase 2); tenancy on MongoDB and the RabbitMQ transport with it are not built yet.

**Rules:** R8, R12.

Example:

```
| Key | Value | Why |
| database | postgres | relational data, transactions |
```
