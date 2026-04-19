namespace Db.Migrations;

internal static class MigrationSqlLoader
{
    internal static string Load(string fileName)
    {
        var asm = typeof(MigrationSqlLoader).Assembly;
        var name = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
        using var stream = asm.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
