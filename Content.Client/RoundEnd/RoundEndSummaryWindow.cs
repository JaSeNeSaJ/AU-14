using System.Linq;
using System.Numerics;
using Content.Client.Message;
using Content.Client.Stylesheets;
using Content.Shared.GameTicking;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.RoundEnd;

public sealed class RoundEndSummaryWindow : DefaultWindow
{
    private static readonly Color Background = Color.FromHex("#0D141B");
    private static readonly Color Card = Color.FromHex("#121F2B");
    private static readonly Color CardQuiet = Color.FromHex("#0F1923");
    private static readonly Color Border = Color.FromHex("#2A3C4C");
    private static readonly Color Text = Color.FromHex("#E7EEF3");
    private static readonly Color TextMuted = Color.FromHex("#95A5B4");
    private static readonly Color MarineBlue = Color.FromHex("#66B6FF");
    private static readonly Color MedicalCyan = Color.FromHex("#52D6D3");
    private static readonly Color WarningGold = Color.FromHex("#E9B96E");
    private static readonly Color TraumaRed = Color.FromHex("#F36D67");
    private static readonly Color OddityPurple = Color.FromHex("#B792FF");
    private static readonly Color SuccessGreen = Color.FromHex("#75D17A");

    private readonly IEntityManager _entityManager;

    public int RoundId;

    public RoundEndSummaryWindow(
        string gm,
        string roundEnd,
        TimeSpan roundTimeSpan,
        int roundId,
        RoundEndMessageEvent.RoundEndPlayerInfo[] info,
        RoundEndSummaryStats summaryStats,
        IEntityManager entityManager)
    {
        _entityManager = entityManager;

        MinSize = new Vector2(720, 640);
        SetSize = new Vector2(760, 680);
        Title = Loc.GetString("round-end-summary-window-title");

        RoundId = roundId;
        var roundEndTabs = new TabContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true
        };

        roundEndTabs.AddChild(MakeRoundEndSummaryTab(gm, roundEnd, roundTimeSpan, roundId, info, summaryStats));
        roundEndTabs.AddChild(MakePlayerManifestTab(info));

        Contents.AddChild(roundEndTabs);

