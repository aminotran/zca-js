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

        var ownId = api.GetOwnId();
        Console.WriteLine($"Own ID: {ownId}");

        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
    }
}