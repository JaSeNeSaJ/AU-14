using System;
using System.IO;
using System.Threading.Tasks;
using Robust.Client.Graphics;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.IoC;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     Writes the client's own framebuffer to a PNG in user data.
/// </summary>
/// <remarks>
///     <para>
///     The engine exposes <see cref="IClyde.Screenshot"/> but wires no console command to it, so there
///     was no way to capture the game short of screen-grabbing the desktop - which copies whatever
///     pixels sit at the window's coordinates rather than the window itself, cannot raise a background
///     window to make that safe, and will happily capture anything else the user has open. This
///     captures the render target and nothing else.
///     </para>
///     <para>
///     Written to a fixed path by default so a caller can find it without being told the name, and
///     overwritten each time so the directory does not fill up during a UI iteration loop.
///     </para>
/// </remarks>
public sealed class CmuScreenshotCommand : LocalizedCommands
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IResourceManager _resource = default!;

    public const string DefaultName = "cmu-screenshot.png";

    public override string Command => "cmuscreenshot";

    public override string Description =>
        "Saves the current frame to user data as a PNG, optionally after a delay.";

    public override string Help =>
        "Usage: cmuscreenshot [delaySeconds] [filename.png] - the delay is there to close the console first.";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        // The command is typed into the console, so without a delay the console is always in the
        // shot - covering the top third of the screen, which is usually the part being looked at.
        // A delay lets the caller press the console key again before the frame is grabbed.
        var delay = 0d;
        var rest = args;

        if (args.Length > 0 && double.TryParse(args[0], out var parsed))
        {
            delay = Math.Clamp(parsed, 0d, 30d);
            rest = args[1..];
        }

        var name = rest.Length > 0 ? rest[0] : DefaultName;

        if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            name += ".png";

        // Reject anything with path separators rather than sanitising: this only ever needs to write
        // one file into one directory, and a command that can be talked into writing elsewhere is a
        // worse problem than a command that refuses an odd name.
        if (name.Contains('/') || name.Contains('\\') || name.Contains(".."))
        {
            shell.WriteError("File name must not contain a path.");
            return;
        }

        try
        {
            if (delay > 0)
            {
                shell.WriteLine($"Capturing in {delay:0.#}s - close the console now.");
                await Task.Delay(TimeSpan.FromSeconds(delay));
            }

            // Final, not BeforeUI: the point is to capture what the player actually sees, chrome
            // included.
            var image = await _clyde.ScreenshotAsync(ScreenshotType.Final);

            var path = new ResPath(name).ToRootedPath();
            await using var stream = _resource.UserData.OpenWrite(path);
            image.SaveAsPng(stream);

            shell.WriteLine($"Saved screenshot to user data: {name}");
        }
        catch (Exception e)
        {
            shell.WriteError($"Screenshot failed: {e.Message}");
        }
    }
}
