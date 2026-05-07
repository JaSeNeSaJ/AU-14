using Content.Shared.Chat;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat.Widgets;

public sealed class ChatLogPanel : PanelContainer
{
    public const int MaxEntries = 2500;

    private readonly ScrollContainer _scroll;
    private readonly BoxContainer _rows;
    private readonly Button _scrollToLatest;
    private bool _isAtBottom = true;
    private int _pendingScrollToBottomFrames;

    public int EntryCount => _rows.ChildCount;

    public ChatLogPanel()
    {
        HorizontalExpand = true;
        VerticalExpand = true;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#07090b"),
            ContentMarginLeftOverride = 3,
            ContentMarginRightOverride = 3,
            ContentMarginTopOverride = 3,
            ContentMarginBottomOverride = 3
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 3,
            HorizontalExpand = true,
            VerticalExpand = true
        };
        AddChild(root);

        _scroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            HScrollEnabled = false,
            VScrollEnabled = true,
            ReserveScrollbarSpace = true
        };
        _scroll.OnScrolled += UpdateScrollState;
        root.AddChild(_scroll);

        _rows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 0,
            HorizontalExpand = true,
            VerticalExpand = false
        };
        _scroll.AddChild(_rows);

        _scrollToLatest = new Button
        {
            Text = "Scroll to latest",
            Visible = false,
            HorizontalAlignment = HAlignment.Center,
            MinWidth = 150,
            StyleClasses = { OutputPanel.StyleClassOutputPanelScrollDownButton }
        };
        _scrollToLatest.OnPressed += _ => ScrollToBottom();
        root.AddChild(_scrollToLatest);
    }

    public ChatMessageRow AddMessage(ChatMessage message, FormattedMessage formatted, Color color, Color? accentOverride = null, int? fontSize = null)
    {
        var row = new ChatMessageRow(message, formatted, color, accentOverride, fontSize);
        _rows.AddChild(row);

        while (_rows.ChildCount > MaxEntries)
        {
            _rows.RemoveChild(0);
        }

        if (_isAtBottom)
            QueueScrollToBottom();

        return row;
    }

    public void Clear()
    {
        while (_rows.ChildCount > 0)
        {
            _rows.RemoveChild(0);
        }

        _isAtBottom = true;
        _scrollToLatest.Visible = false;
        QueueScrollToBottom();
    }

    public void ScrollToBottom()
    {
        _isAtBottom = true;
        _scrollToLatest.Visible = false;
        QueueScrollToBottom();
    }

    public void RefreshLayout(bool forceScrollToBottom = false)
    {
        foreach (var child in _rows.Children)
        {
            child.InvalidateMeasure();
        }

        _rows.InvalidateMeasure();
        _scroll.InvalidateMeasure();

        if (forceScrollToBottom || _isAtBottom)
            ScrollToBottom();
        else
            UpdateScrollState();
    }

    protected override void Resized()
    {
        base.Resized();
        RefreshLayout();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_pendingScrollToBottomFrames <= 0)
            return;

        _scroll.VScrollTarget = float.MaxValue;
        _scroll.VScroll = float.MaxValue;
        _scrollToLatest.Visible = false;
        _pendingScrollToBottomFrames--;
    }

    private void QueueScrollToBottom()
    {
        // Rebuilt tab contents can take multiple layout passes before ScrollContainer
        // knows its final max value, so keep snapping for a few frames.
        _pendingScrollToBottomFrames = 4;
    }

    private void UpdateScrollState()
    {
        var scrollBottom = _scroll.VScrollTarget + Height + 12;
        var contentHeight = _rows.DesiredSize.Y;
        _isAtBottom = scrollBottom >= contentHeight;
        _scrollToLatest.Visible = !_isAtBottom;
    }
}
