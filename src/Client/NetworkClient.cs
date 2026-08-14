using System.Collections.Concurrent;
using System.Net.Sockets;
using PokeMmo.Core.Battle;
using PokeMmo.Core.Net;
using PokeMmo.Core.Save;
using PokeMmo.Core.World;
using PokeMmo.RomExtract.Scripts;

namespace PokeMmo.Client;

/// <summary>
/// The client's half of the connection.
/// <para>
/// Receiving runs on its own task and drops messages into a queue; the render loop
/// drains it once a frame. That keeps every piece of game state owned by one thread —
/// touching it from the receive task would mean a position could change part-way
/// through drawing a frame.
/// </para>
/// </summary>
public sealed class NetworkClient : IDisposable
{
    private readonly ConcurrentQueue<NetMessage> _inbox = new();
    private readonly CancellationTokenSource _shutdown = new();

    private TcpClient? _connection;
    private MessageChannel? _channel;

    /// <summary>Assigned by the server on login; zero until then.</summary>
    public int PlayerId { get; private set; }

    public bool IsConnected => _connection?.Connected ?? false;

    /// <summary>Set when the connection drops, so the client can say why.</summary>
    public string? Failure { get; private set; }

    /// <summary>Opens the socket. No account is involved yet.</summary>
    public async Task ConnectAsync(string host, int port)
    {
        _connection = new TcpClient { NoDelay = true };
        await _connection.ConnectAsync(host, port).ConfigureAwait(false);

        _channel = new MessageChannel(_connection.GetStream());
    }

    /// <summary>
    /// Logs in or registers. Returns null on success, or the server's reason.
    /// <para>
    /// The reply is awaited here rather than through the inbox because the receive
    /// loop is not running yet: a failed attempt has to leave the connection usable
    /// for another try, and a queue would mean the login screen polling for something
    /// that may never come.
    /// </para>
    /// </summary>
    public async Task<string?> AuthenticateAsync(string username, string password, bool register)
    {
        if (_channel is null) return "Not connected.";

        NetMessage request = register
            ? new RegisterRequest(username, password)
            : new LoginRequest(username, password);

        await _channel.SendAsync(request, _shutdown.Token).ConfigureAwait(false);

        NetMessage? reply = await _channel.ReceiveAsync(_shutdown.Token).ConfigureAwait(false);

        switch (reply)
        {
            case Welcome welcome:
                PlayerId = welcome.PlayerId;

                // Handed to the game loop like any other message, so there is one code
                // path that places the player.
                _inbox.Enqueue(welcome);

                _ = ReceiveLoopAsync();
                return null;

            case AuthFailed refused:
                return refused.Reason;

            case null:
                return "The server closed the connection.";

            default:
                return "The server said something unexpected.";
        }
    }

    private async Task ReceiveLoopAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested &&
                   await _channel!.ReceiveAsync(_shutdown.Token).ConfigureAwait(false) is { } message)
            {
                _inbox.Enqueue(message);
            }

            Failure ??= "The server closed the connection.";
        }
        catch (OperationCanceledException)
        {
            // Ordinary shutdown.
        }
        catch (Exception ex)
        {
            Failure = ex.Message;
        }
    }

    /// <summary>Everything that has arrived since the last call.</summary>
    public IEnumerable<NetMessage> Drain()
    {
        while (_inbox.TryDequeue(out NetMessage? message)) yield return message;
    }

    /// <summary>
    /// Tells the server which way we just stepped. Fire and forget: the client has
    /// already predicted the result, and waiting for confirmation would add a round
    /// trip of input lag to every square walked.
    /// </summary>
    public void SendMove(Direction direction) => Send(new MoveRequest(direction));

    /// <summary>What the player chose this turn. The server decides what it does.</summary>
    public void SendBattleAction(BattleAction action) => Send(new BattleTurn(action));

    /// <summary>Asks the server to hold somebody still while they are being spoken to.</summary>
    public void SendTalk(int localId) => Send(new TalkRequest(localId));

    public void SendTalkFinished() => Send(new TalkFinished());

    /// <summary>Asks to get onto the water ahead. Whether that is allowed is not this side's call.</summary>
    public void SendSurf() => Send(new SurfRequest());

    /// <summary>Says a square was stepped onto, so the server can decide what that means.</summary>
    public void SendTriggerFired(int x, int y, int? trainerId = null) =>
        Send(new TriggerFired(x, y, trainerId));

    /// <summary>Names the item a script just handed over, for the server to check.</summary>
    public void SendScriptGave(int localId, int itemId) => Send(new ScriptGave(localId, itemId));

    /// <summary>Tells the server which fight a script just set up, for it to check and run.</summary>
    public void SendScriptFought(int localId, int species, int level) =>
        Send(new ScriptFought(localId, species, level));

    /// <summary>Answers which of the four to drop, or that none of them should go.</summary>
    public void SendLearnMove(int moveId, int forget) => Send(new LearnMoveRequest(moveId, forget));

    /// <summary>Says where a scene left somebody, for the server to accept or refuse.</summary>
    /// <summary>Asks for a scene's cast to stand still, wherever they are.</summary>
    public void SendSceneCast(IReadOnlyList<int> localIds) => Send(new SceneCast(localIds));

    /// <summary>Asks the server to walk the player the way a scene says.</summary>

    public void SendScenePlaced(int localId, GridPosition square, Direction facing) =>
        Send(new ScenePlaced(localId, square.X, square.Y, facing));

    /// <summary>Tells the server what a script the player just ran did to their save.</summary>
    public void SendScriptRan(ScriptRun run) =>
        Send(new ScriptRan(
            run.FlagsSet,
            run.FlagsCleared,
            [.. run.VariablesWritten.Select(v => new SavedVariable(v.Key, v.Value))]));

    /// <summary>
    /// Flags with no script behind them: the ones that say somebody is not on the map
    /// any more, which a script asks for by object number rather than by flag.
    /// </summary>
    public void SendFlagsSet(IReadOnlyList<int> flags) => Send(new ScriptRan(flags, [], []));

    public void SendNameMon(int slot, string name) => Send(new NameMonRequest(slot, name));

    public void SendHeal() => Send(new HealRequest());

    public void SendConsole(string text) => Send(new ConsoleCommand(text));

    public void SendBuy(int itemId, int count) => Send(new BuyRequest(itemId, count));

    public void SendSell(int itemId, int count) => Send(new SellRequest(itemId, count));

    public void SendUseItem(int itemId, int slot) => Send(new UseItemRequest(itemId, slot));

    public void SendGiveItem(int itemId, int slot) => Send(new GiveItemRequest(itemId, slot));

    public void SendTakeItem(int slot) => Send(new TakeItemRequest(slot));

    private void Send(NetMessage message)
    {
        if (_channel is null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await _channel.SendAsync(message, _shutdown.Token).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or OperationCanceledException)
            {
                Failure ??= "Lost the connection.";
            }
        });
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        _connection?.Dispose();
        _shutdown.Dispose();
    }
}
