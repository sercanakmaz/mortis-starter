namespace Boilerplate.IntegrationTests.TestSupport;

public static class OutboxRows
{
    public const string TwoPending = """
        insert into outbox_messages (id, type, payload, occurred_at, attempts, next_attempt_at)
        values (gen_random_uuid(), 'probe', jsonb_build_object(), now() at time zone 'utc' - interval '1 minute', 0, now() at time zone 'utc' - interval '1 minute'),
               (gen_random_uuid(), 'probe', jsonb_build_object(), now() at time zone 'utc' - interval '1 minute', 0, now() at time zone 'utc' - interval '1 minute')
        """;
}
