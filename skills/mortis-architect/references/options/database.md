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

**Costs:** a multi-document transaction needs a replica set, so the compose file starts a single-node one and a standalone server is not supported; integer keys are not supported as the default; no foreign keys; unique indexes instead of migrations (`architecture/documents.md`). The C# pages are written but not built or run yet.

**Rules:** R8, R12.

Example:

```
| Key | Value | Why |
| database | postgres | relational data, transactions |
```
