namespace OnlyM.Core.Services.Database
{
    using System;
    using System.Data.SQLite;
    using System.IO;
    using System.Text;
    using Serilog;
    using Utils;

    // ReSharper disable once ClassNeverInstantiated.Global
    public class DatabaseService : IDatabaseService
    {
        private const int CurrentSchemaVersion = 3;

        public DatabaseService()
        {
            EnsureDatabaseExists();
        }

        public void ClearThumbCache()
        {
            using (var c = CreateConnection())
            using (var cmd = c.CreateCommand())
            {
                Log.Logger.Verbose("Clearing thumb cache");

                cmd.CommandText = "delete from thumb; PRAGMA vacuum;";
                cmd.ExecuteNonQuery();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "There is no danger of sql injection")]
        public void AddThumbnailToCache(string originalPath, long originalLastChanged, byte[] thumbnail)
        {
            using (var c = CreateConnection())
            using (var cmd = c.CreateCommand())
            {
                Log.Logger.Verbose($"Inserting into thumb table {originalPath}");

                var sb = new StringBuilder();

                sb.AppendLine("insert into thumb (path, image, changed)");
                sb.AppendLine("select");
                sb.AppendLine($"@P, @T, {originalLastChanged}");
                sb.AppendLine("where not exists(select 1 from thumb where path=@P)");

                cmd.CommandText = sb.ToString();
                cmd.Parameters.AddWithValue("@P", originalPath);
                cmd.Parameters.AddWithValue("@T", thumbnail);

                cmd.ExecuteNonQuery();
            }
        }

        public byte[] GetThumbnailFromCache(string originalPath, long originalLastChanged)
        {
            using (var c = CreateConnection())
            using (var cmd = c.CreateCommand())
            {
                Log.Logger.Verbose($"Selecting from thumb table {originalPath}");

                cmd.CommandText = "select id, image, changed from thumb where path = @P";
                cmd.Parameters.AddWithValue("@P", originalPath);

                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        int id = Convert.ToInt32(r["id"]);
                        long lastChanged = Convert.ToInt64(r["changed"]);

                        if (lastChanged != originalLastChanged)
                        {
                            DeleteThumbRow(c, id);
                        }
                        else
                        {
                            return (byte[])r["image"];
                        }
                    }
                }
            }

            return null;
        }

        public void AddBrowserData(string url, double zoomLevel)
        {
            using (var c = CreateConnection())
            using (var cmd = c.CreateCommand())
            {
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
        }

        public BrowserData GetBrowserData(string url)
        {
            using (var c = CreateConnection())
            using (var cmd = c.CreateCommand())
            {
                Log.Logger.Verbose($"Selecting from browser table {url}");

                cmd.CommandText = "select id, url, zoom from browser where url = @U";
                cmd.Parameters.AddWithValue("@U", url.Trim());

                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        var result = new BrowserData
                        {
                            Id = Convert.ToInt32(r["id"]),
                            Url = (string)r["url"],
                            ZoomLevel = Convert.ToDouble(r["zoom"])
                        };

                        result.Sanitize();
                        return result;
                    }
                }
            }

            return null;
        }

        private void EnsureDatabaseExists()
        {
            var path = GetDatabasePath();
            if (File.Exists(path))
            {
                if (IsUnsupportedVersion())
                {
                    DeleteDatabase();
                }
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

        private string GetDatabasePath()
        {
            return Path.Combine(FileUtils.GetOnlyMDatabaseFolder(), "OnlyMDatabase.db");
        }

        private SQLiteConnection CreateConnection()
        {
            var c = new SQLiteConnection($"Data source={GetDatabasePath()};Version=3;");
            c.Open();
            return c;
        }

        private void DeleteDatabase()
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

        private int GetDatabaseSchemaVersion()
        {
            using (var c = CreateConnection())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "select * from pragma_user_version()";
                return Convert.ToInt32(cmd.ExecuteScalar());
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "There is no danger of sql injection")]
        private void SetDatabaseSchemaVersion(SQLiteConnection connection, int version)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"PRAGMA user_version={version}";
                cmd.ExecuteNonQuery();
            }
        }

        private bool IsUnsupportedVersion()
        {
            return GetDatabaseSchemaVersion() != CurrentSchemaVersion;
        }

        private void CreateDatabase()
        {
            using (var c = CreateConnection())
            {
                Log.Logger.Verbose("Creating database");

                CreateThumbTable(c);
                CreateBrowserTable(c);
                SetDatabaseSchemaVersion(c, CurrentSchemaVersion);
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "There is no danger of sql injection")]
        private void DeleteThumbRow(SQLiteConnection connection, int id)
        {
            using (var cmd = connection.CreateCommand())
            {
                Log.Logger.Verbose("Deleting row from thumb table");

                cmd.CommandText = $"delete from thumb where id = {id}";
                cmd.ExecuteNonQuery();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "There is no danger of sql injection")]
        private void CreateThumbTable(SQLiteConnection connection)
        {
            using (var cmd = connection.CreateCommand())
            {
                Log.Logger.Verbose("Creating thumb table");

                var sb = new StringBuilder();
                sb.AppendLine("CREATE TABLE[thumb](");
                sb.AppendLine("[id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,");
                sb.AppendLine("[path] TEXT NOT NULL COLLATE NOCASE,");
                sb.AppendLine("[image] BLOB NOT NULL,");
                sb.AppendLine("[changed] INTEGER NOT NULL);");

                sb.AppendLine("CREATE UNIQUE INDEX[PathIndex] ON[thumb]([path]);");

                cmd.CommandText = sb.ToString();
                cmd.ExecuteNonQuery();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "There is no danger of sql injection")]
        private void CreateBrowserTable(SQLiteConnection connection)
        {
            using (var cmd = connection.CreateCommand())
            {
                Log.Logger.Verbose("Creating browser table");

                var sb = new StringBuilder();
                sb.AppendLine("CREATE TABLE[browser](");
                sb.AppendLine("[id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,");
                sb.AppendLine("[url] TEXT NOT NULL COLLATE NOCASE,");
                sb.AppendLine("[zoom] NUMBER NOT NULL);");
                
                sb.AppendLine("CREATE UNIQUE INDEX[UrlIndex] ON[browser]([url]);");

                cmd.CommandText = sb.ToString();
                cmd.ExecuteNonQuery();
            }
        }
    }
}
