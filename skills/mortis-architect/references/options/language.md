# Language

The programming language of the generated project.

## language

Which language the project is written in. Each language has its own reference pages; the architecture is the same.

**Asked in the interview;** no default.

### csharp

**Choose when:** the proven path: .NET 10, EF Core, PostgreSQL or MongoDB.

**Costs:** needs the .NET 10 SDK; the house rules (no comments, no private helpers in orchestration classes) apply.

### go

**Choose when:** the team writes Go and the lean preset's values fit: in-process events, no outbox, no authentication, minimal failures, commands for reads, unit tests, PostgreSQL with uuid keys (decision 54). Go 1.24 or later, `pgx` with hand-written SQL, the standard library router.

**Costs:** every other value is refused for Go until its reference solution is built; a project that will soon need an outbox, a broker, authentication or tenants should start in C#.

**Rules:** R11.

Example:

```
| Key | Value | Why |
| language | csharp | the team's language |
```
