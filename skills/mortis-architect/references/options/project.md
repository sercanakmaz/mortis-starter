# Project keys

The three keys that identify the file and the project.

## version

The format version of this file. Always `1` today; a future format change raises it.

**Default:** `1`.

### 1

**Choose when:** always.

**Costs:** nothing.

Example:

```
| Key | Value | Why |
| version | 1 |  |
```

## name

The project's name, used for the solution, namespaces or module path, and folders. Letters, digits, dot, dash, underscore; starts with a letter.

**Asked in the interview;** no default.

Example:

```
| Key | Value | Why |
| name | Acme.Billing | the product's name |
```

## preset

The starting point the other rows are measured from. A row you leave out takes the preset's value.

**Asked in the interview;** no default.

### lean

**Choose when:** a prototype, a weekend project, one person, a small service nobody else consumes.

**Costs:** no outbox: an event can be lost if the process dies right after the commit; minimal exceptions; unit tests only.

### standard

**Choose when:** a real product with one team, a database to protect, an API for a front end.

**Costs:** a worker host to run, an outbox table to keep small, integration tests that need a database container.

### advanced

**Choose when:** one service of a platform: tenants, a broker and many developers; each other service has its own repository and file.

**Costs:** a broker to run, a separate worker, a longer build and more files to understand before the first feature.

Example:

```
| Key | Value | Why |
| preset | standard | one team, a real product |
```
