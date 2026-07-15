using ICU.Lib.ZaloClientWeb.Demo.Helpers;
using ICU.Lib.ZaloClientWeb.Utils;
using Spectre.Console;
using System.Text.Json;

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
            ZaloApiResponse<List<ICU.Lib.ZaloClientWeb.Models.ApiModels.getAllFriendsModel.ResponseModel>?> friendResult = await api.GetAllFriendsAsync();
            if (!friendResult.IsSuccess)
            {
                AnsiConsole.MarkupLine($"[red]Failed: {friendResult.Error}[/]");
                AnsiConsole.MarkupLine("[dim]Press any key to go back...[/]");
                Console.ReadKey(true);
                return;
            }

            if (friendResult.Data != null)
            {
                int count = 0;
                foreach (var friend in friendResult.Data)
                {
                    string? name = friend.displayName;
                    string? uid = friend.userId;

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
        int count = 0;
        foreach (JsonProperty profile in profiles.EnumerateObject())
        {
            JsonElement val = profile.Value;
            string? name = val.TryGetProperty("displayName", out JsonElement dn)
                ? dn.GetString()
                : val.TryGetProperty("name", out JsonElement n)
                    ? n.GetString()
                    : profile.Name;
            string? avatar = val.TryGetProperty("avatar", out JsonElement av)
                ? av.GetString()
                : "";

            count++;
            string avatarStr = string.IsNullOrEmpty(avatar) ? "👤" : "👤";
            AnsiConsole.MarkupLine($"  {avatarStr} [green]{name.EscapeMarkupForSpectre()}[/] [dim]({profile.Name.Shorten()})[/]");
        }
        AnsiConsole.MarkupLine($"\n[bold]Total: {count} contacts[/]");
    }
}