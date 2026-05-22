using ProtoBuf;
using System.Globalization;
using System.IO.Compression;

namespace ModData;

public class Program
{
    [ProtoContract]
    public class CompDatabaseEntry
    {
        [ProtoMember(1)]
        public int WikiId { get; set; }
        [ProtoMember(2)]
        public required string ChineseName { get; set; }
        [ProtoMember(3)]
        public string? CurseForgeSlug { get; set; }
        [ProtoMember(4)]
        public string? ModrinthSlug { get; set; }
    }

    public static void Main(string[] args)
    {
        Console.WriteLine("Please enter a file name");
        var fileName = Console.ReadLine()?.Trim().Trim('"');
        ArgumentNullException.ThrowIfNullOrWhiteSpace(fileName);
        fileName = Path.GetFullPath(fileName);
        if (!File.Exists(fileName))
            throw new FileNotFoundException();
        var workFolder = Directory.GetParent(fileName);
        ArgumentNullException.ThrowIfNull(workFolder, nameof(workFolder));

        using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new StreamReader(fileStream);

        var buffer = new List<CompDatabaseEntry>();
        var wikiId = 0;

        while (reader.ReadLine() is { } line)
        {
            wikiId++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            foreach (var (curseForgeSlug, modrinthSlug, chineseName) in Parser(line))
            {
                buffer.Add(new CompDatabaseEntry
                {
                    WikiId = wikiId,
                    ChineseName = chineseName,
                    CurseForgeSlug = curseForgeSlug,
                    ModrinthSlug = modrinthSlug
                });
            }
        }

        using var fs = new FileStream(Path.Combine(workFolder.FullName, "mcmod.buf"), FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        using var compress = new GZipStream(fs, CompressionLevel.SmallestSize);
        Serializer.Serialize(compress, buffer);
    }

    public static List<(string? curseForgeSlug, string? modrinthSlug, string chineseName)> Parser(string line)
    {
        var textInfo = CultureInfo.CurrentCulture.TextInfo;
        var result = new List<(string? curseForgeSlug, string? modrinthSlug, string chineseName)>();

        if (string.IsNullOrEmpty(line)) return result;

        var parts = line.Split('¨');
        foreach (var part in parts)
        {
            var subparts = part.Split('|');
            if (subparts.Length < 1) continue;

            string idPart = subparts[0].Trim();
            string chineseName = subparts.Length >= 2 ? subparts[1].Trim() : "";

            string? curseForgeSlug = null;
            string? modrinthSlug = null;

            // Match VB logic exactly
            if (idPart.StartsWith('@'))
            {
                // @modrinth
                modrinthSlug = idPart.Substring(1);
                curseForgeSlug = null;
            }
            else if (idPart.EndsWith('@'))
            {
                // curseforge@
                var slug = idPart.TrimEnd('@');
                curseForgeSlug = slug;
                modrinthSlug = slug;
            }
            else if (idPart.Contains('@'))
            {
                // cf@mr
                var split = idPart.Split('@', 2); // max 2 parts
                curseForgeSlug = string.IsNullOrWhiteSpace(split[0]) ? null : split[0];
                modrinthSlug = string.IsNullOrWhiteSpace(split[1]) ? null : split[1];
            }
            else
            {
                // only curseforge
                curseForgeSlug = idPart;
                modrinthSlug = null;
            }

            // Handle * replacement
            if (chineseName.Contains('*'))
            {
                var baseName = chineseName.Replace("*", "").Trim();
                var displayId = curseForgeSlug ?? modrinthSlug ?? "Unknown";
                // Mimic VB: replace '-' with space, then title case
                var displayName = textInfo.ToTitleCase(displayId.Replace('-', ' '));
                chineseName = $"{baseName} ({displayName})";
            }

            result.Add((curseForgeSlug, modrinthSlug, chineseName));
        }
        return result;
    }
}
