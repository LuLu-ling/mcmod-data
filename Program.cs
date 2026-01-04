using System.Globalization;
using LiteDB;
using System.IO.Compression;

public class Program
{
    public class CompDatabaseEntry
    {
        public int WikiId {get; set;}
        public string ChineseName {get; set;}
        public string? CurseForgeSlug {get; set;}
        public string? ModrinthSlug {get; set;}
    }
    
    public static void Main(string[] args)
    {
        Console.WriteLine("Please enter a file name");
        var fileName = Console.ReadLine()?.Trim().Trim('"');
        ArgumentNullException.ThrowIfNullOrWhiteSpace(fileName);
        fileName = Path.GetFullPath(fileName);
        if (!File.Exists(fileName))
            throw new FileNotFoundException();

        var databasePath = Path.Combine(
            Path.GetDirectoryName(fileName) ?? Environment.CurrentDirectory,
            $"{Path.GetFileNameWithoutExtension(fileName)}.db");
        if (File.Exists(databasePath))
            File.Delete(databasePath);
        using var database = new LiteDatabase(databasePath);

        Console.WriteLine($"The database will be saved to {databasePath}");

        using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(fileStream);
        
        var col = database.GetCollection<CompDatabaseEntry>("ModTranslation");

        int row = 0;
        int count = 0;
        while (reader.ReadLine() is { } line)
        {
            row++;
            line = line.Trim();
            if (string.IsNullOrEmpty(line))
                continue;
            var added = Parser(line);
            count += added.Count;
            List<CompDatabaseEntry> currentOut = [];
            foreach (var entry in added)
                currentOut.Add(new CompDatabaseEntry()
                {
                    WikiId = row,
                    ChineseName = entry.chineseName,
                    CurseForgeSlug = entry.curseForgeSlug,
                    ModrinthSlug = entry.modrinthSlug,
                });
            col.Insert(currentOut);
        }

        Console.WriteLine($"{count} entries added");

        col.EnsureIndex(x => x.ModrinthSlug);
        col.EnsureIndex(x => x.CurseForgeSlug);

        database.Dispose();
        
        using var db = new FileStream(databasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        var outputFile = Path.Combine(
            Path.GetDirectoryName(databasePath) ?? Environment.CurrentDirectory,
            $"{Path.GetFileNameWithoutExtension(databasePath)}.dbcp");
        if (File.Exists(outputFile))
            File.Delete(outputFile);
        using var compressedDb = new FileStream(
            outputFile,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.Read);
        using var zip = new GZipStream(compressedDb, CompressionMode.Compress);
        db.CopyTo(zip);
    }

    public static List<(string curseForgeSlug, string modrinthSlug, string chineseName)> Parser(string line)
    {
        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        List<(string? curseForgeSlug, string? modrinthSlug, string chineseName)> result = [];
        var parts = line.Split('¨');
        foreach (var part in parts)
        {
            var subparts = part.Split('|');
            if (subparts.Length != 2)
                continue;
            (string? curseForgeSlug, string? modrinthSlug, string chineseName) currentOut;
            var sources = subparts[0].Trim().Split('@');
            currentOut.chineseName = subparts[1].Trim();
            if (sources.Length == 2)
            {
                currentOut.curseForgeSlug = sources[0];
                currentOut.modrinthSlug = sources[1];
            }
            else if (sources.Length == 1)
            {
                currentOut.curseForgeSlug = sources[0];
                currentOut.modrinthSlug = null;
            }
            else
            {
                currentOut.curseForgeSlug = null;
                currentOut.modrinthSlug = null;
            }

            if (currentOut.chineseName.Contains('*'))
            {
                currentOut.chineseName = currentOut.chineseName.Replace("*",string.Empty);
                var displayIdInName = string.IsNullOrEmpty(currentOut.curseForgeSlug)
                    ? currentOut.modrinthSlug
                    : currentOut.curseForgeSlug;
                displayIdInName = textInfo.ToTitleCase(displayIdInName!);
                currentOut.chineseName = $"{currentOut.chineseName} ({displayIdInName})";
            }
            result.Add(currentOut);
        }
        return result;
    }
}