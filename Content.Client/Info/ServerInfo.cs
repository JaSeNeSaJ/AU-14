using System.Collections.Generic;
using Content.Client.Changelog;
using Content.Client.Credits;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared.GameTicking;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.Info
{
    /// <summary>
    ///     The lobby's server-info block: a heading row, then a table of round info. The round timer
    ///     is a cell of that table, kept alive across rebuilds so LobbyState can drive it per-frame.
    /// </summary>
    public sealed class ServerInfo : BoxContainer
    {
        private readonly NanoHeading _title;
        private readonly NanoHeading _welcomeHeading;
        private readonly GridContainer _roundInfoGrid;
        private readonly PanelContainer _roundTimeCell;
        private readonly BoxContainer _extraLines;

        public ServerInfo()
        {
            Orientation = LayoutOrientation.Vertical;

            _title = new NanoHeading { VerticalAlignment = VAlignment.Center };

            // The server's title line, styled to match the heading beside it.
            _welcomeHeading = new NanoHeading
            {
                HorizontalExpand = true,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            };

            // The rule under this row is a CRT-genre convention - the rich-text markup used
            // elsewhere has no underline tag, so CRT draws the separator as a border instead. NanoUI
            // has no such convention and no matching visual language for it, so the row stays a
            // plain, unstyled grouping box in base mode rather than carrying a CRT-only decoration.
            var titleRow = new PanelContainer
            {
                HorizontalExpand = true,
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = LayoutOrientation.Horizontal,
                        HorizontalExpand = true,
                        SeparationOverride = 8,
                        Children = { _title, _welcomeHeading },
                    },
                },
            };

            if (StyleNano.CrtUiEnabled)
                titleRow.AddStyleClass(StyleNano.StyleClassCrtUnderlineRow);

            AddChild(titleRow);

            // Round info is a real grid rather than pre-formatted text so the columns stay aligned
            // at any panel width and however long a ship or platoon name gets.
            _roundInfoGrid = new GridContainer
            {
                Columns = 2,
                HorizontalExpand = true,
                HSeparationOverride = 4,
                VSeparationOverride = 3,
                Margin = new Thickness(0, 4, 0, 0),
            };
            AddChild(_roundInfoGrid);

            RoundTimeLabel = new Label
            {
                HorizontalExpand = true,
                Align = Label.AlignMode.Center,
                StyleClasses = { StyleNano.StyleClassCrtTableCellText },
            };
            _roundTimeCell = MakeCell(Loc.GetString("lobby-info-round-time"), RoundTimeLabel);

            _extraLines = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                HorizontalExpand = true,
                Margin = new Thickness(0, 2, 0, 0),
            };
            AddChild(_extraLines);
        }

        /// <summary>
        ///     Heading shown to the left of the server title.
        /// </summary>
        public string? Title
        {
            get => _title.Text;
            set => _title.Text = value;
        }

        /// <summary>
        ///     The round timer, driven per-frame by LobbyState.
        /// </summary>
        public Label RoundTimeLabel { get; }

        /// <summary>
        ///     Sets the server's intro text. The first line becomes the title heading and is drawn as
        ///     plain text, so it must not contain markup. Any further lines render underneath.
        /// </summary>
        public void SetInfoBlob(string markup)
        {
            _extraLines.DisposeAllChildren();

            var first = true;
            foreach (var line in markup.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0)
                    continue;

                if (first)
                {
                    _welcomeHeading.Text = trimmed;
                    first = false;
                    continue;
                }

                var label = new RichTextLabel
                {
                    HorizontalAlignment = HAlignment.Center,
                    // Opts out of the generic CRT body-text style so it can be sized independently.
                    // CrtLobbyTheme checks for this class and leaves it alone.
                    StyleClasses = { StyleNano.StyleClassCrtServerInfoText },
                };
                label.SetMessage(FormattedMessage.FromMarkupOrThrow(trimmed));
                _extraLines.AddChild(label);
            }
        }

        /// <summary>
        ///     Rebuilds the round-info table. Each field becomes a boxed cell with its heading above
        ///     its value, laid out two columns per row, with the round timer last.
        /// </summary>
        public void SetRoundInfo(IReadOnlyList<LobbyRoundInfoField> fields)
        {
            // The timer cell outlives the rebuild - detach it so DisposeAllChildren doesn't take
            // RoundTimeLabel with it.
            if (_roundTimeCell.Parent == _roundInfoGrid)
                _roundInfoGrid.RemoveChild(_roundTimeCell);

            _roundInfoGrid.DisposeAllChildren();

            foreach (var field in fields)
            {
                var value = new Label
                {
                    Text = field.Value,
                    HorizontalExpand = true,
                    Align = Label.AlignMode.Center,
                    StyleClasses = { StyleNano.StyleClassCrtTableCellText },
                };

                if (field.Color != null && Color.TryFromHex(field.Color) is { } color)
                    value.FontColorOverride = color;

                _roundInfoGrid.AddChild(MakeCell(field.Label, value));
            }

            _roundInfoGrid.AddChild(_roundTimeCell);

            // Keep a trailing odd cell from stretching across both columns.
            if ((fields.Count + 1) % 2 != 0)
                _roundInfoGrid.AddChild(new Control());
        }

        private static PanelContainer MakeCell(string headingText, Label value)
        {
            var heading = new Label
            {
                Text = headingText,
                HorizontalExpand = true,
                Align = Label.AlignMode.Center,
                StyleClasses = { StyleNano.StyleClassCrtTableCellText },
            };

            return new PanelContainer
            {
                HorizontalExpand = true,
                StyleClasses = { StyleNano.StyleClassCrtTableCell },
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = LayoutOrientation.Vertical,
                        HorizontalExpand = true,
                        SeparationOverride = 2,
                        Children = { heading, value },
                    },
                },
            };
        }
    }
}
