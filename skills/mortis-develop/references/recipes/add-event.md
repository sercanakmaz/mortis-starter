# Add an event

A fact an aggregate records when its state changes.

| # | File | Page | Rule | Test |
|---|---|---|---|---|
| 1 | the aggregate's test: the operation raises the event once, with the right values | `worked-slice-tests.md` | AGENTS 3 | red, then green |
| 2 | `src/{Name}.Domain/Aggregates/{Plural}/Events/{Event}.cs`: past tense, ids as strings, never entities | `worked-slice.md` | AGENTS 3 | compiler |
| 3 | the aggregate's operation calls `RaiseEvent` | `worked-slice.md` | AGENTS 3 | step 1 green |
| 4 | an event other processes read implements `IQueueEvent` and carries every value its consumers need | `event-consumers.md` | AGENTS 19 | the consumer's test |
| 5 | [addons=events-project] a queue event lives in `src/{Name}.Contracts/{Plural}/` | `events-project.md` | none | `ContractsRulesTests` |
| 6 | [testing=architecture] a queue event is pinned in `event-contracts.json` | `testing-levels.md` | none | `EventContractTests` |
| 7 | [events.transport=rabbitmq] the wire name is the namespace and type name; record it in the project's wire-format spec | `rabbitmq.md` | none | `transport-rabbitmq.sh` |
| 8 | [addons=caching] a cached answer that shows the change gets an invalidation handler | `caching.md` | none | its test |
