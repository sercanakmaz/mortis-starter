# 0029 — A chain of reactions longer than 16 commands stops

- Status: accepted
- Date: 2026-11-20
- Context: Handlers that trigger each other, directly or through the outbox and the bus, would run forever and fill the logs, the queues and the database.

## Decision

The command context and every event carry the chain of command names that led to them. The pipeline appends the command's name and refuses to run a command whose chain is longer than `LoopGuardConstants.MaxChain` (16) with a 500 `loop_detected` that names the chain.

## What varies

The limit.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Nothing | Nothing | An accidental loop runs until someone notices |
| A depth number only | Smaller | The error cannot say which handlers loop |
| **The chain of names** | A list on the context and the event | n/a |

## Consequences

- Gained: a loop stops within a second and its log line names it.
- Lost or owed: every event carries a short list of names.
- Cost of going back: low.

## How it is enforced

`CommandPipelineTests`, `LoopGuardTests` (two handlers that trigger each other stop at the limit), and `loop-guard.sh`.
