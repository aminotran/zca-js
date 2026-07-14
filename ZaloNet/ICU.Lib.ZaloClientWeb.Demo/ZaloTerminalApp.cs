using ICU.Lib.ZaloClientWeb.Demo.Components;
using ICU.Lib.ZaloClientWeb.Demo.Helpers;
using ICU.Lib.ZaloClientWeb.Models;
using Spectre.Console;
using SpectreStyle = Spectre.Console.Style;

namespace ICU.Lib.ZaloClientWeb.Demo;

/// <summary>
/// Main terminal-based Zalo Web-like interface.
/// Features:
/// - Tab switching: Messages (Tin nhắn) | Contacts (Danh bạ) | About
/// - Conversation list sidebar with keyboard navigation
/// - Real-time chat panel with WebSocket events
/// - Contact list viewer
/// </summary>
public static class ZaloTerminalApp
{
    public static async Task RunAsync(ZaloApi api)
    {
        var ownUid = api.GetOwnId();
        var running = true;

        while (running)
        {
            AnsiConsole.Clear();

            // Header Bar
            var header = new Panel(
                Align.Center(new Markup(
                    "[bold cyan]Zalo Web \u2726 Terminal Edition[/]  " +
                    $"[dim]UID: {ownUid.ToString().Shorten()}[/]"))
            )
            {
                BorderStyle = new SpectreStyle(Color.Purple),
                Padding = new Padding(1, 0, 1, 0)
            };
            AnsiConsole.Write(header);
            AnsiConsole.WriteLine();

            // Tab Navigation
            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold yellow]\U0001f4cc Main Menu[/]")
                    .PageSize(6)
                    .MoreChoicesText("[dim](\u2191\u2193 scroll)[/]")
                    .HighlightStyle(new SpectreStyle(Color.Black, Color.Yellow, Decoration.Bold))
                    .AddChoices(
                        "[cyan]\U0001f4ac[/] Tin nh\u1eafn (Messages)",
                        "[cyan]\U0001f465[/] Danh b\u1ea1 (Contacts)",
                        "[cyan]\u2139\ufe0f[/] About",
                        "[cyan]\U0001f6aa[/] Logout & Exit"
                    ));

            switch (selection)
            {
                case string s when s.Contains("Tin nh"):
                    await RunMessagesTabAsync(api, ownUid);
                    break;

                case string s when s.Contains("Danh b"):
                    await ContactsPanelComponent.ShowContactsAsync(api);
                    break;

                case string s when s.Contains("About"):
                    ShowAbout();
                    break;

                case string s when s.Contains("Logout"):
                    running = false;
                    break;
            }
        }
    }

    private static async Task RunMessagesTabAsync(ZaloApi api, long ownUid)
    {
        while (true)
        {
            AnsiConsole.Clear();

            var conversation = await ConversationListComponent.SelectConversationAsync(api, ownUid);

            if (conversation == null)
            {
                return;
            }

            await ChatPanelComponent.RunChatSessionAsync(api, conversation, ownUid);
        }
    }

    private static void ShowAbout()
    {
        AnsiConsole.Clear();

        var aboutPanel = new Panel(
            new Markup(
                "[bold cyan]ICU.Lib.ZaloClientWeb Demo[/]\n\n" +
                "[green]Version[/]: 1.0.0\n" +
                "[green]Framework[/]: .NET 10.0\n" +
                "[green]UI Library[/]: Spectre.Console 0.57\n" +
                "[green]Zalo API Client[/]: ICU.Lib.ZaloClientWeb\n\n" +
                "[dim]This application demonstrates the core features\n" +
                "of the ICU.Lib.ZaloClientWeb library through an\n" +
                "interactive terminal interface inspired by Zalo Web.\n\n" +
                "Built with \u2764\ufe0f using .NET and Spectre.Console.[/]")
        )
        {
            BorderStyle = new SpectreStyle(Color.Cyan),
            Padding = new Padding(2, 1, 2, 1),
        };

        AnsiConsole.Write(aboutPanel);
        AnsiConsole.MarkupLine("\n[dim]Press any key to go back...[/]");
        Console.ReadKey(true);
    }
}