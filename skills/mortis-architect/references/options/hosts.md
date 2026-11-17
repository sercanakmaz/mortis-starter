# Hosts

The processes the project runs.

## hosts.worker

Where background work (outbox relay, consumers, jobs) runs.

**By preset:** lean `none`, standard `in-api`, advanced `separate`.

### none

**Choose when:** no background work.

**Costs:** no outbox, no scheduler.

**Rules:** R3, R9.

### in-api

**Choose when:** background work shares the Api's process.

**Costs:** a heavy job slows requests; scaling the Api scales the relay too.

**Rules:** R4.

### separate

**Choose when:** a broker is used or the work is heavy.

**Costs:** a second deployable and its configuration.

Example:

```
| Key | Value | Why |
| hosts.worker | in-api |  |
```
