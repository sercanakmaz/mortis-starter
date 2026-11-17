# ADR template

One record per value that differs from the preset. Keep it to one screen. Filename: `docs/adr/NNNN-<slug>.md`, numbered from 0001, slug from the decision.

```markdown
# NNNN — <the decision in one line>

- Status: accepted
- Date: <YYYY-MM-DD>
- Context: <what in the project makes the preset's value wrong or doubtful>

## Decision

<The key and the value chosen, in one or two sentences.>

## What varies

<The one thing this choice decides, in a sentence.>

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| <preset's value> | <from the option page's Costs line> | <reason> |
| **<chosen value>** | <from the option page's Costs line> | n/a |

## Consequences

- Gained: <what the choice buys>
- Lost or owed: <what it costs, including rules it triggered>
- Cost of going back: <small | moderate | large>
```

Cost lines come from the **Costs** line of the value's section in `references/options/`. "More robust" is not a cost.
