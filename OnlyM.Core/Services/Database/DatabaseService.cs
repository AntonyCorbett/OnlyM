using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Text;
using OnlyM.Core.Utils;
using Serilog;

namespace OnlyM.Core.Services.Database;

// ReSharper disable once ClassNeverInstantiated.Global
public class DatabaseService : IDatabaseService
{
    private const int CurrentSchemaVersion = 4;

    public DatabaseService()
    {
        EnsureDatabaseExists();
    }

    public void ClearThumbCache()
    {
        using var c = CreateConnection();
        using var cmd = c.CreateCommand();
        Log.Logger.Verbose("Clearing thumb cache");

        cmd.CommandText = "delete from thumb; PRAGMA vacuum;";
        cmd.ExecuteNonQuery();
    }

    public void AddThumbnailToCache(string originalPath, long originalLastChanged, byte[] thumbnail)
    {
        using var c = CreateConnection();
        using var cmd = c.CreateCommand();
        Log.Logger.Verbose($"Inserting into thumb table {originalPath}");

        var sb = new StringBuilder();

        sb.AppendLine("insert into thumb (path, image, changed)");
        sb.AppendLine("select");
        sb.AppendLine(CultureInfo.InvariantCulture, $"@P, @T, {originalLastChanged}");
        sb.AppendLine("where not exists(select 1 from thumb where path=@P)");

        cmd.CommandText = sb.ToString();
        cmd.Parameters.AddWithValue("@P", originalPath);
        cmd.Parameters.AddWithValue("@T", thumbnail);

        cmd.ExecuteNonQuery();
    }

    public byte[]? GetThumbnailFromCache(string originalPath, long originalLastChanged)
    {
        using var c = CreateConnection();
        using var cmd = c.CreateCommand();
        Log.Logger.Verbose($"Selecting from thumb table {originalPath}");

        cmd.CommandText = "select id, image, changed from thumb where path = @P";
        cmd.Parameters.AddWithValue("@P", originalPath);

        using var r = cmd.ExecuteReader();
        if (r.Read())
        {
            var lastChanged = Convert.ToInt64(r["changed"], CultureInfo.InvariantCulture);

            if (lastChanged != originalLastChanged)
            {
                var id = Convert.ToInt32(r["id"], CultureInfo.InvariantCulture);
                DeleteThumbRow(c, id);
            }
            else
            {
                return (byte[])r["image"];
            }
        }

        return null;
    }

    public void AddBrowserData(string url, double zoomLevel)
    {
        using var c = CreateConnection();
        using var cmd = c.CreateCommand();
        Log.Logger.Verbose($"Inserting into browser table {url}");

        var sb = new StringBuilder();

        sb.AppendLine("insert into browser (url, zoom)");
        sb.AppendLine("values (@U, @Z)");
        sb.AppendLine("on conflict(url) do update set zoom=@Z");

        cmd.CommandText = sb.ToString();
        cmd.Parameters.AddWithValue("@U", url.Trim());
        cmd.Parameters.AddWithValue("@Z", zoomLevel);

        cmd.ExecuteNonQuery();
    }

    public void AddMediaStartOffsetData(string fileName, string startOffsets, int lengthSeconds)
    {
        using var c = CreateConnection();
        using var cmd = c.CreateCommand();
        Log.Logger.Verbose($"Inserting into mediaOptions table: {fileName}");

        var sb = new StringBuilder();

        sb.AppendLine("insert into mediaOptions (fileName, startOffsets, lengthSeconds)");
        sb.AppendLine("values (@F, @S, @L)");
        sb.AppendLine("on conflict(fileName) do update set startOffsets=@S, lengthSeconds=@L");

        cmd.CommandText = sb.ToString();
        cmd.Parameters.AddWithValue("@F", fileName.Trim());
        cmd.Parameters.AddWithValue("@S", startOffsets);
        cmd.Parameters.AddWithValue("@L", lengthSeconds);

        cmd.ExecuteNonQuery();
    }

    public MediaStartOffsetData? GetMediaStartOffsetData(string fileName)
    {
        using var c = CreateConnection();
        using var cmd = c.CreateCommand();
        Log.Logger.Verbose($"Selecting from mediaOptions table: {fileName}");

        cmd.CommandText = "select id, fileName, startOffsets, lengthSeconds from mediaOptions where fileName = @F";
        cmd.Parameters.AddWithValue("@F", fileName.Trim());

        using var r = cmd.ExecuteReader();
        if (r.Read())
        {
            var result = new MediaStartOffsetData
            {
                Id = Convert.ToInt32(r["id"], CultureInfo.InvariantCulture),
                FileName = (string)r["fileName"],
                StartOffsets = ParseStartOffsets((string)r["startOffsets"]),
                LengthSeconds = Convert.ToInt32(r["lengthSeconds"], CultureInfo.InvariantCulture),
            };

            result.Sanitize();
            return result;
        }

        return null;
    }

