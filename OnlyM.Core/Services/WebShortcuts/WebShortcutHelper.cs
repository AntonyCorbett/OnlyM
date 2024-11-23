using System;
using System.IO;
using System.Linq;

namespace OnlyM.Core.Services.WebShortcuts;

public class WebShortcutHelper
{
    private const string UrlToken = "URL";

    private readonly string _path;
    private bool _initialised;
    private string? _webAddress;

    public WebShortcutHelper(string path)
    {
        _path = path;
    }

    public string? Uri
    {
        get
        {
            Init();
            return _webAddress;
        }
    }

    public static void Generate(string localFile, Uri remoteUri)
    {
        using (var writer = new StreamWriter(localFile))
        {
            writer.WriteLine("[InternetShortcut]");
            writer.WriteLine($"URL={remoteUri}");
            writer.Flush();
        }
    }

    private void Init()
    {
        if (!_initialised)
        {
            try
            {
                var lines = File.ReadLines(_path);
                var line = lines.SingleOrDefault(x => x.Trim().StartsWith(UrlToken, StringComparison.OrdinalIgnoreCase));
                if (line != null)
                {
                    var pos = line.IndexOf("=", StringComparison.OrdinalIgnoreCase);
                    if (pos > 0)
                    {
                        _webAddress = line[(pos + 1)..].Trim();
                    }
                }
            }
            catch (Exception)
            {
                // nothing
            }
            finally
            {
                _initialised = true;
            }
        }
    }
}
