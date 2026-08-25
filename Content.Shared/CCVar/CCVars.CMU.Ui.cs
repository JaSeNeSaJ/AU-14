using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Draws the vote popup at a larger scale - wider options, taller rows. Aimed at high
    ///     resolutions and ultrawides, where the default sizing leaves the vote small and hard to
    ///     read against a lot of screen.
    /// </summary>
    public static readonly CVarDef<bool> CMUVoteUiLarge =
        CVarDef.Create("cmu.vote_ui_large", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Playtime in minutes below which the guidebook opens itself in the lobby, or 0 to never do
    ///     it. Was a hardcoded 60, which means every fresh client - including a second test client on
    ///     the same machine - opens with the guidebook covering the middle of the screen.
    /// </summary>
    public static readonly CVarDef<int> CMUGuidebookAutoOpenPlaytime =
        CVarDef.Create("cmu.guidebook_auto_open_minutes", 60, CVar.CLIENTONLY);

    /// <summary>
    ///     Seconds between automatic screenshots, or 0 for off. A development aid: the client's
    ///     console cannot be written to from outside the process, so a screenshot command alone still
    ///     needs a person to type it. This can be set from the launch line
    ///     (<c>--cvar cmu.screenshot_interval=5</c>), which closes the loop for anyone iterating on UI
    ///     without being able to see the screen.
    /// </summary>
    /// <remarks>
    ///     Deliberately not ARCHIVE: an archived value would persist into a player's config and keep
    ///     writing files forever, and a changed default would never reach anyone who already has one.
    /// </remarks>
    public static readonly CVarDef<float> CMUScreenshotInterval =
        CVarDef.Create("cmu.screenshot_interval", 0f, CVar.CLIENTONLY);

    /// <summary>
    ///     Opens the proposed-chat mock window on startup. Empty for off; otherwise
    ///     <c>&lt;selection&gt;,&lt;input&gt;</c> - selection one of <c>inverted|s4|underline</c>,
    ///     input one of <c>band|flat|chip</c>. For example
    ///     <c>--cvar cmu.chatmock=inverted,band</c>.
    /// </summary>
    /// <remarks>
    ///     Same reason as <see cref="CMUScreenshotInterval"/>: <c>cmu_chatmock</c> alone still needs a
    ///     person to type it, because the client's console is drawn in-game and cannot be written to
    ///     from outside the process. Pairing this with the screenshot interval makes the whole
    ///     look-at-it-and-change-it loop run without anyone watching.
    /// </remarks>
    public static readonly CVarDef<string> CMUChatMock =
        CVarDef.Create("cmu.chatmock", "", CVar.CLIENTONLY);

    /// <summary>
    ///     Opens one or more of the small CRT panels on startup so they can be looked at without
    ///     anyone clicking through the lobby to reach them. Comma-separated, any of <c>join</c>,
    ///     <c>staffhelp</c>, <c>vote</c>, <c>ready</c> - for example
    ///     <c>--cvar cmu.panel_preview=join,staffhelp</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Same reason as <see cref="CMUChatMock"/>. These three are the panels that share
    ///     <c>CmuPanelMetrics</c>, and until now two of them could only be reached by clicking a
    ///     lobby button and the third needed an actual vote to be running - so a change to their
    ///     shared measurements could not be checked without a person driving the client.
    ///     </para>
    ///     <para>
    ///     The vote preview is a fabricated vote with votes already cast, which is the state the real
    ///     popup is hardest to see: the option bars only draw once something has been voted for. The
    ///     ready preview shows both states of the lobby's ready toggle together - that control only
    ///     exists before a round starts, so on a mid-round server there is otherwise no way to look
    ///     at it, and the thing worth judging is the contrast between the two states rather than
    ///     either one alone.
    ///     </para>
    /// </remarks>
    public static readonly CVarDef<string> CMUPanelPreview =
        CVarDef.Create("cmu.panel_preview", "", CVar.CLIENTONLY);

    /// <summary>
    ///     Console command(s) to run once, a few seconds after connecting. Semicolon-separated.
    ///     For example <c>--cvar cmu.startup_command=golobby</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The last piece of the look-at-it-without-a-person loop, alongside
    ///     <see cref="CMUScreenshotInterval"/> and <see cref="CMUPanelPreview"/>. The client's
    ///     console is drawn in-game and cannot be written to from outside the process, so anything
    ///     reachable only by a console command was unreachable when nobody was at the keyboard.
    ///     </para>
    ///     <para>
    ///     The case that prompted it: the dev config boots straight into a running round, so the
    ///     pre-round lobby - and everything that only exists there, like the ready toggle and the
    ///     countdown - could not be looked at. <c>golobby</c> has always existed on the server side.
    ///     </para>
    ///     <para>
    ///     Not ARCHIVE, deliberately: a stored value would run a command at every launch forever.
    ///     </para>
    /// </remarks>
    public static readonly CVarDef<string> CMUStartupCommand =
        CVarDef.Create("cmu.startup_command", "", CVar.CLIENTONLY);

    /// <summary>
    ///     Where the player has dragged the lobby's round clock, as a fraction of the free space
    ///     around it - 0.5, 0.5 being centred. Negative means untouched, so the clock takes its
    ///     default place in the gap between the action panel and the server-info screen.
    /// </summary>
    /// <remarks>
    ///     A fraction rather than pixels so it survives a resolution change or the right-hand panel
    ///     being collapsed, either of which would otherwise leave the clock off-screen or on top of
    ///     something. ARCHIVE: having to re-place it every session would defeat the point of moving
    ///     it at all.
    /// </remarks>
    public static readonly CVarDef<float> CMULobbyClockX =
        CVarDef.Create("cmu.lobby_clock_x", -1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> CMULobbyClockY =
        CVarDef.Create("cmu.lobby_clock_y", -1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Whether the round clock is folded into the action column instead of floating over the
    ///     lobby. ARCHIVE so it doubles as the preference; does not clear the dragged position.
    /// </summary>
    public static readonly CVarDef<bool> CMULobbyClockMinimized =
        CVarDef.Create("cmu.lobby_clock_minimized", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Draw chat in a plain proportional face instead of the terminal one, leaving the rest of
    ///     the CRT theme alone. An accessibility option, not a cosmetic one.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     The uavOsd face the CRT theme uses is all-caps by design - it has no lowercase glyphs at
    ///     all - so every message in the log renders shouting whatever the sender typed. That is
    ///     fine for the handful of words on a piece of chrome and hard work for a conversation,
    ///     especially at the 8px the terminal look wants. This swaps the chat's font only.
    ///     </para>
    ///     <para>
    ///     ARCHIVE, because someone who needs it needs it every session. Deliberately independent of
    ///     <see cref="CrtUiEnabled"/>: turning the whole theme off to make chat readable is exactly
    ///     the trade this exists to avoid.
    ///     </para>
    /// </remarks>
    public static readonly CVarDef<bool> CMUChatReadableFont =
        CVarDef.Create("cmu.chat_readable_font", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Overall strength of the CRT effect - scanlines, grain and the roll bar together, 0 to 1.
    ///     The individual settings below shape each one; this scales the lot.
    /// </summary>
    public static readonly CVarDef<float> CMUCrtEffectIntensity =
        CVarDef.Create("cmu.crt_effect_intensity", 0.5f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Distance in pixels between scanlines. The single most important number in the effect:
    ///     below about 3 the line and the gap stop resolving separately and the whole thing reads as
    ///     a flat darkening rather than as lines.
    /// </summary>
    public static readonly CVarDef<float> CMUCrtEffectPitch =
        CVarDef.Create("cmu.crt_effect_pitch", 3f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Strength of the animated per-pixel grain, 0 to 1.
    /// </summary>
    public static readonly CVarDef<float> CMUCrtEffectStatic =
        CVarDef.Create("cmu.crt_effect_static", 0.35f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Seconds between roll-bar passes. Two minutes: the sweep takes about two seconds, so the
    ///     bar is on screen for under two percent of the time and is genuinely a thing you catch
    ///     rather than a thing you watch.
    /// </summary>
    /// <remarks>
    ///     Was 19 seconds, which is often enough that the eye starts waiting for it - and an effect
    ///     you are waiting for has stopped being scenery and become a distraction on a screen people
    ///     read. Rare is the whole point of it.
    /// </remarks>
    public static readonly CVarDef<float> CMUCrtEffectRollPeriod =
        CVarDef.Create("cmu.crt_effect_roll_period", 120f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Seconds one roll-bar crossing takes.</summary>
    public static readonly CVarDef<float> CMUCrtEffectRollSweep =
        CVarDef.Create("cmu.crt_effect_roll_sweep", 2.1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Roll-bar half-height, as a fraction of the surface.</summary>
    public static readonly CVarDef<float> CMUCrtEffectRollHeight =
        CVarDef.Create("cmu.crt_effect_roll_height", 0.045f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Peak horizontal shear inside the roll bar, as a fraction of width. This is the effect:
    ///     the band moves the image rather than lighting it up.
    /// </summary>
    public static readonly CVarDef<float> CMUCrtEffectRollDisplace =
        CVarDef.Create("cmu.crt_effect_roll_displace", 0.053f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     How much light the roll bar adds. Zero by default: the shear carries the effect on its
    ///     own, and any light the band adds is a moving bright patch on a surface being read.
    /// </summary>
    public static readonly CVarDef<float> CMUCrtEffectRollLift =
        CVarDef.Create("cmu.crt_effect_roll_lift", 0f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Barrel distortion. Bulges the picture toward the viewer like a real tube. Edge midpoints
    ///     stay pinned to the window, so the cost of raising this is rounded corners eating into the
    ///     picture - past about 0.15 they reach far enough in to clip content.
    /// </summary>
    public static readonly CVarDef<float> CMUCrtEffectCurvature =
        CVarDef.Create("cmu.crt_effect_curvature", 0.05f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>Radial corner darkening. A true gradient, unlike the old eight-rectangle version.</summary>
    public static readonly CVarDef<float> CMUCrtEffectVignette =
        CVarDef.Create("cmu.crt_effect_vignette", 0.35f, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    ///     Runs the CRT surface texture - scanlines and grain - over ordinary menus. Separate from
    ///     the effect's own settings because a prop terminal and a settings page want the same
    ///     texture at very different strengths, and because this is the one that can hurt
    ///     readability.
    /// </summary>
    public static readonly CVarDef<bool> CMUCrtMenuEffect =
        CVarDef.Create("cmu.crt_menu_effect", true, CVar.CLIENTONLY | CVar.ARCHIVE);
}
