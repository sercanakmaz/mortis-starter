namespace Boilerplate.Infrastructure.Persistence.Idempotency;

public class IdempotencyRecord
{
    public string Scope { get; private set; }
    public string Key { get; private set; }
    public string RequestHash { get; private set; }
    public DateTime StartedAt { get; private set; }
    public int? StatusCode { get; private set; }
    public string Location { get; private set; }
    public string ResponseBody { get; private set; }

    public static IdempotencyRecord Of(string scope, string key, string requestHash, int? statusCode = null, string location = null, string responseBody = null)
    {
        return new IdempotencyRecord { Scope = scope, Key = key, RequestHash = requestHash, StartedAt = DateTime.UtcNow, StatusCode = statusCode, Location = location, ResponseBody = responseBody };
    }
}
