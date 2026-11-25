namespace Acme.IntegrationTests.TestSupport;

public static class OutboxRows
{
    public const string TwoPending = """
        insert into outbox_messages (id, event_id, consumer, type, payload, occurred_at, attempts, max_attempts, next_attempt_at)
        values (gen_random_uuid(), gen_random_uuid(), 'probe', 'probe', jsonb_build_object(), now() at time zone 'utc' - interval '1 minute', 0, 5, now() at time zone 'utc' - interval '1 minute'),
               (gen_random_uuid(), gen_random_uuid(), 'probe', 'probe', jsonb_build_object(), now() at time zone 'utc' - interval '1 minute', 0, 5, now() at time zone 'utc' - interval '1 minute')
        """;
}
