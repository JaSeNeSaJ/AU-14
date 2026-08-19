using System.Numerics;
using Content.Client.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     A dead mock of the proposed chat layout, opened by <c>cmu_chatmock</c>.
/// </summary>
/// <remarks>
///     <para>
///     This exists because the real chat is the worst possible place to design one. Its surfaces are
///     written from four different places at runtime - <c>Modulate</c>, <c>PanelOverride</c>,
///     stylesheet rules that lose on specificity to stock rules on a deeper element type - so a value
///     changed here does not necessarily reach the screen there, and several rounds were spent tuning
///     rules that could never have applied. Every colour below is set on the control from
///     <see cref="CrtTerminalPalette"/>, so what is written is what draws. The single exception is the
///     message body font - <see cref="RichTextLabel"/> has no font override and can only be reached
///     through the stylesheet.
///     </para>
///     <para>
///     Built in code rather than XAML on purpose. The point is to move numbers - a padding, a step on
///     the ladder - and see the result; XAML adds a codegen step and a named-control dance to every
///     one of those edits without buying anything for a control that is never reused.
///     </para>
///     <para>
///     The layout it proposes: <b>one ground</b>. The chat's surround is <see cref="CrtTerminalPalette.Surface0"/>,
///     the darkest surface, so every band sits lighter on it rather than being framed by a lighter
///     wrapper - which is what the live build does, nesting Surface0 inside Surface1 inside the bands
///     and leaving a 9px lighter rim around the input bar that reads as a border though nothing is
///     stroked. <b>Only selection is drawn</b>: a resting tab has no fill at all, so no chip sits on a
///     strip sitting on a panel. <b>The channel is a prompt, not a chip</b>, which removes the last
///     nested box on the input row.
///     </para>
/// </remarks>
public sealed class CrtChatMockWindow : DefaultWindow
{
    /// <summary>Width of the chat block. The lobby's real chat measures ~552px, so this is life-size.</summary>
    private const int ChatWidth = 560;

    /// <summary>
    ///     Width of the message prefix column. ADMIN/SYS/OOC/RTO are different widths, so left to
    ///     themselves every message body starts at a different x - the log reads as ragged rather
    ///     than tabulated. A terminal aligns them.
    /// </summary>
    private const int PrefixColumnWidth = 54;

    /// <summary>
    ///     One size for the whole chat. The message bodies are pinned to 8 by
    ///     <c>StyleClassCrtRichText</c> - <see cref="RichTextLabel"/> takes its font only from the
    ///     stylesheet - so 8 is what everything else has to be if the panel is to read as one
    ///     terminal rather than chrome sitting on top of a log. The live chat has exactly this
    ///     mismatch: its channel chip is 12 while the text beside it is 8.
    /// </summary>
    /// <remarks>Must track <c>crtRichTextFont</c> in <c>StyleNanoCrt</c>, which is <c>uavOsd</c> 8.</remarks>
    private const int FontSize = 8;

    public enum SelectionStyle
    {
        /// <summary>Inverted accent block - the terminal convention, where selection is the cursor.</summary>
        Inverted,

        /// <summary>The gallery's Surface4 fill with bright text.</summary>
        Surface4,

        /// <summary>No fill; an accent rule under the label.</summary>
        Underline,
    }

    public enum InputStyle
    {
        /// <summary>Surface1 band with an accent prompt and caret.</summary>
        Band,

        /// <summary>No fill at all - the caret is the only affordance.</summary>
        Flat,

        /// <summary>Today's build: a Surface2 chip on a Surface3 bar.</summary>
        Chip,
    }

    public SelectionStyle Selection { get; }
    public InputStyle Input { get; }

    private readonly IResourceCache _resCache;

