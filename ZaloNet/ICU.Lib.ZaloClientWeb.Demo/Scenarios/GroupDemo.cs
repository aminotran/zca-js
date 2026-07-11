using ICU.Lib.ZaloClientWeb;

namespace ICU.Lib.ZaloClientWeb.Demo.Scenarios;

public static class GroupDemo
{
    public static async Task RunAsync(ZaloApi api)
    {
        Console.WriteLine("\n--- Group Info ---");

        var groups = await api.GetAllGroupsAsync();
        Console.WriteLine(groups.IsSuccess ? $"Groups: {groups.Data}" : $"Error: {groups.Error}");

        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
    }
}