namespace Boilerplate.Tests.TestSupport;

public static class SolutionFolder
{
    public static string Find()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);

        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "Boilerplate.slnx")))
        {
            folder = folder.Parent;
        }

        return folder?.FullName ?? throw new DirectoryNotFoundException("No folder above the test binaries holds Boilerplate.slnx.");
    }
}
