# Request lifecycle

How one command travels from a host to a result, and what happens to its events. Roles only; the notation is in [pseudo-code.md](pseudo-code.md).

## The numbered path

1. **[Host]** receives the outside request, authenticates it, parses it into a command, clears the context fields the client may have sent, and fills `context` (actor, correlation, causation, tenant, language).
2. **[Host]** calls `dispatcher.send(command)`.
3. **[Shared]** the dispatcher finds the handler registered for that command and gives it to the pipeline.
4. **[Shared]** the pipeline runs every validator for the command. A failure stops here with a validation error; nothing has been written.
5. **[Shared]** the pipeline runs the before-middlewares (logging, timing).
6. **[Shared]** if the command is transactional, the pipeline begins the transaction. If one is already open, it joins it.
7. **[Application]** the handler loads the aggregates the use case needs through their repositories (usually one; several when the action is about all of them).
8. **[Domain]** when the decision needs several entities or an outside system, the handler asks a domain service; otherwise it calls the aggregate directly.
9. **[Domain]** each aggregate runs its operation: checks its rules, changes its state, raises one or several events (one command can raise many events).
10. **[Application]** the handler hands each changed aggregate to its repository; the repository takes the raised events off it and records the change.
11. **[Application]** the handler maps the aggregate to a view.
12. **[Shared]** the pipeline asks the unit of work to commit.

## The commit order

```
save changes
run sync handlers, in order       they may write and raise more events, which are handled too
COMMIT                            on any failure before this line: roll back, clear events, rethrow
hand committed events to the event publisher
run the after-commit actions
```

Everything above `COMMIT` is all-or-nothing. Everything below it is allowed to fail without undoing the change.

13. **[Infrastructure]** the event publisher of the project's model takes the committed events: in process it runs the async handlers, each in its own scope and transaction; with an outbox the rows were written before `COMMIT` and its worker runs them (see [events.md](events.md)).
14. **[Shared]** the pipeline runs the after-middlewares and returns the view.
15. **[Host]** turns the view into the outside world's response, or a failure into `code, message, errors` with the failure's status.

## What each failure does

| Failure | Where | Effect |
|---|---|---|
| Validation | step 4 | nothing written, error returned |
| Business rule | step 9 | nothing saved, transaction rolled back |
| Sync handler throws | before `COMMIT` | the whole change is rolled back, the caller gets the error |
| Async handler throws | after `COMMIT` | logged; the change stays; the next handler still runs |
| Concurrency conflict in an async handler | after `COMMIT` | retried in a fresh transaction up to a fixed number of attempts |
| Write without a transaction | step 10 | fails loudly, never silently loses events |

## Notes

- Counting: one command runs one handler; the handler changes one aggregate or, when the action is about several, those several; each change can raise N events; each event can have M handlers; each event handler sends one command, which can raise events of its own. So one request can fan out into many commands and events, all sharing one correlation id.
- The view of step 11 is built before `COMMIT`; it does not show what a sync handler changes afterwards. Read the aggregate again to see it.
- A command sent from inside a handler joins the open transaction; the first to begin owns the commit.
- Events are raised in step 9, before the repository write of step 10. An event raised after the write would never be handled.
