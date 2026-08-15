using System.Diagnostics;
using System.Net.Sockets;
using PokeMmo.Core.Net;
using PokeMmo.Core.World;

namespace PokeMmo.Crowd;

/// <summary>
/// A crowd of players, for finding out what this server actually does when there are
/// thousands of them.
/// <para>
/// This is an instrument, in the same sense as the startup report: it exists to produce
/// numbers nobody can argue with, before anybody optimises anything. Every guess about
/// where a server spends its time is wrong until it is measured, and the guesses in this
/// project's own head were "the database" and "JSON", neither of which was the answer.
/// </para>
/// <para>
/// It speaks the real protocol over a real socket. Nothing here is a mock: these are the
/// same frames the raylib client sends, minus the walking, the drawing and the cartridge.
/// A bot registers, waits for its Welcome, and then steps at a fixed rate for the length
/// of the run — and times how long each step takes to come back as the server's own
/// answer, which is the number a player would feel.
/// </para>
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        int players = Number(args, "--players") ?? 200;
        int port = Number(args, "--port") ?? 7777;
        int seconds = Number(args, "--seconds") ?? 30;
        int stepsPerMinute = Number(args, "--steps") ?? 60;
        string prefix = Text(args, "--prefix") ?? "crowd";

        // Registering makes the accounts; logging in uses ones that are already there.
        // They are very different measurements and this switch is the difference.
        bool returning = args.Contains("--login");

        Console.WriteLine(
            $"{players} players, port {port}, {seconds}s, {stepsPerMinute} steps a minute each");

        using var run = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));

        var bots = new List<Bot>(players);
        var joining = new List<Task>(players);

        var clock = Stopwatch.StartNew();

        for (int i = 0; i < players; i++)
        {
            var bot = new Bot($"{prefix}{i:D4}", port, stepsPerMinute, i, returning);

            bots.Add(bot);
            joining.Add(bot.RunAsync(run.Token));

            // Joining a thousand at once measures the accept queue rather than the game.
            // Spread out, this measures what it is meant to.
            if (i % 25 == 24) await Task.Delay(50, CancellationToken.None).ConfigureAwait(false);
        }

        await Task.WhenAll(joining).ConfigureAwait(false);

        clock.Stop();

        Report(bots, clock.Elapsed);

        return bots.Count(b => b.IsIn) == players ? 0 : 1;
    }

    private static void Report(List<Bot> bots, TimeSpan elapsed)
    {
        int arrived = bots.Count(b => b.IsIn);
        long steps = bots.Sum(b => (long)b.Steps);
        long answers = bots.Sum(b => (long)b.Answers);
        long received = bots.Sum(b => (long)b.Received);
        long bytes = bots.Sum(b => b.Bytes);

        List<double> waits = [.. bots.SelectMany(b => b.Waits).OrderBy(w => w)];
        List<double> joins = [.. bots.Where(b => b.JoinMilliseconds > 0).Select(b => b.JoinMilliseconds).OrderBy(j => j)];

        Console.WriteLine();
        Console.WriteLine($"  {arrived} of {bots.Count} got in, over {elapsed.TotalSeconds:F1}s");

        if (bots.FirstOrDefault(b => b.Failure is not null)?.Failure is { } why)
            Console.WriteLine($"  first failure: {why}");

        Console.WriteLine($"  joining      {At(joins, 0.5):F0} / {At(joins, 0.95):F0} / {At(joins, 1.0):F0} ms  (median, 95th, worst)");
        Console.WriteLine($"  steps        {steps} asked, {answers} answered");
        Console.WriteLine($"  a step took  {At(waits, 0.5):F1} / {At(waits, 0.95):F1} / {At(waits, 0.99):F1} / {At(waits, 1.0):F1} ms  (median, 95th, 99th, worst)");
        Console.WriteLine($"  messages     {received} in, {received / Math.Max(elapsed.TotalSeconds, 0.001):F0} a second across the crowd");
        Console.WriteLine($"  bytes        {bytes / 1024.0 / 1024.0:F1} MiB, {bytes / Math.Max(elapsed.TotalSeconds, 0.001) / 1024.0:F0} KiB a second");

        // The number that says whether this scales: what one player costs everybody else.
        if (arrived > 0)
            Console.WriteLine(
                $"  per player   {received / (double)arrived / Math.Max(elapsed.TotalSeconds, 0.001):F1} messages a second in, " +
                $"{bytes / (double)arrived / Math.Max(elapsed.TotalSeconds, 0.001) / 1024.0:F1} KiB a second");
    }

    private static double At(List<double> sorted, double fraction) =>
        sorted.Count == 0 ? 0 : sorted[Math.Clamp((int)(fraction * (sorted.Count - 1)), 0, sorted.Count - 1)];

    private static int? Number(string[] args, string name) =>
        Text(args, name) is { } text && int.TryParse(text, out int value) ? value : null;

    private static string? Text(string[] args, string name)
    {
        int at = Array.IndexOf(args, name);

        return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
    }
}

