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

**Costs:** key or authority settings to manage; the claims fill the command's context; every endpoint but health needs a token. The Console takes the same token, and prints a development token in the development environment only. Every command declares a permission checked against the caller's (`permissions.md`, written but not generated yet); which claim carries permissions is asked at scaffold time. There is no system user: reactions run as the user who caused them, with that user's permissions.

Example:

```
| Key | Value | Why |
| auth | jwt |  |
```
