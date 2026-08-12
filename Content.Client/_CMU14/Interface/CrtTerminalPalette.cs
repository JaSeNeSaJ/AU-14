using Robust.Shared.Maths;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     A proposed replacement ladder for the green phosphor look.
/// </summary>
/// <remarks>
///     <para>
///     The shipped palette derives all eighteen of its colours from one hue and packs every surface
///     between <c>#000906</c> and <c>#032314</c> - about five percent of the luminance range, all of
///     it near-black. Three problems follow from that one fact, and all three showed up in practice:
///     surfaces cannot be told apart by fill, so every boundary has to be a border, which is what
///     produces the box-in-box look; nothing can signal danger, because a fill can only ever be the
///     theme colour brighter; and a scanline has no luminance to modulate, so it is either invisible
///     or a colour cast.
///     </para>
///     <para>
///     This ladder fixes the cause rather than the symptoms. Four surface steps, each visibly lighter
///     than the last, so a panel can sit inside a panel and be read as separate without a rule
///     between them. Text is genuinely bright - an Aliens terminal is high contrast, dim green on
///     black is a different look entirely. Caution and alert are off-hue on purpose.
///     </para>
/// </remarks>
public static class CrtTerminalPalette
{
    /// <summary>Behind everything. Not pure black - a phosphor tube never is.</summary>
    public static readonly Color Void = Color.FromHex("#040705");

    /// <summary>Window body.</summary>
    public static readonly Color Surface0 = Color.FromHex("#071009");

    /// <summary>A section or group within the body.</summary>
    public static readonly Color Surface1 = Color.FromHex("#0D1A12");

    /// <summary>One row inside a section.</summary>
    public static readonly Color Surface2 = Color.FromHex("#142519");

    /// <summary>Header and status strips; selected rows.</summary>
    public static readonly Color Surface3 = Color.FromHex("#1C3323");

    /// <summary>Hairline, for the few places a rule still says something a fill cannot.</summary>
    public static readonly Color Line = Color.FromHex("#2A5238");

    /// <summary>Field labels and other secondary text.</summary>
    public static readonly Color TextDim = Color.FromHex("#4E9C6B");

    /// <summary>Body text.</summary>
    public static readonly Color Text = Color.FromHex("#8FE9AE");

    /// <summary>Headings and values worth reading first.</summary>
    public static readonly Color TextBright = Color.FromHex("#C9FFDC");

    /// <summary>The phosphor itself. Bars, pips, active states.</summary>
    public static readonly Color Accent = Color.FromHex("#46FF8E");

    public static readonly Color Caution = Color.FromHex("#FFB454");
    public static readonly Color Alert = Color.FromHex("#FF4E5E");
}