/// <summary>One player, on one socket, doing the one thing every player does.</summary>
public sealed class Bot(string name, int port, int stepsPerMinute, int seed, bool returning = false)
{
    private static readonly Direction[] Ways =
        [Direction.Up, Direction.Down, Direction.Left, Direction.Right];

    private readonly Random _rng = new(seed + 1);

    /// <summary>How long each step took to be answered, in milliseconds.</summary>
    public List<double> Waits { get; } = [];

    public bool IsIn { get; private set; }

    public double JoinMilliseconds { get; private set; }

    public int Steps { get; private set; }

    public int Answers { get; private set; }

    public int Received { get; private set; }

    public long Bytes { get; private set; }

    public string? Failure { get; private set; }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var socket = new TcpClient { NoDelay = true };

            await socket.ConnectAsync("127.0.0.1", port, cancellationToken).ConfigureAwait(false);

            await using NetworkStream stream = socket.GetStream();

            var channel = new MessageChannel(stream);
            var clock = Stopwatch.StartNew();

            // Registering rather than logging in, because a crowd of a thousand needs a
            // thousand accounts and making them is part of what is being measured.
            NetMessage door = returning
                ? new LoginRequest(name, "a-good-password")
                : new RegisterRequest(name, "a-good-password");

            await channel.SendAsync(door, cancellationToken).ConfigureAwait(false);

            double asked = -1;

            // Everything the server says, read as fast as it says it. A bot that reads
            // slowly is measuring itself.
            var reading = Task.Run(async () =>
            {
                while (await channel.ReceiveAsync(cancellationToken).ConfigureAwait(false) is { } message)
                {
                    Received++;
                    Bytes += 64;   // A frame's own size is not exposed; this is the floor.

                    switch (message)
                    {
                        case Welcome:
                            IsIn = true;
                            JoinMilliseconds = clock.Elapsed.TotalMilliseconds;
                            break;

                        case AuthFailed failed:
                            Failure ??= failed.Reason;
                            return;

                        case PlayerMoved:
                        case MapChanged:
                            if (asked >= 0)
                            {
                                Waits.Add(clock.Elapsed.TotalMilliseconds - asked);
                                Answers++;
                                asked = -1;
                            }

                            break;
                    }
                }
            }, cancellationToken);

            for (int spin = 0; spin < 400 && !IsIn && !cancellationToken.IsCancellationRequested; spin++)
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);

            if (!IsIn) Failure ??= "never got a welcome";

            int millisecondsBetweenSteps = Math.Max(1, 60_000 / Math.Max(stepsPerMinute, 1));

            while (!cancellationToken.IsCancellationRequested && IsIn)
            {
                await Task.Delay(millisecondsBetweenSteps, cancellationToken).ConfigureAwait(false);

                asked = clock.Elapsed.TotalMilliseconds;

                await channel
                    .SendAsync(new MoveRequest(Ways[_rng.Next(Ways.Length)]), cancellationToken)
                    .ConfigureAwait(false);

                Steps++;
            }

            await reading.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The run ending is not a failure.
        }
        catch (Exception ex)
        {
            Failure ??= ex.GetType().Name + ": " + ex.Message;
        }
    }
}