        OpenCenteredRight();
        MoveToFront();
    }

    private BoxContainer MakeRoundEndSummaryTab(
        string gamemode,
        string roundEnd,
        TimeSpan roundDuration,
        int roundId,
        RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo,
        RoundEndSummaryStats summaryStats)
    {
        var roundEndSummaryTab = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = Loc.GetString("round-end-summary-window-round-end-summary-tab-title")
        };

        var roundEndSummaryContainerScrollbox = new ScrollContainer
        {
            VerticalExpand = true,
            Margin = new Thickness(10),
            HScrollEnabled = false,
        };
        var roundEndSummaryContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 10
        };

        roundEndSummaryContainer.AddChild(MakeReportHeader(gamemode, roundId, roundDuration, playersInfo));
        roundEndSummaryContainer.AddChild(MakeMetricGrid(roundId, roundDuration, playersInfo));
        roundEndSummaryContainer.AddChild(MakeStatSection(
            "round-end-summary-window-injury-ledger-title",
            "round-end-summary-window-injury-ledger-subtitle",
            summaryStats.InjuryStats));
        roundEndSummaryContainer.AddChild(MakeStatSection(
            "round-end-summary-window-oddities-title",
            "round-end-summary-window-oddities-subtitle",
            summaryStats.OddityStats));

        if (!string.IsNullOrEmpty(roundEnd))
            roundEndSummaryContainer.AddChild(MakeRoundEndTextPanel(roundEnd));

        roundEndSummaryContainerScrollbox.AddChild(roundEndSummaryContainer);
        roundEndSummaryTab.AddChild(roundEndSummaryContainerScrollbox);

        return roundEndSummaryTab;
    }

    private Control MakeReportHeader(
        string gamemode,
        int roundId,
        TimeSpan roundDuration,
        RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
    {
        var panel = MakePanel(CardQuiet, MarineBlue.WithAlpha(0.65f));
        var container = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(12),
            SeparationOverride = 4,
            HorizontalExpand = true
        };

        container.AddChild(new Label
        {
            Text = Loc.GetString("round-end-summary-window-after-action-title"),
            FontColorOverride = Text,
            StyleClasses = { StyleNano.StyleClassLabelHeadingBigger }
        });
        container.AddChild(new Label
        {
            Text = Loc.GetString(
                "round-end-summary-window-after-action-detail",
                ("roundId", roundId),
                ("gamemode", gamemode),
                ("duration", FormatDuration(roundDuration)),
                ("players", playersInfo.Length)),
            FontColorOverride = TextMuted,
            HorizontalExpand = true
        });

        panel.AddChild(container);
        return panel;
    }

    private Control MakeMetricGrid(
        int roundId,
        TimeSpan roundDuration,
        RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
    {
        var antags = playersInfo.Count(player => player.Antag);
        var observers = playersInfo.Count(player => player.Observer);
        var connected = playersInfo.Count(player => player.Connected);

        var grid = new GridContainer
        {
            Columns = 4,
            HSeparationOverride = 8,
            VSeparationOverride = 8,
            HorizontalExpand = true
        };

        grid.AddChild(MakeMetricCard(
            Loc.GetString("round-end-summary-window-metric-round"),
            Loc.GetString("round-end-summary-window-metric-round-value", ("roundId", roundId)),
            MarineBlue));
        grid.AddChild(MakeMetricCard(
            Loc.GetString("round-end-summary-window-metric-duration"),
            FormatDuration(roundDuration),
            WarningGold));
        grid.AddChild(MakeMetricCard(
            Loc.GetString("round-end-summary-window-metric-players"),
            playersInfo.Length.ToString(),
            MedicalCyan));
        grid.AddChild(MakeMetricCard(
            Loc.GetString("round-end-summary-window-metric-antags"),
            antags.ToString(),
            TraumaRed));
        grid.AddChild(MakeMetricCard(
            Loc.GetString("round-end-summary-window-metric-observers"),
            observers.ToString(),
            OddityPurple));
        grid.AddChild(MakeMetricCard(
            Loc.GetString("round-end-summary-window-metric-connected"),
            connected.ToString(),
            SuccessGreen));

        return grid;
    }

    private Control MakeMetricCard(string title, string value, Color accent)
    {
        var panel = MakePanel(accent.WithAlpha(0.13f), accent.WithAlpha(0.72f));
        panel.MinSize = new Vector2(160, 68);

        var container = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(10, 8),
            SeparationOverride = 2,
            HorizontalExpand = true
        };

        container.AddChild(new Label
        {
            Text = title,
            FontColorOverride = TextMuted
        });
        container.AddChild(new Label
        {
            Text = value,
            FontColorOverride = accent,
            StyleClasses = { StyleNano.StyleClassLabelBig }
        });

        panel.AddChild(container);
        return panel;
    }

    private Control MakeStatSection(
        string title,
        string subtitle,
        RoundEndSummaryStat[] stats)
    {
        var section = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
            HorizontalExpand = true
        };

        section.AddChild(new Label
        {
            Text = Loc.GetString(title),
            FontColorOverride = Text,
            StyleClasses = { StyleNano.StyleClassLabelHeadingBigger }
        });
        section.AddChild(new Label
        {
            Text = Loc.GetString(subtitle),
            FontColorOverride = TextMuted
        });

        if (stats.Length == 0)
        {
            section.AddChild(MakeEmptyStatsPanel());
            return section;
        }

        var grid = new GridContainer
        {
            Columns = 2,
            HSeparationOverride = 8,
            VSeparationOverride = 8,
            HorizontalExpand = true
        };

        foreach (var stat in stats)
            grid.AddChild(MakeStatCard(stat));

        section.AddChild(grid);
        return section;
    }

    private Control MakeStatCard(RoundEndSummaryStat stat)
    {
        var accent = GetStatColor(stat.Color);
        var panel = MakePanel(accent.WithAlpha(0.14f), accent.WithAlpha(0.72f));
        panel.MinSize = new Vector2(310, 82);

        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(10),
            SeparationOverride = 10,
            HorizontalExpand = true
        };

        var value = MakePanel(accent.WithAlpha(0.16f), accent.WithAlpha(0.6f));
        value.MinSize = new Vector2(54, 54);
        value.AddChild(new Label
        {
            Text = stat.Value.ToString(),
            FontColorOverride = accent,
            StyleClasses = { StyleNano.StyleClassLabelBig },
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center
        });
        row.AddChild(value);

        var text = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 2,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true
        };
        text.AddChild(new Label
        {
            Text = Loc.GetString(stat.Label),
            FontColorOverride = Text,
            HorizontalExpand = true
        });
        text.AddChild(new Label
        {
            Text = Loc.GetString(stat.Detail),
            FontColorOverride = TextMuted,
            HorizontalExpand = true
        });

        row.AddChild(text);
        panel.AddChild(row);
        return panel;
    }

    private Control MakeEmptyStatsPanel()
    {
        var panel = MakePanel(Card, Border);
        panel.AddChild(new Label
        {
            Text = Loc.GetString("round-end-summary-window-telemetry-empty"),
            FontColorOverride = TextMuted,
            Margin = new Thickness(10, 8)
        });

        return panel;
    }

    private Control MakeRoundEndTextPanel(string roundEnd)
    {
        var panel = MakePanel(Background, Border);
        var container = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Margin = new Thickness(10),
            SeparationOverride = 6,
            HorizontalExpand = true
        };

        container.AddChild(new Label
        {
            Text = Loc.GetString("round-end-summary-window-transmission-title"),
            FontColorOverride = Text,
            StyleClasses = { StyleNano.StyleClassLabelHeadingBigger }
        });

        var roundEndLabel = new RichTextLabel
        {
            HorizontalExpand = true
        };
        roundEndLabel.SetMarkup(roundEnd);
        container.AddChild(roundEndLabel);

        panel.AddChild(container);
        return panel;
    }

    private BoxContainer MakePlayerManifestTab(RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
    {
        var playerManifestTab = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = Loc.GetString("round-end-summary-window-player-manifest-tab-title")
        };

        var playerInfoContainerScrollbox = new ScrollContainer
        {
            VerticalExpand = true,
            Margin = new Thickness(10),
            HScrollEnabled = false
        };
        var playerInfoContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 8
        };

        playerInfoContainer.AddChild(MakeManifestHeader(playersInfo));

        var sortedPlayersInfo = playersInfo
            .OrderBy(player => player.Observer)
            .ThenBy(player => !player.Antag)
            .ThenBy(player => player.PlayerICName ?? player.PlayerOOCName);

        foreach (var playerInfo in sortedPlayersInfo)
            playerInfoContainer.AddChild(MakePlayerCard(playerInfo));

        playerInfoContainerScrollbox.AddChild(playerInfoContainer);
        playerManifestTab.AddChild(playerInfoContainerScrollbox);

        return playerManifestTab;
    }

    private Control MakeManifestHeader(RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
    {
        var panel = MakePanel(CardQuiet, MarineBlue.WithAlpha(0.65f));
        panel.AddChild(new Label
        {
            Text = Loc.GetString("round-end-summary-window-manifest-title", ("players", playersInfo.Length)),
            FontColorOverride = Text,
            Margin = new Thickness(10, 8),
            StyleClasses = { StyleNano.StyleClassLabelHeadingBigger }
        });

        return panel;
    }

    private Control MakePlayerCard(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo)
    {
        var accent = playerInfo.Antag
            ? TraumaRed
            : playerInfo.Observer
                ? MarineBlue
                : MedicalCyan;

        var panel = MakePanel(Card, accent.WithAlpha(0.64f));
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(8),
            SeparationOverride = 8,
            HorizontalExpand = true
        };

        row.AddChild(MakePlayerSprite(playerInfo));

        var info = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 4,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true
        };
        info.AddChild(new Label
        {
            Text = playerInfo.PlayerICName ?? playerInfo.PlayerOOCName,
            FontColorOverride = accent,
            HorizontalExpand = true
        });
        info.AddChild(new Label
        {
            Text = Loc.GetString(
                "round-end-summary-window-player-role-line",
                ("playerOOCName", playerInfo.PlayerOOCName),
                ("playerRole", GetPlayerRole(playerInfo))),
            FontColorOverride = TextMuted,
            HorizontalExpand = true
        });

        var badges = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            HorizontalExpand = true
        };
        badges.AddChild(MakeBadge(
            playerInfo.Connected
                ? Loc.GetString("round-end-summary-window-player-connected")
                : Loc.GetString("round-end-summary-window-player-disconnected"),
            playerInfo.Connected ? SuccessGreen : TextMuted));

        if (playerInfo.Observer)
        {
            badges.AddChild(MakeBadge(
                Loc.GetString("round-end-summary-window-player-observer"),
                MarineBlue));
        }

        if (playerInfo.Antag)
        {
            badges.AddChild(MakeBadge(
                Loc.GetString("round-end-summary-window-player-antagonist"),
                TraumaRed));
        }

        info.AddChild(badges);
        row.AddChild(info);
        panel.AddChild(row);

        return panel;
    }

    private Control MakePlayerSprite(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo)
    {
        if (playerInfo.PlayerNetEntity != null)
        {
            return new SpriteView(playerInfo.PlayerNetEntity.Value, _entityManager)
            {
                OverrideDirection = Direction.South,
                VerticalAlignment = VAlignment.Center,
                SetSize = new Vector2(42, 42),
                VerticalExpand = true,
            };
        }

        var placeholder = MakePanel(CardQuiet, Border);
        placeholder.MinSize = new Vector2(42, 42);
        return placeholder;
    }

    private Control MakeBadge(string label, Color color)
    {
        var badge = MakePanel(color.WithAlpha(0.13f), color.WithAlpha(0.58f));
        badge.AddChild(new Label
        {
            Text = label,
            FontColorOverride = color,
            Margin = new Thickness(5, 2)
        });

        return badge;
    }

    private static PanelContainer MakePanel(Color background, Color border)
    {
        return new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = background,
                BorderColor = border,
                BorderThickness = new Thickness(1)
            }
        };
    }

    private static Color GetStatColor(RoundEndSummaryStatColor color)
    {
        return color switch
        {
            RoundEndSummaryStatColor.Blue => MarineBlue,
            RoundEndSummaryStatColor.Red => TraumaRed,
            RoundEndSummaryStatColor.Gold => WarningGold,
            RoundEndSummaryStatColor.Purple => OddityPurple,
            RoundEndSummaryStatColor.Cyan => MedicalCyan,
            RoundEndSummaryStatColor.Green => SuccessGreen,
            _ => Text,
        };
    }

    private static string GetPlayerRole(RoundEndMessageEvent.RoundEndPlayerInfo playerInfo)
    {
        return playerInfo.Observer
            ? Loc.GetString("round-end-summary-window-player-observer-role")
            : Loc.GetString(playerInfo.Role);
    }

    private static string FormatDuration(TimeSpan roundDuration)
    {
        return Loc.GetString(
            "round-end-summary-window-duration-value",
            ("hours", (roundDuration.Days * 24) + roundDuration.Hours),
            ("minutes", roundDuration.Minutes),
            ("seconds", roundDuration.Seconds));
    }
}
