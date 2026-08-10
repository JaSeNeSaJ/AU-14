using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared.Chat;
using Content.Shared.Radio;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;

namespace Content.Client.UserInterface.Systems.Chat.Controls;

public sealed class ChannelSelectorButton : ChatPopupButton<ChannelSelectorPopup>
{
    public event Action<ChatSelectChannel>? OnChannelSelect;

    public ChatSelectChannel SelectedChannel { get; private set; }

    /// <summary>
    ///     How much of the chat input row the channel popup spans, left-aligned with it.
    /// </summary>
    private const float PopupWidthFraction = 0.5f;

    public ChannelSelectorButton()
    {
        Name = "ChannelSelector";

        // Match the channel buttons in the popup this opens. Without it this stayed a plain NanoUI
        // button sitting next to a strip of CRT ones. Set here rather than via CrtLobbyTheme, which
        // returns early on a ChatBox and so never walks the chat's controls at all.
        if (StyleNano.CrtUiEnabled)
        {
            AddStyleClass(StyleNano.StyleClassCrtButton);

            // The CRT button box is far tighter than the NanoUI one it replaces - 3px of vertical
            // padding and an 8px label - so on its own it shrinks the whole input row around it.
            // FontOverride rather than a style class: the CRT rule sizes every button label through
            // a single parent-child selector that a competing class would only tie with.
            Label.FontOverride = StyleNano.GetCrtFont(IoCManager.Resolve<IResourceCache>(), 12);
        }

        Popup.Selected += OnChannelSelected;

        if (Popup.FirstChannel is { } firstSelector)
        {
            Select(firstSelector);
        }
    }

    protected override UIBox2 GetPopupPosition()
    {
        // Sit on top of the whole input row, not below this button. The input row is the bottom
        // edge of the chat, so opening downwards put the list off the panel entirely - and the old
        // box was only this button's width, so the channels spilled out of it.
        //
        // Anchor to the ChatInputBox itself rather than to Parent: Parent is the BoxContainer
        // *inside* it, which is already inset by the input row's stylebox margins.
        var row = FindInputRow();

        // Measure before positioning: the top edge is derived from the popup's height, and
        // Popup.MeasureOverride floors at whatever size the previous Open() passed, so a guess made
        // here would stick permanently.
        Popup.Measure(Vector2Helpers.Infinity);

        // Half the input row, not all of it - but never narrower than the channels actually need,
        // so the in-round list (up to nine channels, against three or four in the lobby) still fits
        // rather than squeezing every label into a sliver.
        var width = MathF.Max(row.Width * PopupWidthFraction, Popup.DesiredSize.X);

        // Tall enough to cover the input row outright. The popup's bottom edge is the row's *bottom*
        // edge, so it sits over the bar rather than perching on top of it and leaving the channel
        // button and placeholder text peeking out underneath.
        var height = MathF.Max(Popup.DesiredSize.Y, row.Height);

        return UIBox2.FromDimensions(
            new Vector2(row.GlobalPosition.X, row.GlobalPosition.Y + row.Height - height),
            new Vector2(width, height));
    }

    private Control FindInputRow()
    {
        for (var parent = Parent; parent != null; parent = parent.Parent)
        {
            if (parent is ChatInputBox)
                return parent;
        }

        return Parent ?? this;
    }

    private void OnChannelSelected(ChatSelectChannel channel)
    {
        Select(channel);
    }

    public void Select(ChatSelectChannel channel)
    {
        if (Popup.Visible)
        {
            Popup.Close();
        }

        if (SelectedChannel == channel)
            return;
        SelectedChannel = channel;
        OnChannelSelect?.Invoke(channel);
    }

    public static string ChannelSelectorName(ChatSelectChannel channel)
    {
        return Loc.GetString($"hud-chatbox-select-channel-{channel}");
    }

    public Color ChannelSelectColor(ChatSelectChannel channel)
    {
        return channel switch
        {
            ChatSelectChannel.Radio => Color.LimeGreen,
            ChatSelectChannel.LOOC => Color.MediumTurquoise,
            ChatSelectChannel.OOC => Color.LightSkyBlue,
            ChatSelectChannel.Dead => Color.MediumPurple,
            ChatSelectChannel.Admin => Color.HotPink,
            ChatSelectChannel.Mentor => Color.Orange,
            _ => Color.DarkGray
        };
    }

    public void UpdateChannelSelectButton(ChatSelectChannel channel, RadioChannelPrototype? radio)
    {
        Text = radio != null ? Loc.GetString(radio.Name) : ChannelSelectorName(channel);
        Modulate = radio?.Color ?? ChannelSelectColor(channel);
    }
}
