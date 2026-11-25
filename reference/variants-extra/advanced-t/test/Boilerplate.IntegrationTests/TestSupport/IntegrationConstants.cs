namespace Boilerplate.IntegrationTests.TestSupport;

public static class IntegrationConstants
{
    public const string PostgresVariable = "MORTIS_TEST_POSTGRES";
    public const string RabbitMqVariable = "MORTIS_TEST_RABBITMQ";
    public const string PostgresImage = "postgres:16";
    public const string RabbitMqImage = "rabbitmq:4-management";
    public const string DatabasePrefix = "it_";
    public const string SkipValue = "skip";
    public const string PostgresCollection = "postgres";
    public const string RabbitMqCollection = "rabbitmq";
}
