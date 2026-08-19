using Robust.Shared.Console;

namespace Content.Client._CMU14.Interface;

/// <summary>
///     Opens the proposed-chat mock window, and switches between its variants.
/// </summary>
/// <remarks>
///     Separate from the real chat on purpose: the chat's surfaces are written at runtime from four
///     places, so a value changed in the stylesheet may never reach the screen there. This draws the
///     layout with nothing in the way, so what is written is what is seen. Port to the real chat once
///     the values are settled - see <see cref="CrtChatMockWindow"/>.
/// </remarks>
public sealed class CMUChatMockCommand : IConsoleCommand
{
    private CrtChatMockWindow? _window;

    // Chosen 2026-08-19: underline selection, band input. The other four remain switchable so the
    // decision can be re-argued against something real rather than from memory.
    private CrtChatMockWindow.SelectionStyle _selection = CrtChatMockWindow.SelectionStyle.Underline;
    private CrtChatMockWindow.InputStyle _input = CrtChatMockWindow.InputStyle.Band;

    public string Command => "cmu_chatmock";
    public string Description => "Opens the proposed CRT chat mock, or switches its variants.";
    public string Help =>
        "Usage: cmu_chatmock [on|off] | sel <inverted|s4|underline> | input <band|flat|chip>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            Report(shell);
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "on":
            case "open":
                Open();
                break;

            case "off":
            case "close":
                Close();
                break;

            case "sel":
            case "selection":
                if (args.Length < 2)
                {
                    shell.WriteError(Help);
                    return;
                }

                switch (args[1].ToLowerInvariant())
                {
                    case "inverted": _selection = CrtChatMockWindow.SelectionStyle.Inverted; break;
                    case "s4": _selection = CrtChatMockWindow.SelectionStyle.Surface4; break;
                    case "underline": _selection = CrtChatMockWindow.SelectionStyle.Underline; break;
                    default:
                        shell.WriteError("Expected: inverted, s4 or underline.");
                        return;
                }

                // Rebuild rather than restyle: the variants differ in tree shape, not just colour -
                // a resting tab has no panel around it at all.
                Reopen();
                break;

            case "input":
                if (args.Length < 2)
                {
                    shell.WriteError(Help);
                    return;
                }

                switch (args[1].ToLowerInvariant())
                {
                    case "band": _input = CrtChatMockWindow.InputStyle.Band; break;
                    case "flat": _input = CrtChatMockWindow.InputStyle.Flat; break;
                    case "chip": _input = CrtChatMockWindow.InputStyle.Chip; break;
                    default:
                        shell.WriteError("Expected: band, flat or chip.");
                        return;
                }

                Reopen();
                break;

            default:
                shell.WriteError(Help);
                return;
        }

        Report(shell);
    }

    private void Open()
    {
        if (_window is { Disposed: false })
        {
            _window.OpenCentered();
            _window.MoveToFront();
            return;
        }

        _window = new CrtChatMockWindow(_selection, _input);
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }

    private void Reopen()
    {
        Close();
        Open();
    }

    private void Close()
    {
        _window?.Close();
        _window = null;
    }

    private void Report(IConsoleShell shell)
    {
        shell.WriteLine("CMU chat mock:");
        shell.WriteLine($"  open={_window is { Disposed: false }}");
        shell.WriteLine($"  selection={_selection}, input={_input}");
    }
}
