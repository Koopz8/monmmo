using PokeMmo.RomExtract.Maps;
using Raylib_cs;

namespace PokeMmo.Client;

/// <summary>
/// The map currently on screen, and the ability to change it.
/// <para>
/// A texture on the graphics card has to be released before another takes its place,
/// and forgetting to is the sort of leak that only shows up after an hour of walking
/// through doors. Keeping the pairing in one place is cheaper than remembering.
/// </para>
/// </summary>
public sealed class MapView : IDisposable
{
    private readonly MapLibrary _library;

    private Texture2D _texture;

    public MapView(MapLibrary library, LoadedMap first)
    {
        _library = library;
        Map = first;
        _texture = Upload(first);
    }

    public LoadedMap Map { get; private set; }

    public Texture2D Texture => _texture;

    /// <summary>The map's <c>bank.map</c> address, which is what the server calls it.</summary>
    public string MapId => $"{Map.Bank}.{Map.Number}";

    /// <summary>
    /// Switches to another map. Returns false when this cartridge does not have it,
    /// leaving the current one on screen — a client that cannot follow the server
    /// somewhere should stay where it is rather than showing nothing.
    /// </summary>
    public bool SwitchTo(string mapId)
    {
        if (mapId == MapId) return true;
        if (_library.TryLoad(mapId) is not { } loaded) return false;

        Raylib.UnloadTexture(_texture);

        Map = loaded;
        _texture = Upload(loaded);

        return true;
    }

    private static Texture2D Upload(LoadedMap map)
    {
        // Reuse the extractor's own PNG writer rather than marshalling raw pixels:
        // it is already covered by tests, and it keeps this file free of unsafe code.
        Image image = Raylib.LoadImageFromMemory(".png", map.ToPng());
        Texture2D texture = Raylib.LoadTextureFromImage(image);

        Raylib.UnloadImage(image);
        Raylib.SetTextureFilter(texture, TextureFilter.Point);

        return texture;
    }

    public void Dispose() => Raylib.UnloadTexture(_texture);
}
