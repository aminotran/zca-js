using System.Text.Json;
using ICU.Lib.ZaloClientWeb.Demo.Helpers;
using ICU.Lib.ZaloClientWeb.Demo.Models;
using ICU.Lib.ZaloClientWeb.Models;
using Spectre.Console;

namespace ICU.Lib.ZaloClientWeb.Demo.Components;

/// <summary>
/// Renders a friend/contact list panel (like Zalo Web's Contacts tab).
/// Shows friends with name, avatar, online status.
/// </summary>
public static class ContactsPanelComponent
{
    public static async Task ShowContactsAsync(ZaloApi api)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold cyan]👥 Danh bạ (Contacts)[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Fetching friends...", ctx =>
            {
                Thread.Sleep(500);
            });

        try
        {
            var friendResult = await api.GetAllFriendsAsync();
            if (!friendResult.IsSuccess)
            {
                AnsiConsole.MarkupLine($"[red]Failed: {friendResult.Error}[/]");
                AnsiConsole.MarkupLine("[dim]Press any key to go back...[/]");
                Console.ReadKey(true);
                return;
            }

            var root = friendResult.Data;
            JsonElement friendArray = root;

            // Try to extract friend list from response
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
            {
                if (dataEl.TryGetProperty("profiles", out var profiles))
                {
                    ShowContactsFromProfiles(profiles);
                    AnsiConsole.MarkupLine("[dim]Press any key to go back...[/]");
                    Console.ReadKey(true);
                    return;
                }
                if (dataEl.TryGetProperty("friends", out var friends))
                {
                    friendArray = friends;
                }
            }

            if (friendArray.ValueKind == JsonValueKind.Array)
            {
                var count = 0;
                foreach (var friend in friendArray.EnumerateArray())
                {
                    var name = friend.TryGetProperty("displayName", out var dn)
                        ? dn.GetString()
                        : friend.TryGetProperty("name", out var n)
                            ? n.GetString()
                            : "Unknown";
                    var uid = friend.TryGetProperty("id", out var idEl)
                        ? idEl.GetString()
                        : "?";

                    count++;
                    AnsiConsole.MarkupLine($"  [green]👤 {name.EscapeMarkupForSpectre()}[/] [dim]({uid.Shorten()})[/]");
                }

                if (count == 0)
                    AnsiConsole.MarkupLine("[italic dim]No contacts found.[/]");
                else
                    AnsiConsole.MarkupLine($"\n[bold]Total: {count} contacts[/]");
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        }

        AnsiConsole.MarkupLine("\n[dim]Press any key to go back...[/]");
        Console.ReadKey(true);
    }

    private static void ShowContactsFromProfiles(JsonElement profiles)
    {
        var count = 0;
        foreach (var profile in profiles.EnumerateObject())
        {
            var val = profile.Value;
            var name = val.TryGetProperty("displayName", out var dn)
                ? dn.GetString()
                : val.TryGetProperty("name", out var n)
                    ? n.GetString()
                    : profile.Name;
            var avatar = val.TryGetProperty("avatar", out var av)
                ? av.GetString()
                : "";

            count++;
            var avatarStr = string.IsNullOrEmpty(avatar) ? "👤" : "👤";
            AnsiConsole.MarkupLine($"  {avatarStr} [green]{name.EscapeMarkupForSpectre()}[/] [dim]({profile.Name.Shorten()})[/]");
        }
        AnsiConsole.MarkupLine($"\n[bold]Total: {count} contacts[/]");
    }
}