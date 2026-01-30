using System.Globalization;
using System.IO.Compression;
using Microsoft.Data.Sqlite;
using Dapper;

public class Program
{
    public class CompDatabaseEntry
    {
        public int WikiId { get; set; }
        public string ChineseName { get; set; }
        public string? CurseForgeSlug { get; set; }
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

        // 1. 准备数据库文件路径（使用 .sqlite 或 .db 扩展名）
        var databasePath = Path.Combine(
            Path.GetDirectoryName(fileName) ?? Environment.CurrentDirectory,
            $"{Path.GetFileNameWithoutExtension(fileName)}.sqlite");

        if (File.Exists(databasePath))
            File.Delete(databasePath);

        // 2. 建立连接并创建表
        using (var connection = new SqliteConnection($"Data Source=\"{databasePath}\";Pooling=False"))
        {
            connection.Open();

            // 创建表结构并建立索引（SQLite 在这种模式下查询效率极高）
            connection.Execute(@"
            CREATE TABLE ModTranslation (
                WikiId INTEGER,
                ChineseName TEXT,
                CurseForgeSlug TEXT,
                ModrinthSlug TEXT
            );
            CREATE INDEX idx_curseforge ON ModTranslation (CurseForgeSlug);
            CREATE INDEX idx_modrinth ON ModTranslation (ModrinthSlug);
            CREATE INDEX idx_chinesename ON ModTranslation (ChineseName);
        ");

            Console.WriteLine($"The database will be saved to {databasePath}");

            using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new StreamReader(fileStream);

            // 3. 批量处理数据
            int row = 0;
            int count = 0;

            // 使用事务是 SQLite 性能的关键
            using var transaction = connection.BeginTransaction();

            var insertSql = @"INSERT INTO ModTranslation (WikiId, ChineseName, CurseForgeSlug, ModrinthSlug) 
                          VALUES (@WikiId, @ChineseName, @CurseForgeSlug, @ModrinthSlug)";

            while (reader.ReadLine() is { } line)
            {
                row++;
                line = line.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var added = Parser(line);
                foreach (var entry in added)
                {
                    var dbEntry = new CompDatabaseEntry
                    {
                        WikiId = row,
                        ChineseName = entry.chineseName,
                        CurseForgeSlug = entry.curseForgeSlug,
                        ModrinthSlug = entry.modrinthSlug,
                    };

                    // 使用 Dapper 进行参数化插入，防止 SQL 注入且自动处理
                    connection.Execute(insertSql, dbEntry, transaction);
                    count++;
                }
            }

            transaction.Commit(); // 提交事务
            Console.WriteLine($"{count} entries added");

            // 4. 优化数据库文件（收缩空间）
            connection.Execute("VACUUM;");
            connection.Close();
        }

        Thread.Sleep(1000);

        // 5. 压缩逻辑
        CompressFile(databasePath);
    }

    private static void CompressFile(string databasePath)
    {
        var outputFile = Path.ChangeExtension(databasePath, ".dbcp");
        if (File.Exists(outputFile)) File.Delete(outputFile);

        using var dbFs = new FileStream(databasePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var compressedFs = new FileStream(outputFile, FileMode.Create);
        using var zip = new GZipStream(compressedFs, CompressionMode.Compress);
        dbFs.CopyTo(zip);
        Console.WriteLine($"Compressed database saved to {outputFile}");
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
            if (idPart.StartsWith("@"))
            {
                // @modrinth
                modrinthSlug = idPart.Substring(1);
                curseForgeSlug = null;
            }
            else if (idPart.EndsWith("@"))
            {
                // curseforge@
                var slug = idPart.TrimEnd('@');
                curseForgeSlug = slug;
                modrinthSlug = slug;
            }
            else if (idPart.Contains("@"))
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