using PokeMmo.Core.Net;
using PokeMmo.Core.World;
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
        Collision = first.Collision;
        _texture = Upload(first);
    }

    public LoadedMap Map { get; private set; }

    /// <summary>
    /// The map's walkability with its people made solid.
    /// <para>
    /// Built here because the client predicts every step against it. The server counts
    /// people as blocking; a client predicting against bare map collision would walk
    /// straight through somebody, be corrected, and spend the rest of the map arguing.
    /// </para>
    /// <para>
    /// Rebuilt whenever they move. Copying a map's walkability is a few hundred bytes
    /// and happens a handful of times a second, which is cheaper than teaching every
    /// caller to consult two sources.
    /// </para>
    /// </summary>
    public CollisionGrid Collision { get; private set; } = null!;

    /// <summary>Where the server says everybody is, keyed by their id on this map.</summary>
    public Dictionary<int, WalkingPerson> People { get; } = [];

    /// <summary>Replaces everybody, as sent on arriving at a map.</summary>
    public void Place(IEnumerable<ObjectView> people)
    {
        People.Clear();

        foreach (ObjectView person in people)
        {
            People[person.LocalId] = new WalkingPerson(
                person.GraphicsId, new GridPosition(person.X, person.Y), person.Facing);
        }

        Rebuild();
    }

    /// <summary>
    /// Moves one of them.
    /// <para>
    /// A step and a turn on the spot arrive as the same message, and they must not look
    /// the same: walking one square takes a fraction of a second, and a shopkeeper
    /// glancing about should not slide anywhere at all.
    /// </para>
    /// </summary>
    public void Moved(ObjectMoved moved)
    {
        if (!People.TryGetValue(moved.LocalId, out WalkingPerson? person)) return;

        person.GoTo(new GridPosition(moved.X, moved.Y), moved.Facing);

        Rebuild();
    }

    /// <summary>Advances everybody's walk. Called once a frame.</summary>
    public void Update(float deltaSeconds)
    {
        foreach (WalkingPerson person in People.Values) person.Update(deltaSeconds);
    }

    private void Rebuild() =>
        Collision = Map.Collision.With(People.Values.Select(p => p.Square));

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

        // Emptied rather than carried over: whoever was on the last map is not here,
        // and the server sends this map's people immediately after saying we arrived.
        People.Clear();
        Collision = loaded.Collision;

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

