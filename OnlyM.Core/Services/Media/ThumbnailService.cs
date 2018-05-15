using Serilog;

namespace OnlyM.Core.Services.Media
{
    using System;
    using System.Data.SQLite;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Text;
    using System.Threading;
    using Models;
    using Utils;

    public sealed class ThumbnailService : IThumbnailService
    {
        private const int MaxPixelDimension = 180;
        private const int CurrentSchemaVersion = 2;
        private readonly Lazy<byte[]> _standardAudioThumbnail = new Lazy<byte[]>(() =>
        {
            var bmp = Properties.Resources.Audio;
            var converter = new ImageConverter();
            return (byte[])converter.ConvertTo(bmp, typeof(byte[]));
        });
        
        public event EventHandler ThumbnailsPurgedEvent;

        public ThumbnailService()
        {
            EnsureDatabaseExists();
        }

        public byte[] GetThumbnail(
            string originalPath, 
            string ffmpegFolder,
            MediaClassification mediaClassification, 
            long originalLastChanged, 
            out bool foundInCache)
        {
            byte[] result = GetThumbnailFromCache(originalPath, originalLastChanged);
            if (result != null)
            {
                foundInCache = true;
                return result;
            }

            result = GenerateThumbnail(originalPath, ffmpegFolder, mediaClassification);
            if (result != null)
            {
                AddThumbnailToCache(originalPath, originalLastChanged, result);
            }

            foundInCache = false;
            return result;
        }

        public void ClearCache()
        {
            using (var c = CreateConnection())
            using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = "delete from thumb; PRAGMA vacuum;";
                cmd.ExecuteNonQuery();
            }

            OnThumbnailsPurgedEvent();
        }
        
        private byte[] GenerateThumbnail(
            string originalPath,
            string ffmpegFolder,
            MediaClassification mediaClassification)
        {
            switch (mediaClassification)
            {
                case MediaClassification.Image:
                    return GraphicsUtils.CreateThumbnailOfImage(originalPath, MaxPixelDimension, ImageFormat.Jpeg);

                case MediaClassification.Video:
                    var tempFile = GraphicsUtils.CreateThumbnailForVideo(originalPath, ffmpegFolder);
                    if (string.IsNullOrEmpty(tempFile))
                    {
                        return null;
                    }

                    return File.ReadAllBytes(tempFile);

                case MediaClassification.Audio:
                    return _standardAudioThumbnail.Value;

                default:
                    return null;
            }
        }

        private void AddThumbnailToCache(string originalPath, long originalLastChanged, byte[] thumbnail)
        {
            using (var c = CreateConnection())
            using (var cmd = c.CreateCommand())
            {
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

        private byte[] GetThumbnailFromCache(string originalPath, long originalLastChanged)
        {
            using (var c = CreateConnection())
            using (var cmd = c.CreateCommand())
            {
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
                            DeleteRow(c, id);
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

        private void DeleteRow(SQLiteConnection connection, int id)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = $"delete from thumb where id = {id}";
                cmd.ExecuteNonQuery();
            }
        }

        private string GetThumbnailDatabasePath()
        {
            return Path.Combine(FileUtils.GetOnlyMThumbnailFolder(), "onlyMThumbs.db");
        }

        private SQLiteConnection CreateConnection()
        {
            var c = new SQLiteConnection($"Data source={GetThumbnailDatabasePath()};Version=3;");
            c.Open();
            return c;
        }

        private void EnsureDatabaseExists()
        {
            var path = GetThumbnailDatabasePath();
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
                    throw new Exception("Could not create thumbnail cache database!");
                }
            }
        }

        private void DeleteDatabase()
        {
            SQLiteConnection.ClearAllPools();
            File.Delete(GetThumbnailDatabasePath());
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
                CreateThumbTable(c);
                SetDatabaseSchemaVersion(c, CurrentSchemaVersion);
            }
        }

        private void CreateThumbTable(SQLiteConnection connection)
        {
            using (var cmd = connection.CreateCommand())
            {
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

        private void OnThumbnailsPurgedEvent()
        {
            ThumbnailsPurgedEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}
