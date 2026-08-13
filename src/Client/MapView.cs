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

    /// <summary>
    /// Maps already read off the cartridge and already on the graphics card.
    /// <para>
    /// Walking through a door is the commonest thing anybody does and it used to cost a
    /// full decompression and a full upload every time — including walking back out of
    /// the room you were just in. On this cartridge, in a release build, that is 7 ms for
    /// Pallet Town, 20 ms for Route 1 and 71 ms for Viridian Forest, and several times
    /// that in a debug build. It reads as the game stopping.
    /// </para>
    /// <para>
    /// Bounded, because a texture is memory on a card and 425 of them is not a cache, it
    /// is the whole world. Sixteen is more than any run of doors anybody walks through
    /// and small enough not to have to think about.
    /// </para>
    /// </summary>
    private readonly Dictionary<string, (LoadedMap Map, Texture2D Texture)> _loaded = [];

    private readonly List<string> _order = [];

    private const int Remembered = 16;

    private Texture2D _texture;

    public MapView(MapLibrary library, LoadedMap first)
    {
        _library = library;
        Map = first;
        Collision = first.Collision;
        _texture = Upload(first);

        Keep($"{first.Bank}.{first.Number}", first, _texture);
    }

    private void Keep(string mapId, LoadedMap map, Texture2D texture)
    {
        _loaded[mapId] = (map, texture);
        _order.Remove(mapId);
        _order.Add(mapId);

        while (_order.Count > Remembered)
        {
            string oldest = _order[0];
            _order.RemoveAt(0);

            if (!_loaded.Remove(oldest, out (LoadedMap Map, Texture2D Texture) gone)) continue;

            Raylib.UnloadTexture(gone.Texture);
        }
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
                person.GraphicsId, new GridPosition(person.X, person.Y), person.Facing, person.Heals);
        }

        Rebuild();
    }

    /// <summary>
    /// Takes one of them off the map, for this player and this visit.
    /// <para>
    /// A tree that has been cut. The map file still has it — everybody else can still
    /// see it, and walking out and back in puts it up again — so this removes it from
    /// the living population rather than from anything on disk.
    /// </para>
    /// </summary>
    public void Remove(int localId)
    {
        if (!People.Remove(localId)) return;

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

        if (_loaded.TryGetValue(mapId, out (LoadedMap Map, Texture2D Texture) already))
        {
            Arrive(mapId, already.Map, already.Texture);
            return true;
        }

        if (_library.TryLoad(mapId) is not { } loaded) return false;

        Arrive(mapId, loaded, Upload(loaded));

        return true;
    }

    private void Arrive(string mapId, LoadedMap map, Texture2D texture)
    {
        Map = map;

        // Emptied rather than carried over: whoever was on the last map is not here,
        // and the server sends this map's people immediately after saying we arrived.
        People.Clear();
        Collision = map.Collision;

        _texture = texture;

        Keep(mapId, map, texture);
    }

    /// <summary>
    /// Puts a map's pixels on the graphics card.
    /// <para>
    /// The pixels go up as they are. They used to be encoded to a PNG and decoded again
    /// on the way, which was the larger half of the cost of walking through a door —
    /// 52 ms of the 71 for Viridian Forest — for a round trip that ends where it started.
    /// The PNG writer was borrowed because it was already tested and kept this file free
    /// of unsafe code, and the generic UpdateTexture does the same without it.
    /// </para>
    /// </summary>
    private static Texture2D Upload(LoadedMap map)
    {
        Image blank = Raylib.GenImageColor(map.PixelWidth, map.PixelHeight, Color.Black);
        Texture2D texture = Raylib.LoadTextureFromImage(blank);

        Raylib.UnloadImage(blank);
        Raylib.UpdateTexture(texture, map.Rgba);
        Raylib.SetTextureFilter(texture, TextureFilter.Point);

        return texture;
    }

    public void Dispose()
    {
        foreach ((LoadedMap _, Texture2D texture) in _loaded.Values) Raylib.UnloadTexture(texture);

        _loaded.Clear();
        _order.Clear();
    }
}

