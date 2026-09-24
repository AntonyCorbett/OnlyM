using System.Data.SQLite;
using OnlyM.Core.Services.Database;

namespace OnlyM.Core.Tests;

public sealed class DatabaseServiceMigrationTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"OnlyM-migration-{Guid.NewGuid()}.db");

    public void Dispose()
    {
        File.Delete(_databasePath);
        File.Delete(_databasePath + "-wal");
        File.Delete(_databasePath + "-shm");
    }

    [Fact]
    public void Version4_PreservesExistingDataAndSupportsManualOrderAfterReopening()
    {
        CreateVersion4Database();

        var service = new DatabaseService(_databasePath);
        Assert.Equal(5L, Scalar("PRAGMA user_version"));
        AssertExistingData(service);
        Assert.Empty(service.GetMediaOrderItemKeys("folder"));

        service.UpsertMediaOrder("folder", ["b.mp4", "a.mp4"]);
        var reopened = new DatabaseService(_databasePath);

        AssertExistingData(reopened);
        Assert.Equal(["b.mp4", "a.mp4"], reopened.GetMediaOrderItemKeys("folder"));
        Assert.Equal(5L, Scalar("PRAGMA user_version"));
    }

    [Fact]
    public void FailedMigration_RollsBackAndPreservesVersion4ForRetry()
    {
        CreateVersion4Database();
        // Force failure after CREATE TABLE, while creating the first new index.
        Execute("CREATE INDEX scopeItemKeyIndex ON thumb(changed)");

        Assert.Throws<SQLiteException>(() => new DatabaseService(_databasePath));

        Assert.Equal(4L, Scalar("PRAGMA user_version"));
        Assert.Equal(0L, Scalar("SELECT count(*) FROM sqlite_master WHERE name = 'mediaOrder'"));
        Assert.Equal("10,20", Scalar("SELECT startOffsets FROM mediaOptions"));

        Execute("DROP INDEX scopeItemKeyIndex");
        AssertExistingData(new DatabaseService(_databasePath));
        Assert.Equal(5L, Scalar("PRAGMA user_version"));
    }

    [Fact]
    public void NewDatabase_CreatesVersion5AndSupportsManualOrder()
    {
        var service = new DatabaseService(_databasePath);

        service.UpsertMediaOrder("folder", ["a.mp4"]);

        Assert.Equal(5L, Scalar("PRAGMA user_version"));
        Assert.Equal(["a.mp4"], service.GetMediaOrderItemKeys("folder"));
    }

    private static void AssertExistingData(DatabaseService service)
    {
        Assert.Equal(new byte[] { 1, 2, 3 }, service.GetThumbnailFromCache("image.jpg", 123));
        var browser = Assert.IsType<BrowserData>(service.GetBrowserData("https://example.com"));
        Assert.Equal(1.5, browser.ZoomLevel);
        var offsets = Assert.IsType<MediaStartOffsetData>(service.GetMediaStartOffsetData("video.mp4"));
        Assert.Equal([10, 20], offsets.StartOffsets);
        Assert.Equal(120, offsets.LengthSeconds);
    }

    private void CreateVersion4Database() => Execute(
        """
        CREATE TABLE thumb (
            id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,
            path TEXT NOT NULL COLLATE NOCASE,
            image BLOB NOT NULL,
            changed INTEGER NOT NULL);
        CREATE UNIQUE INDEX pathIndex ON thumb(path);
        CREATE TABLE browser (
            id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,
            url TEXT NOT NULL COLLATE NOCASE,
            zoom NUMBER NOT NULL);
        CREATE UNIQUE INDEX urlIndex ON browser(url);
        CREATE TABLE mediaOptions (
            id INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL UNIQUE,
            fileName TEXT NOT NULL COLLATE NOCASE,
            startOffsets TEXT NULL,
            lengthSeconds INTEGER NOT NULL);
        CREATE UNIQUE INDEX fileNameIndex ON mediaOptions(fileName);
        INSERT INTO thumb(path, image, changed) VALUES ('image.jpg', X'010203', 123);
        INSERT INTO browser(url, zoom) VALUES ('https://example.com', 1.5);
        INSERT INTO mediaOptions(fileName, startOffsets, lengthSeconds) VALUES ('video.mp4', '10,20', 120);
        PRAGMA user_version=4;
        """);

    private void Execute(string sql)
    {
        using var connection = new SQLiteConnection($"Data source={_databasePath};Version=3;");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private object Scalar(string sql)
    {
        using var connection = new SQLiteConnection($"Data source={_databasePath};Version=3;");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
