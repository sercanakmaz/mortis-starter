using Boilerplate.Shared.Exceptions;
using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Persistence.Mongo;

public static class MongoExceptionHandler
{
    private const int DuplicateKey = 11000;

    public static async Task<T> Run<T>(Func<Task<T>> write)
    {
        try
        {
            return await write();
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new DuplicateException(IndexOf(exception.WriteError.Message), exception);
        }
        catch (MongoCommandException exception) when (exception.Code == DuplicateKey)
        {
            throw new DuplicateException(IndexOf(exception.ErrorMessage), exception);
        }
    }

    public static Task Run(Func<Task> write)
    {
        return Run(async () =>
        {
            await write();

            return true;
        });
    }

    private static string IndexOf(string message)
    {
        var start = message.IndexOf("index: ", StringComparison.Ordinal);

        return start < 0 ? "unique index" : message[(start + 7)..].Split(' ')[0];
    }
}
