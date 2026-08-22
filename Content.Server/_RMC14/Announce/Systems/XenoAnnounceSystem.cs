using Content.Server.Administration.Logs;
using Content.Shared._RMC14.Announce;
using Content.Shared._RMC14.Xenonids.Evolution;
using Content.Server.Chat.Managers;
using Content.Shared._RMC14.Xenonids.Announce;
using Content.Shared._RMC14.Xenonids.Word;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Ghost;
using Content.Shared.Popups;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server._RMC14.Announce;

public sealed partial class XenoAnnounceSystem : SharedXenoAnnounceSystem
{
    private const string QueenAnnouncementPreset = "XenoQueen";

    /// <summary>
    ///     Row tint for xeno announcements in chat.
    /// </summary>
    /// <remarks>
    ///     Announcements carry no channel prefix - <c>ChatMessageRow.IsUnlabeledRadioSystemMessage</c>
    ///     suppresses it - so in a busy log they were a bare run of coloured text with nothing marking
    ///     where they began or ended. Under the CRT theme that was total: rows there draw no fill of
    ///     their own, so a xeno announcement was indistinguishable from ordinary radio chatter.
    ///     A band gives it an edge to read against without adding a prefix that would say "RAD".
    ///
    ///     Dark enough that the announcement's own #7575F3 title and red body still sit clearly on
    ///     top of it - this is a ground, not a highlight.
    /// </remarks>
    private static readonly Color AnnouncementBackground = Color.FromHex("#2A1740");

    [Dependency] private IAdminLogManager _adminLogs = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private GeneralAnnounceSystem _generalAnnounce = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Announce(EntityUid source, Filter filter, string message, string wrapped, SoundSpecifier? sound = null, PopupType? popup = null, bool needsQueen = false)
    {
        base.Announce(source, filter, message, wrapped, sound, popup, needsQueen);

        if (needsQueen)
        {
            if (Hive.GetHive(source) is { } sourceHive)
            {
                if (!Hive.HasHiveQueen(sourceHive))
                    return;
            }
            else
            {
                return;
            }
        }

        filter.AddWhereAttachedEntity(HasComp<GhostComponent>);

        if (source.IsValid())
            _adminLogs.Add(LogType.RMCXenoAnnounce, $"{ToPrettyString(source):source} xeno announced message: {message}");

        if (source.IsValid() && IsQueenAnnouncementSource(source))
        {
            var request = new AnnouncementRequest
            {
                Message = message,
                Preset = QueenAnnouncementPreset,
                Target = AnnouncementTarget.Xenos,
                Speaker = source,
                Source = source,
                ShowSprite = false,
            };

            _generalAnnounce.AnnounceAdvanced(request, filter);
        }

        _chat.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, wrapped, source, false, true, null, hidePopup: true, backgroundColorOverride: AnnouncementBackground);
        _audio.PlayGlobal(sound, filter, true);

        if (popup == null)
            return;

        foreach (var session in filter.Recipients)
        {
            if (session.AttachedEntity is { } recipient)
                _popup.PopupEntity(message, recipient, recipient, popup.Value);
        }
    }

    private bool IsQueenAnnouncementSource(EntityUid source)
    {
        return HasComp<XenoWordQueenComponent>(source) ||
               HasComp<XenoEvolutionGranterComponent>(source);
    }
}
