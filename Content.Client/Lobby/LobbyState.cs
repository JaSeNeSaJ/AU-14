using System.Linq;
using Content.Client._CMU14.Lobby;
using Content.Client._RMC14.LinkAccount;
using Content.Client.Audio;
using Content.Client.GameTicking.Managers;
using Content.Client.LateJoin;
using Content.Client.Lobby.UI;
using Content.Client.Message;
using Content.Client.Playtime;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.Chat;
using Content.Client.Voting;
using Content.Shared.AU14.Allegiance;
using Content.Shared.CCVar;
using Content.Shared.Preferences;
using Robust.Client;
using Robust.Client.Console;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client.Lobby
{
    public sealed partial class LobbyState : State
    {
        [Dependency] private IBaseClient _baseClient = default!;
        [Dependency] private IConfigurationManager _cfg = default!;
        [Dependency] private IClientConsoleHost _consoleHost = default!;
        [Dependency] private IEntityManager _entityManager = default!;
        [Dependency] private IResourceCache _resourceCache = default!;
        [Dependency] private IUserInterfaceManager _userInterfaceManager = default!;
        [Dependency] private IGameTiming _gameTiming = default!;
        [Dependency] private IVoteManager _voteManager = default!;
        [Dependency] private ClientsidePlaytimeTrackingManager _playtimeTracking = default!;

        // RMC14
        [Dependency] private LinkAccountManager _linkAccount = default!;
        [Dependency] private IClientPreferencesManager _preferencesManager = default!;

        /// <summary>
        /// Whether the player wants to ignore allegiance for spawning the current character.
        /// </summary>
        public bool IgnoreAllegiance { get; set; }

        private ClientGameTicker _gameTicker = default!;
        private ContentAudioSystem _contentAudioSystem = default!;
        // The faction choices, opened from JoinRoundButton. Held so a second press re-focuses the
        // one window rather than stacking another copy on top of it.
        private JoinRoundWindow? _joinRoundWindow;

        protected override Type? LinkedScreenType { get; } = typeof(LobbyGui);
        public LobbyGui? Lobby;

        protected override void Startup()
        {
            if (_userInterfaceManager.ActiveScreen == null)
            {
                return;
            }

            Lobby = (LobbyGui) _userInterfaceManager.ActiveScreen;

            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            _gameTicker = _entityManager.System<ClientGameTicker>();
            _contentAudioSystem = _entityManager.System<ContentAudioSystem>();
            _contentAudioSystem.LobbySoundtrackChanged += UpdateLobbySoundtrackInfo;

            chatController.SetMainChat(true);

            _voteManager.SetPopupContainer(Lobby.VoteContainer);
            LayoutContainer.SetAnchorPreset(Lobby, LayoutContainer.LayoutPreset.Wide);

            var lobbyNameCvar = _cfg.GetCVar(CCVars.ServerLobbyName);
            var serverName = _baseClient.GameInfo?.ServerName ?? string.Empty;

            Lobby.ServerName.Text = string.IsNullOrEmpty(lobbyNameCvar)
                ? Loc.GetString("ui-lobby-title", ("serverName", serverName))
                : lobbyNameCvar;

            var width = _cfg.GetCVar(CCVars.ServerLobbyRightPanelWidth);
            Lobby.RightSide.SetWidth = width;

            UpdateLobbyUi();

            Lobby.CharacterPreview.CharacterSetupButton.OnPressed += OnSetupPressed;
            Lobby.CharacterPreview.PatronPerks.OnPressed += OnPatronPerksPressed;
            Lobby.CharacterPreview.PrevCharacterButton.OnPressed += OnPrevCharPressed;
            Lobby.CharacterPreview.NextCharacterButton.OnPressed += OnNextCharPressed;
            Lobby.CharacterPreview.IgnoreAllegianceToggle.OnToggled += OnIgnoreAllegianceToggled;
            Lobby.ReadyButton.OnPressed += OnReadyPressed;
            Lobby.ReadyButton.OnToggled += OnReadyToggled;

            _gameTicker.InfoBlobUpdated += UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated += LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated += LobbyLateJoinStatusUpdated;

            // RMC14/CMU: the faction choices used to be three buttons on the lobby panel. They now
            // live in JoinRoundWindow, opened from one button; the handlers below are unchanged.
            Lobby.JoinRoundButton.OnPressed += OnJoinRoundPressed;
        }

        protected override void Shutdown()
        {
            var chatController = _userInterfaceManager.GetUIController<ChatUIController>();
            chatController.SetMainChat(false);
            _gameTicker.InfoBlobUpdated -= UpdateLobbyUi;
            _gameTicker.LobbyStatusUpdated -= LobbyStatusUpdated;
            _gameTicker.LobbyLateJoinStatusUpdated -= LobbyLateJoinStatusUpdated;
            _contentAudioSystem.LobbySoundtrackChanged -= UpdateLobbySoundtrackInfo;

            _voteManager.ClearPopupContainer();

            Lobby!.CharacterPreview.CharacterSetupButton.OnPressed -= OnSetupPressed;
            Lobby.CharacterPreview.PatronPerks.OnPressed -= OnPatronPerksPressed;
            Lobby.CharacterPreview.PrevCharacterButton.OnPressed -= OnPrevCharPressed;
            Lobby.CharacterPreview.NextCharacterButton.OnPressed -= OnNextCharPressed;
            Lobby.CharacterPreview.IgnoreAllegianceToggle.OnToggled -= OnIgnoreAllegianceToggled;
            Lobby!.ReadyButton.OnPressed -= OnReadyPressed;
            Lobby!.ReadyButton.OnToggled -= OnReadyToggled;

            // Unhook RMC14 buttons
            Lobby.JoinRoundButton.OnPressed -= OnJoinRoundPressed;
            _joinRoundWindow?.Close();
            _joinRoundWindow = null;

            Lobby = null;
        }

        public void SwitchState(LobbyGui.LobbyGuiState state)
        {
            // Yeah I hate this but LobbyState contains all the badness for now.
            Lobby?.SwitchState(state);
        }

        private void OnSetupPressed(BaseButton.ButtonEventArgs args)
        {
            SetReady(false);
            Lobby?.SwitchState(LobbyGui.LobbyGuiState.CharacterSetup);
        }

        private void OnPatronPerksPressed(BaseButton.ButtonEventArgs obj)
        {
            _userInterfaceManager.GetUIController<LinkAccountUIController>().TogglePatronPerksWindow();
        }

        private void OnReadyPressed(BaseButton.ButtonEventArgs args)
        {
            if (!_gameTicker.IsGameStarted)
            {
                return;
            }

            // Second-stage ready action: open colonists-filtered late-join UI
            new LateJoinGui("colonists").OpenCentered();
        }

        private void OnReadyToggled(BaseButton.ButtonToggledEventArgs args)
        {
            SetReady(args.Pressed);
        }

        public override void FrameUpdate(FrameEventArgs e)
        {
            if (_gameTicker.IsGameStarted)
            {
                var roundTime = _gameTiming.CurTime.Subtract(_gameTicker.RoundStartTimeSpan);
                Lobby!.StationTime.Text = Loc.GetString("lobby-state-round-time-short", ("hours", roundTime.Hours), ("minutes", roundTime.Minutes));

                // Upstream blanks the countdown here. The panel's heading slot would then be empty
                // and the button grid would sit against the top border, so the two states would not
                // look like the same panel. Say what's happening instead.
                Lobby!.StartTime.Text = Loc.GetString("cmu-lobby-state-round-in-progress");
                SetCountdownUrgency(CountdownUrgency.None);
                Lobby!.LobbyStatusLine.Text = Loc.GetString("cmu-lobby-state-round-elapsed",
                    ("hours", roundTime.Hours), ("minutes", roundTime.Minutes));
                Lobby!.LobbyStatusLine.Visible = true;
                return;
            }

            Lobby!.LobbyStatusLine.Visible = false;
            Lobby!.StationTime.Text = Loc.GetString("lobby-state-round-not-started-short");
            string text;

            if (_gameTicker.Paused)
            {
                text = Loc.GetString("lobby-state-paused");
                // Paused is indefinite, not urgent - it must not sit there glowing red.
                SetCountdownUrgency(CountdownUrgency.None);
            }
            else if (_gameTicker.StartTime < _gameTiming.CurTime)
            {
                SetCountdownUrgency(CountdownUrgency.Imminent);
                Lobby!.StartTime.Text = Loc.GetString("lobby-state-soon");
                return;
            }
            else
            {
                var difference = _gameTicker.StartTime - _gameTiming.CurTime;
                var seconds = difference.TotalSeconds;
                SetCountdownUrgency(GetCountdownUrgency(seconds));
                if (seconds < 0)
                {
                    text = Loc.GetString(seconds < -5 ? "lobby-state-right-now-question" : "lobby-state-right-now-confirmation");
                }
                else if (difference.TotalHours >= 1)
                {
                    text = $"{Math.Floor(difference.TotalHours)}:{difference.Minutes:D2}:{difference.Seconds:D2}";
                }
                else
                {
                    text = $"{difference.Minutes}:{difference.Seconds:D2}";
                }
            }

            Lobby!.StartTime.Text = Loc.GetString("lobby-state-round-start-countdown-text", ("timeLeft", text));
        }

        /// <summary>
        ///     How loud the countdown should be. Colour is the only channel here, so the thresholds
        ///     are wide enough to be noticed at a glance rather than read off a clock.
        /// </summary>
        private enum CountdownUrgency
        {
            None,
            Soon,
            Imminent
        }

        private const double CountdownSoonSeconds = 60;
        private const double CountdownImminentSeconds = 20;

        private static CountdownUrgency GetCountdownUrgency(double seconds)
        {
            if (seconds <= CountdownImminentSeconds)
                return CountdownUrgency.Imminent;

            return seconds <= CountdownSoonSeconds ? CountdownUrgency.Soon : CountdownUrgency.None;
        }

        private void SetCountdownUrgency(CountdownUrgency urgency)
        {
            var styleClass = urgency switch
            {
                CountdownUrgency.Imminent => StyleNano.StyleClassCrtHeadingBigDanger,
                CountdownUrgency.Soon => StyleNano.StyleClassCrtHeadingBigWarning,
                _ => StyleNano.StyleClassCrtHeadingBig
            };

            var label = Lobby!.StartTime;
            if (label.HasStyleClass(styleClass))
                return;

            // Swap, never stack: all three rules set the same font and font colour, and two matching
            // rules of equal specificity have no defined winner.
            label.RemoveStyleClass(StyleNano.StyleClassCrtHeadingBig);
            label.RemoveStyleClass(StyleNano.StyleClassCrtHeadingBigWarning);
            label.RemoveStyleClass(StyleNano.StyleClassCrtHeadingBigDanger);
            label.AddStyleClass(styleClass);
        }

        private void LobbyStatusUpdated()
        {
            UpdateLobbyBackground();
            UpdateLobbyUi();
        }

        private void LobbyLateJoinStatusUpdated()
        {
            Lobby!.ReadyButton.Disabled = _gameTicker.DisallowedLateJoin;
        }

        private void UpdateLobbyUi()
        {
            Lobby!.CharacterPreview.PatronPerks.Visible = _linkAccount.CanViewPatronPerks();

            if (_gameTicker.IsGameStarted)
            {
                Lobby!.ObserveButton.Disabled = false;

                // RMC14/CMU: readying up is meaningless once the round is running, so the row swaps
                // to the single button that opens the faction choices rather than restyling Ready
                // into a join button.
                Lobby!.ReadyButton.Visible = false;
                Lobby!.JoinRoundButton.Visible = true;
            }
            else
            {
                Lobby!.StartTime.Text = string.Empty;
                Lobby!.ReadyButton.Text = Loc.GetString(Lobby!.ReadyButton.Pressed ? "lobby-state-player-status-ready": "lobby-state-player-status-not-ready");
                Lobby!.ReadyButton.ToggleMode = true;
                Lobby!.ReadyButton.Disabled = false;
                Lobby!.ReadyButton.Pressed = _gameTicker.AreWeReady;
                Lobby!.ObserveButton.Disabled = true;

                // RMC14/CMU
                Lobby!.ReadyButton.Visible = true;
                Lobby!.JoinRoundButton.Visible = false;
                _joinRoundWindow?.Close();
            }

            if (_gameTicker.ServerInfoBlob != null)
            {
                Lobby!.ServerInfo.SetInfoBlob(_gameTicker.ServerInfoBlob);
            }

            Lobby!.ServerInfo.SetRoundInfo(_gameTicker.ServerRoundInfo);

            var minutesToday = _playtimeTracking.PlaytimeMinutesToday;
            if (minutesToday > 60)
            {
                Lobby!.PlaytimeComment.Visible = false; // RMC14

                var hoursToday = Math.Round(minutesToday / 60f, 1);

                var chosenString = minutesToday switch
                {
                    < 180 => "lobby-state-playtime-comment-normal",
                    < 360 => "lobby-state-playtime-comment-concerning",
                    < 720 => "lobby-state-playtime-comment-grasstouchless",
                    _ => "lobby-state-playtime-comment-selfdestructive"
                };

                Lobby.PlaytimeComment.SetMarkup(Loc.GetString(chosenString, ("hours", hoursToday)));
            }
            else
                Lobby!.PlaytimeComment.Visible = false;
        }

        private void UpdateLobbySoundtrackInfo(LobbySoundtrackChangedEvent ev)
        {
            if (ev.SoundtrackFilename == null)
            {
                Lobby!.LobbySong.SetMarkup(Loc.GetString("lobby-state-song-no-song-text"));
            }
            else if (
                ev.SoundtrackFilename != null
                && _resourceCache.TryGetResource<AudioResource>(ev.SoundtrackFilename, out var lobbySongResource)
                )
            {
                var lobbyStream = lobbySongResource.AudioStream;

                var title = string.IsNullOrEmpty(lobbyStream.Title)
                    ? Loc.GetString("lobby-state-song-unknown-title")
                    : lobbyStream.Title;

                var artist = string.IsNullOrEmpty(lobbyStream.Artist)
                    ? Loc.GetString("lobby-state-song-unknown-artist")
                    : lobbyStream.Artist;

                var markup = Loc.GetString("lobby-state-song-text",
                    ("songTitle", title),
                    ("songArtist", artist));

                Lobby!.LobbySong.SetMarkup(markup);
            }
        }

        private void UpdateLobbyBackground()
        {
            if (_gameTicker.LobbyBackground != null)
            {
                Lobby!.Background.Texture = _resourceCache.GetResource<TextureResource>(_gameTicker.LobbyBackground );
            }
            else
            {
                Lobby!.Background.Texture = null;
            }

        }

        private void SetReady(bool newReady)
        {
            if (_gameTicker.IsGameStarted)
            {
                return;
            }

            _consoleHost.ExecuteCommand($"toggleready {newReady}");
        }

        private void OnJoinRoundPressed(BaseButton.ButtonEventArgs args)
        {
            if (_joinRoundWindow is { Disposed: false })
            {
                _joinRoundWindow.MoveToFront();
                return;
            }

            var window = new JoinRoundWindow();
            _joinRoundWindow = window;

            // Each choice is terminal - it opens a late-join or ghost-roles window on top - so close
            // this one first rather than leaving it stranded behind whatever the choice opened.
            window.JoinColonistsButton.OnPressed += args2 => { window.Close(); OnReadyPressed(args2); };
            window.JoinGovforButton.OnPressed += args2 => { window.Close(); OnJoinGovforPressed(args2); };
            window.JoinOpforButton.OnPressed += args2 => { window.Close(); OnJoinOpforPressed(args2); };
            window.JoinOtherButton.OnPressed += args2 => { window.Close(); OnJoinOtherPressed(args2); };
            window.OnClose += () =>
            {
                if (_joinRoundWindow == window)
                    _joinRoundWindow = null;
            };

            window.OpenCentered();
        }

        private void OnJoinGovforPressed(BaseButton.ButtonEventArgs args)
        {
            new LateJoinGui("govfor").OpenCentered();
        }

        private void OnJoinOpforPressed(BaseButton.ButtonEventArgs args)
        {
            new LateJoinGui("opfor").OpenCentered();
        }

        private void OnJoinOtherPressed(BaseButton.ButtonEventArgs args)
        {
             // Open the ghost roles UI (server-driven) to display all ghost roles
             _consoleHost.RemoteExecuteCommand(null, "ghostroles");
        }

        private void OnPrevCharPressed(BaseButton.ButtonEventArgs args)
        {
            if (_preferencesManager.Preferences == null || _preferencesManager.Settings == null)
                return;

            var characters = _preferencesManager.Preferences.Characters;
            var currentIndex = _preferencesManager.Preferences.SelectedCharacterIndex;

            // Find the previous occupied slot
            var sortedSlots = characters.Keys.OrderBy(k => k).ToList();
            if (sortedSlots.Count <= 1)
                return;

            var idx = sortedSlots.IndexOf(currentIndex);
            var prevIdx = idx <= 0 ? sortedSlots.Count - 1 : idx - 1;
            _preferencesManager.SelectCharacter(sortedSlots[prevIdx]);
            _userInterfaceManager.GetUIController<LobbyUIController>().ReloadCharacterSetup();
        }

        private void OnNextCharPressed(BaseButton.ButtonEventArgs args)
        {
            if (_preferencesManager.Preferences == null || _preferencesManager.Settings == null)
                return;

            var characters = _preferencesManager.Preferences.Characters;
            var currentIndex = _preferencesManager.Preferences.SelectedCharacterIndex;

            // Find the next occupied slot
            var sortedSlots = characters.Keys.OrderBy(k => k).ToList();
            if (sortedSlots.Count <= 1)
                return;

            var idx = sortedSlots.IndexOf(currentIndex);
            var nextIdx = idx >= sortedSlots.Count - 1 ? 0 : idx + 1;
            _preferencesManager.SelectCharacter(sortedSlots[nextIdx]);
            _userInterfaceManager.GetUIController<LobbyUIController>().ReloadCharacterSetup();
        }

        private void OnIgnoreAllegianceToggled(BaseButton.ButtonToggledEventArgs args)
        {
            IgnoreAllegiance = args.Pressed;
            var netManager = IoCManager.Resolve<Robust.Shared.Network.IClientNetManager>();
            var msg = new MsgIgnoreAllegiance
            {
                IgnoreAllegiance = args.Pressed
            };
            netManager.ClientSendMessage(msg);
        }
    }
}
