using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

namespace Content.Client.Administration.UI.CustomControls;

public sealed class AdminLogLabel : PanelContainer
{
    private static readonly Color BackgroundColor = Color.FromHex("#1B1B1E");
    private static readonly Color BorderColor = Color.FromHex("#303645");
    private static readonly Color HeaderColor = Color.FromHex("#C8D0DB");
    private static readonly Color SeparatorColor = Color.FromHex("#252A33");
    private static readonly Color TypeColor = Color.FromHex("#9DBCE6");

    public AdminLogLabel(ref SharedAdminLog log, HSeparator separator)
    {
        Log = log;
        Separator = separator;
        TimeText = $"{log.Date:HH:mm:ss}";
        ImpactText = log.Impact.ToString();
        TypeText = log.Type.ToString();

        HorizontalExpand = true;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = BackgroundColor,
            BorderColor = GetImpactColor(log.Impact),
            BorderThickness = new Thickness(4, 1, 1, 1),
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 6,
            ContentMarginBottomOverride = 6,
        };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            SeparationOverride = 4,
        };

        var header = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 8,
        };

        header.AddChild(new Label
        {
            Text = TimeText,
            ClipText = true,
            FontColorOverride = HeaderColor,
        });

        header.AddChild(new Label
        {
            Text = ImpactText,
            ClipText = true,
            FontColorOverride = GetImpactColor(log.Impact),
        });

        header.AddChild(new Label
        {
            Text = TypeText,
            ClipText = true,
            HorizontalExpand = true,
            FontColorOverride = TypeColor,
        });

        var message = new RichTextLabel
        {
            HorizontalExpand = true,
        };
        message.SetMessage(log.Message);

        root.AddChild(header);
        root.AddChild(message);

        AddChild(root);

        Separator.Color = SeparatorColor;
        OnVisibilityChanged += VisibilityChanged;
    }

    public new SharedAdminLog Log { get; }

    public HSeparator Separator { get; }

    public string TimeText { get; }

    public string ImpactText { get; }

    public string TypeText { get; }

    private static Color GetImpactColor(LogImpact impact)
    {
        return impact switch
        {
            LogImpact.Extreme => Color.FromHex("#FF6B5F"),
            LogImpact.High => Color.FromHex("#FF9C5A"),
            LogImpact.Medium => Color.FromHex("#D7B95E"),
            LogImpact.Low => Color.FromHex("#8DBA75"),
            _ => BorderColor,
        };
    }

    private void VisibilityChanged(Control control)
    {
        Separator.Visible = Visible;
    }

    [System.Obsolete]
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        OnVisibilityChanged -= VisibilityChanged;
    }
}
