# 0026 — Jobs on a clock run once per tick, as a named service user

- Status: accepted
- Date: 2026-11-20
- Context: Some work happens on a clock, not on a request. With several worker replicas a naive timer runs it once per replica.

## Decision

A recurring job implements `IRecurringJob` (a name, a default cron, `RunAsync` that sends commands). `SchedulerService` claims each due tick with an insert into `scheduled_job_runs` keyed by job and tick; only the winner runs it. Each job runs as the service user its settings name; there is no system user.

## What varies

The jobs, their schedules and their users.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| A timer in each process | Nothing | Runs once per replica |
| A distributed lock library | A lock service | A tick claimed in the database needs nothing new |
| An external scheduler (cron, Quartz cluster) | Another system | Jobs belong next to the commands they send |
| **A row per tick, claimed by insert** | Two tables | n/a |

## Consequences

- Gained: any number of workers; a job's runs and failures are visible in a table; jobs are audited as their service user.
- Lost or owed: a failed tick is not retried until the next one; a service user per job to provision.
- Cost of going back: low.

## How it is enforced

`SchedulerTicks` tests and `scheduler.sh` (with two workers a job fires once per tick).
