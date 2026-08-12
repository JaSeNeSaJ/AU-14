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
    ///     Strength of the CRT scanline effect, 0 to 1. Currently only read by the
    ///     <c>cmu_crt</c> test window, which exists to tune the look in isolation before it is
    ///     applied to any real surface.
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
    ///     Seconds between roll-bar passes. The bar crosses in about a tenth of this, so most of the
    ///     period is quiet - it is meant to be noticed occasionally, not watched.
    /// </summary>
    public static readonly CVarDef<float> CMUCrtEffectRollPeriod =
        CVarDef.Create("cmu.crt_effect_roll_period", 19f, CVar.CLIENTONLY | CVar.ARCHIVE);

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

    /// <summary>
    ///     How much of the control, per side, is casing rather than glass. The tube face is inset by
    ///     this much and the barrel curve then rounds its corners, so the screen is a screen-shaped
    ///     region inside a bezel rather than a rectangle with warped contents. Zero puts the glass
    ///     flush to the control edge; the corners still round, because the curve does that.
    /// </summary>
}
