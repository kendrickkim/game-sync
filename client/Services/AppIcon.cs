using System.Drawing;
using System.Reflection;

namespace GameSync.Services;

/// <summary>
/// Resolves the application icon. The embedded resource is tried first so the
/// icon also works from a single-file publish, where loose Assets files are absent.
/// </summary>
public static class AppIcon
{
    private const string ResourceName = "GameSync.Assets.app.ico";

    private static readonly Lazy<Icon?> Cached = new(Load);

    public static Icon? Value => Cached.Value;

    private static Icon? Load()
    {
        try
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream != null)
            {
                return new Icon(stream);
            }
        }
        catch
        {
            // fall through to the file-based lookups
        }

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(path))
            {
                return new Icon(path);
            }

            return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        }
        catch
        {
            return null;
        }
    }
}
