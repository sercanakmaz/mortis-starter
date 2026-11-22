# Turn an option on after the project exists

Change a value in `MORTIS.md`, write the option's files from the same pages `mortis-scaffold` uses, adapt the existing files the option touches, and prove it with `verify.sh`.

| # | File | Page | Rule | Test |
|---|---|---|---|---|
| 1 | `MORTIS.md`: the new value with its reason; `python3 -I ../mortis-architect/scripts/validate_decisions.py MORTIS.md` must pass (the rules R1 to R14) | `../mortis-architect/references/options/` | the rule that fires | the validator |
| 2 | `docs/adr/`: a record for the change, numbered after the last one | `../mortis-architect/references/adr-template.md` | none | file exists |
| 3 | the option's new files: `manifest.json` entries whose `when` now matches (`python3 -I ../mortis-scaffold/scripts/structure.py <project> --plan` lists what is expected) | the entries' pages | none | `verify.sh` structure |
| 4 | the existing files the option changes: `manifest.json`'s `option_changes` for the option, and the "Files this option changes" table of its page | the option's page | none | build and tests |
| 5 | [events.outbox=after-command\|background] turning an outbox on: the outbox table migration, `UnitOfWork.CommitTransactionAsync`, the worker host per `hosts.worker`; `EventPublisher` and its tests go | `outbox.md` | R1, R3 | `outbox.sh` |
| 6 | [events.transport=rabbitmq] turning RabbitMQ on: the bus files, the worker becomes `separate`, compose gains `rabbitmq` | `rabbitmq.md` | R2, R4 | `transport-rabbitmq.sh` |
| 7 | [tenancy.mode=interface] turning tenancy on: `make-aggregate-tenant-owned.md` (this skill) for every aggregate; a tenant for the existing rows | `tenancy.md` | R13 | `tenancy.sh` |
| 8 | [languages.mode!=none] turning languages on: the message files for every existing error code | `languages.md` | R5, R6 | `languages.sh` |
| 9 | [addons=contexts] turning contexts on moves every file: do it in one change, before the first release with RabbitMQ (wire names follow namespaces) | `contexts.md` | none | `contexts.sh` |
| 10 | [addons=events-project] turning the contracts project on after a release: pin every moved event's old wire name with `[MessageName]` | `events-project.md` | none | `events-project.sh` |
| 11 | the kernel shape of the option: `shape_for.py` lists the lines the project must now have | `kernel-shape.txt` | none | `verify.sh` shape |
| 12 | `bash ../mortis-scaffold/scripts/verify.sh <project>` | none | none | every check |

Turning an option off is the same in reverse; its files are removed, and `verify.sh` reports any that remain as extra.