    public CrtChatMockWindow(SelectionStyle selection, InputStyle input)
    {
        Selection = selection;
        Input = input;
        _resCache = IoCManager.Resolve<IResourceCache>();

        Title = $"CRT CHAT MOCK  -  selection: {selection}  -  input: {input}";
        MinSize = new Vector2(ChatWidth + 120, 560);

        // A darker surround than the chat's own ground, so the "one ground" claim can actually be
        // judged. On a mid-tone backdrop every band looks fine; the whole argument is about what the
        // chat sits on.
        var backdrop = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Void },
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        Contents.AddChild(backdrop);

        var centre = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(30, 24),
            HorizontalAlignment = HAlignment.Center,
            VerticalExpand = true,
        };
        backdrop.AddChild(centre);

        centre.AddChild(BuildChat());
    }

    private Control BuildChat()
    {
        // THE ground. Everything below draws on this and nothing wraps it.
        var chat = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface0 },
            MinWidth = ChatWidth,
            VerticalExpand = true,
        };

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        chat.AddChild(column);

        column.AddChild(BuildTabs());
        column.AddChild(BuildLog());
        column.AddChild(BuildInput());

        return chat;
    }

    private Control BuildTabs()
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        row.AddChild(Tab("ALL", selected: true));
        row.AddChild(Tab("RADIO", selected: false));
        row.AddChild(Tab("COMMAND", selected: false));
        row.AddChild(new Control { HorizontalExpand = true });
        row.AddChild(Tab("SPLIT +", selected: false));

        return row;
    }

    /// <summary>
    ///     A resting tab is text on the ground - no fill, no box. That is the whole trick: with
    ///     nothing else filled, the selected tab cannot read as a chip stacked on a strip.
    /// </summary>
    private Control Tab(string text, bool selected)
    {
        var label = new Label
        {
            Text = text,
            FontOverride = StyleNano.GetCrtFont(_resCache, FontSize),
            FontColorOverride = selected switch
            {
                // Inverted: the label is punched out of the accent block behind it.
                true when Selection == SelectionStyle.Inverted => CrtTerminalPalette.Surface0,
                true => CrtTerminalPalette.TextBright,
                false => CrtTerminalPalette.TextDim,
            },
            Margin = new Thickness(15, 7),
        };

        if (!selected)
            return label;

        var box = new StyleBoxFlat();
        switch (Selection)
        {
            case SelectionStyle.Inverted:
                box.BackgroundColor = CrtTerminalPalette.Accent;
                break;
            case SelectionStyle.Surface4:
                box.BackgroundColor = CrtTerminalPalette.Surface4;
                break;
            case SelectionStyle.Underline:
                // The one variant that draws a rule. Transparent fill, accent edge on the bottom
                // only - included to be argued about, not because the theme wants another line.
                box.BackgroundColor = Color.Transparent;
                box.BorderColor = CrtTerminalPalette.Accent;
                box.BorderThickness = new Thickness(0, 0, 0, 2);
                break;
        }

        var panel = new PanelContainer { PanelOverride = box };
        panel.AddChild(label);
        return panel;
    }

    private Control BuildLog()
    {
        var log = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(15, 12),
            SeparationOverride = 5,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        // Prefix colours are the phosphor-compatible remap, shown as a proposal - this is still the
        // parked decision, and the layout holds whichever way it goes.
        log.AddChild(Message("ADMIN", CrtTerminalPalette.Alert, "login: localhost@JoeGenero"));
        log.AddChild(Message("SYS", CrtTerminalPalette.Caution,
            "Welcome to CMU! Read the rules, and ask for help in LOOC or OOC."));
        log.AddChild(Message("SYS", CrtTerminalPalette.Caution, "Restarting round..."));
        log.AddChild(Message("OOC", CrtTerminalPalette.TextDim, "anyone readying up?"));
        log.AddChild(Message("RTO", CrtTerminalPalette.Accent, "Alamo inbound, two mikes."));
        log.AddChild(Message("OOC", CrtTerminalPalette.TextDim,
            "Tip: you can put screwdrivers and cigarettes in an ear slot.", quiet: true));

        log.AddChild(new Control { VerticalExpand = true });
        return log;
    }

    /// <summary>
    ///     One row: a fixed-width prefix column and a wrapping body. No row fill - channel identity
    ///     is the prefix colour, which is the one thing the gallery is explicit about.
    /// </summary>
    private Control Message(string prefix, Color prefixColor, string body, bool quiet = false)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
        };

        row.AddChild(new Label
        {
            Text = prefix,
            FontOverride = StyleNano.GetCrtFont(_resCache, FontSize),
            FontColorOverride = prefixColor,
            MinWidth = PrefixColumnWidth,
            Align = Label.AlignMode.Right,
        });

        row.AddChild(new Control { MinWidth = 12 });

        // RichTextLabel rather than Label: a Label does not wrap, and the welcome line is the one
        // message here long enough to prove the column holds when it does.
        //
        // The style class is the one thing in this window that does come from the stylesheet, and it
        // has to: RichTextLabel resolves its font from the "font" style property and exposes no
        // FontOverride, so without this the message bodies fall back to the theme default and render
        // proportional next to mono prefixes - which is most of what "not a terminal" looks like.
        // Colour still comes from SetMessage, so the class only supplies the font and line height.
        var text = new RichTextLabel { HorizontalExpand = true };
        text.AddStyleClass(StyleNano.StyleClassCrtRichText);
        text.SetMessage(body, defaultColor: quiet ? CrtTerminalPalette.TextDim : CrtTerminalPalette.Text);
        row.AddChild(text);

        return row;
    }

    private Control BuildInput()
    {
        var fill = Input switch
        {
            InputStyle.Band => CrtTerminalPalette.Surface1,
            InputStyle.Flat => Color.Transparent,
            InputStyle.Chip => CrtTerminalPalette.Surface3,
            _ => CrtTerminalPalette.Surface1,
        };

        var bar = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = fill },
            HorizontalExpand = true,
        };

        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(15, 8),
            HorizontalExpand = true,
        };
        bar.AddChild(row);

        var prompt = new Label
        {
            Text = "OOC",
            FontOverride = StyleNano.GetCrtFont(_resCache, FontSize),
            FontColorOverride = CrtTerminalPalette.Accent,
            VerticalAlignment = VAlignment.Center,
        };

        if (Input == InputStyle.Chip)
        {
            // The variant kept for comparison: even on a bare ground the chip still reads as a box
            // on a band, which is the thing the other two are trying to get rid of.
            prompt.Margin = new Thickness(10, 3);
            var chip = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Surface2 },
                VerticalAlignment = VAlignment.Center,
            };
            chip.AddChild(prompt);
            row.AddChild(chip);
        }
        else
        {
            row.AddChild(prompt);
            row.AddChild(new Control { MinWidth = 10 });

            // A solid block, not a blinking one. A blinking caret is the obvious terminal flourish
            // and it is also continuous brightness modulation in the corner of the eye for a whole
            // round, which this project has already ruled out once.
            row.AddChild(new PanelContainer
            {
                PanelOverride = new StyleBoxFlat { BackgroundColor = CrtTerminalPalette.Accent },
                MinSize = new Vector2(6, 11),
                VerticalAlignment = VAlignment.Center,
            });
        }

        row.AddChild(new Control { MinWidth = 10 });

        row.AddChild(new Label
        {
            Text = "T TO TALK, TAB TO CYCLE CHANNELS.",
            FontOverride = StyleNano.GetCrtFont(_resCache, FontSize),
            FontColorOverride = CrtTerminalPalette.TextDim,
            VerticalAlignment = VAlignment.Center,
        });

        row.AddChild(new Control { HorizontalExpand = true });

        row.AddChild(new Label
        {
            Text = "*",
            FontOverride = StyleNano.GetCrtFont(_resCache, FontSize),
            FontColorOverride = CrtTerminalPalette.TextDim,
            VerticalAlignment = VAlignment.Center,
        });

        return bar;
    }
}
