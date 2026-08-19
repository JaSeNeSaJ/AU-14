using System;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     Opens <see cref="CrtChatMockWindow"/> once at startup when <see cref="CCVars.CMUChatMock"/> is
///     set, so the mock can be looked at without anyone typing the console command.
/// </summary>
/// <remarks>
///     The counterpart to <see cref="CmuAutoScreenshotSystem"/>: that one removes the person from the
///     capture, this one removes them from the opening. Together they make a design change visible
///     end to end from the launch line.
/// </remarks>
public sealed class CmuChatMockAutoOpenSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    ///     Wait before opening. The UI root exists well before the lobby has finished building
    ///     itself, and a window opened into that gap gets buried by whatever is added afterwards.
    /// </summary>
    private static readonly TimeSpan OpenDelay = TimeSpan.FromSeconds(3);

    private bool _done;
    private CrtChatMockWindow? _window;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_done)
            return;

        var spec = _cfg.GetCVar(CCVars.CMUChatMock);
        if (string.IsNullOrWhiteSpace(spec))
            return;

        if (_timing.RealTime < OpenDelay)
            return;

        _done = true;

        var parts = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Defaults are the chosen pair - underline selection, band input - so a bare
        // `cmu.chatmock=1` shows what was picked rather than what happened to be first in the enum.
        var selection = parts.Length > 0
            ? parts[0].ToLowerInvariant() switch
            {
                "inverted" => CrtChatMockWindow.SelectionStyle.Inverted,
                "s4" => CrtChatMockWindow.SelectionStyle.Surface4,
                _ => CrtChatMockWindow.SelectionStyle.Underline,
            }
            : CrtChatMockWindow.SelectionStyle.Underline;

        var input = parts.Length > 1
            ? parts[1].ToLowerInvariant() switch
            {
                "flat" => CrtChatMockWindow.InputStyle.Flat,
                "chip" => CrtChatMockWindow.InputStyle.Chip,
                _ => CrtChatMockWindow.InputStyle.Band,
            }
            : CrtChatMockWindow.InputStyle.Band;

        _window = new CrtChatMockWindow(selection, input);
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
        _window.MoveToFront();
    }
}
