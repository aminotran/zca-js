using ICU.Lib.ZaloClientWeb;

namespace ICU.Lib.ZaloClientWeb.Demo.Scenarios;

public static class AccountInfoDemo
{
    public static async Task RunAsync(ZaloApi api)
    {
        Console.WriteLine("\n--- Account Info ---");

        var response = await api.GetAccountInfoAsync();
        if (response.IsSuccess)
        {
            Console.WriteLine($"Account data: {response.Data}");
        }
        else
        {
            Console.WriteLine($"Error: {response.Error}");
        }

        var ownId = await api.GetOwnIdAsync();
        Console.WriteLine(ownId.IsSuccess ? $"Own ID: {ownId.Data}" : $"Error: {ownId.Error}");

        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
    }
}