    public BrowserData? GetBrowserData(string url)
    {
        using var c = CreateConnection();
        using var cmd = c.CreateCommand();
        Log.Logger.Verbose($"Selecting from browser table {url}");

        cmd.CommandText = "select id, url, zoom from browser where url = @U";
        cmd.Parameters.AddWithValue("@U", url.Trim());

        using var r = cmd.ExecuteReader();
        if (r.Read())
        {
            var result = new BrowserData
            {
                Id = Convert.ToInt32(r["id"], CultureInfo.InvariantCulture),
                Url = (string)r["url"],
                ZoomLevel = Convert.ToDouble(r["zoom"], CultureInfo.InvariantCulture),
            };

            result.Sanitize();
            return result;
        }

        return null;
    }

    private static void EnsureDatabaseExists()
    {
        var path = GetDatabasePath();
        if (File.Exists(path) && IsUnsupportedVersion())
        {
            DeleteDatabase();
        }

        if (!File.Exists(path))
        {
            CreateDatabase();

            if (!File.Exists(path))
            {
                throw new Exception("Could not create database!");
            }
        }
    }

    private static string GetDatabasePath() => Path.Combine(FileUtils.GetOnlyMDatabaseFolder(), "OnlyMDatabase.db");

    private static SQLiteConnection CreateConnection()
    {
        var c = new SQLiteConnection($"Data source={GetDatabasePath()};Version=3;", true);
        c.Open();
        return c;
    }

    private static void DeleteDatabase()
    {
        Log.Logger.Verbose("Deleting database");

        try
        {
            SQLiteConnection.ClearAllPools();
            File.Delete(GetDatabasePath());
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Could not delete database");
            throw;
        }
    }

    private static int GetDatabaseSchemaVersion()
    {
        try
        {
            using var c = CreateConnection();
            using var cmd = c.CreateCommand();
            cmd.CommandText = "select * from pragma_user_version()";
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Could not get database schema version");
            return 0;
        }
    }

    private static void SetDatabaseSchemaVersion(SQLiteConnection connection, int version)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version={version}";
        cmd.ExecuteNonQuery();
    }

    private static bool IsUnsupportedVersion() => GetDatabaseSchemaVersion() != CurrentSchemaVersion;

    private static void CreateDatabase()
    {
        using var c = CreateConnection();
        Log.Logger.Verbose("Creating database");

        CreateThumbTable(c);
        CreateBrowserTable(c);
        CreateMediaOptionsTable(c);
        SetDatabaseSchemaVersion(c, CurrentSchemaVersion);
    }

    private static void DeleteThumbRow(SQLiteConnection connection, int id)
    {
        using var cmd = connection.CreateCommand();
        Log.Logger.Verbose("Deleting row from thumb table");

        cmd.CommandText = $"delete from thumb where id = {id}";
        cmd.ExecuteNonQuery();
    }

    private static void CreateThumbTable(SQLiteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        Log.Logger.Verbose("Creating thumb table");

        var sb = new StringBuilder();
        sb.AppendLine("CREATE TABLE[thumb](");
        sb.AppendLine("[id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,");
        sb.AppendLine("[path] TEXT NOT NULL COLLATE NOCASE,");
        sb.AppendLine("[image] BLOB NOT NULL,");
        sb.AppendLine("[changed] INTEGER NOT NULL);");

        sb.AppendLine("CREATE UNIQUE INDEX[pathIndex] ON[thumb]([path]);");

        cmd.CommandText = sb.ToString();
        cmd.ExecuteNonQuery();
    }

    private static void CreateMediaOptionsTable(SQLiteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        Log.Logger.Verbose("Creating media options table");

        var sb = new StringBuilder();
        sb.AppendLine("CREATE TABLE[mediaOptions](");
        sb.AppendLine("[id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,");
        sb.AppendLine("[fileName] TEXT NOT NULL COLLATE NOCASE,");
        sb.AppendLine("[startOffsets] TEXT NULL,");
        sb.AppendLine("[lengthSeconds] INTEGER NOT NULL);");

        sb.AppendLine("CREATE UNIQUE INDEX[fileNameIndex] ON[mediaOptions]([fileName]);");

        cmd.CommandText = sb.ToString();
        cmd.ExecuteNonQuery();
    }

    private static void CreateBrowserTable(SQLiteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        Log.Logger.Verbose("Creating browser table");

        var sb = new StringBuilder();
        sb.AppendLine("CREATE TABLE[browser](");
        sb.AppendLine("[id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,");
        sb.AppendLine("[url] TEXT NOT NULL COLLATE NOCASE,");
        sb.AppendLine("[zoom] NUMBER NOT NULL);");

        sb.AppendLine("CREATE UNIQUE INDEX[urlIndex] ON[browser]([url]);");

        cmd.CommandText = sb.ToString();
        cmd.ExecuteNonQuery();
    }

    private static List<int> ParseStartOffsets(string s)
    {
        var result = new List<int>();

        if (!string.IsNullOrEmpty(s))
        {
            var tokens = s.Split([","], StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (int.TryParse(token, out var seconds))
                {
                    result.Add(seconds);
                }
            }
        }

        return result;
    }
}
