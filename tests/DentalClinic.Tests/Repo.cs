namespace DentalClinic.Tests;

/// <summary>Locations in the repository, for the tests that read the sources.</summary>
public static class Repo
{
    public static string Root { get; } = FindRoot();

    /// <summary>The web application project, <c>src/DentalClinic</c>.</summary>
    public static string App => Path.Combine(Root, "src", "DentalClinic");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "DentalClinic.sln"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("DentalClinic.sln not found above the test binaries.");
    }
}
