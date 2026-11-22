#!/usr/bin/env bash
# Conformance: the scheduler add-on. With two hosts running the scheduler, a job fires once per schedule tick.
# NOT RUN YET (decision 50): written in phase 1 from scheduler.md, first run in phase 2.
# Needs: PROJECT NAME LANG_SH WORK DATABASE API_PORT WORKER, a migrated PostgreSQL, psql. Runs about two minutes.
# Applies when: addons contains scheduler, database = postgres (R9: hosts.worker is not none)
set -uo pipefail
. "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

JOB="cancel-empty-orders"
JOB_ENV=("Scheduler__PollSeconds=2" "Scheduler__Jobs__${JOB}__Cron=* * * * *" "Scheduler__Jobs__${JOB}__UserId=scheduler-conformance" "Scheduler__Jobs__${JOB}__UserEmail=scheduler@example.com")
START="$(sql "select now() at time zone 'utc'")"
PIDS=()
for n in 1 2; do
  if [ "${WORKER:-none}" = "separate" ]; then
    PIDS+=("$(env "${JOB_ENV[@]}" MORTIS_LOG="$WORK/scheduler.$n.log" "$LANG_SH" worker-start "$PROJECT" "$NAME")")
  else
    PIDS+=("$(env "${JOB_ENV[@]}" MORTIS_LOG="$WORK/scheduler.$n.log" "$LANG_SH" api-start "$PROJECT" "$NAME" "$((API_PORT + 20 + n))")")
  fi
done
sleep 130
for pid in "${PIDS[@]}"; do kill "$pid" 2>/dev/null; wait "$pid" 2>/dev/null; done

RUNS="$(sql "select count(*) from scheduled_job_runs where job = '$JOB' and started_at >= '$START'")"
TICKS="$(sql "select count(distinct tick) from scheduled_job_runs where job = '$JOB' and started_at >= '$START'")"
expect_equal "scheduler: in two minutes of two hosts the job ran for at least two ticks" "yes" "$([ "${TICKS:-0}" -ge 2 ] && echo yes || echo no)"
expect_equal "scheduler: each tick ran once (one run row per tick)" "$TICKS" "$RUNS"
expect_equal "scheduler: every run finished without an error" "0" "$(sql "select count(*) from scheduled_job_runs where job = '$JOB' and started_at >= '$START' and (finished_at is null or error is not null)")"
cat "$WORK/scheduler.1.log" "$WORK/scheduler.2.log" > "$WORK/scheduler.log" 2>/dev/null
expect_equal "scheduler: the hosts logged one 'ran' line per tick between them" "$TICKS" "$(log_count "$WORK/scheduler.log" "l.get('Job') == '$JOB' and str(l.get('Message', '')).startswith('Job $JOB ran')")"

report
[ "$FAILED" -eq 0 ]
