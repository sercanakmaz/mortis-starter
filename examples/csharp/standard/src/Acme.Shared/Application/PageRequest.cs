namespace Acme.Shared.Application;

public static class PageRequest
{
    public const int DefaultSize = 20;
    public const int MaxSize = 100;

    public static int Skip(int page, int size) => (page - 1) * size;
}
