using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     Writes a screenshot to user data every <see cref="CCVars.CMUScreenshotInterval"/> seconds while
///     that cvar is above zero.
/// </summary>
/// <remarks>
///     <para>
///     Exists because <see cref="CmuScreenshotCommand"/> still needs a person to type it: the client's
///     console is drawn in-game and cannot be written to from outside the process. Set the interval on
///     the launch line instead - <c>--cvar cmu.screenshot_interval=5</c> - and the capture loop needs
///     nobody at all, which is the difference between iterating on a UI change in seconds and waiting
///     on a round trip for every colour value.
///     </para>
///     <para>
///     Always overwrites the same file. This runs unattended and a fresh file per capture would fill
///     the data directory during a long session.
///     </para>
/// </remarks>
public sealed class CmuAutoScreenshotSystem : EntitySystem
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IResourceManager _resource = default!;

    public const string FileName = "cmu-auto.png";

    private TimeSpan _next;
    private bool _busy;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var interval = _cfg.GetCVar(CCVars.CMUScreenshotInterval);
        if (interval <= 0f)
            return;

        if (_timing.RealTime < _next)
            return;

        // Schedule the next one before capturing, not after: the capture is asynchronous and a frame
        // can be slow, so scheduling afterwards lets a backlog build up and fire back to back.
        _next = _timing.RealTime + TimeSpan.FromSeconds(interval);

        if (_busy)
            return;

        _busy = true;

        _clyde.Screenshot(ScreenshotType.Final, image =>
        {
            try
            {
                var stream = _resource.UserData.OpenWrite(new ResPath(FileName).ToRootedPath());
                image.SaveAsPng(stream);
                stream.Dispose();
            }
            catch
            {
                // A failed capture is not worth interrupting the game for - it is a dev aid, and the
                // caller finds out by the file not changing.
            }
            finally
            {
                _busy = false;
            }
        });
    }
}
