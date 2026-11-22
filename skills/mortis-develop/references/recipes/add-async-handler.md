# Add an asynchronous reaction

A reaction after the commit, for eventual consistency (stock, notifications, read models fed later). Its failure never undoes the original change.

| # | File | Page | Rule | Test |
|---|---|---|---|---|
| 1 | the event implements `IQueueEvent` and carries what the reaction needs: `add-event.md` (this skill) | `event-consumers.md` | AGENTS 19 | the aggregate's test |
| 2 | the reaction's command: `add-command.md` (this skill) | `application.md` | AGENTS 10 | its handler test |
| 3 | `test/.../Application/{Plural}/{Action}When{Aggregate}{Event}EventConsumerTests.cs` | `event-consumers.md` | AGENTS 14 | the consumer sends that command as the user whose command raised the event |
| 4 | `src/{Name}.Application/{Plural}/{Action}When{Aggregate}{Event}EventConsumer.cs`: `BaseEventConsumer<TEvent>` | `event-consumers.md` | AGENTS 19 | step 3 green |
| 5 | the host line: `add-consumer.md` (this skill) | `event-consumers.md` | AGENTS 13 | the conformance of the transport |
| 6 | [events.outbox=none] it runs in-process after the commit; lost if the process dies first | `events-inprocess.md` | none | `example-http.sh` |
| 7 | [events.outbox=after-command\|background] it runs from the outbox, at least once: make the command idempotent | `outbox.md` | none | `outbox.sh` |
| 8 | [auth=jwt] the user whose command raised the event needs the reaction's permission, once permissions are generated | `permissions.md` | none | `permissions.sh` |
