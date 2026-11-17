# Authentication

Who the caller is.

## auth

Whether the Api authenticates callers.

**By preset:** lean `none`, standard `jwt`, advanced `jwt`.

### none

**Choose when:** a prototype or a service behind a gateway.

**Costs:** the audit user of a write is empty.

### jwt

**Choose when:** callers sign in elsewhere and present a bearer token.

**Costs:** key or authority settings to manage; the claims fill the command's context.

Example:

```
| Key | Value | Why |
| auth | jwt |  |
```
