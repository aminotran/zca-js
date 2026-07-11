using ICU.Lib.ZaloClientWeb;

namespace ICU.Lib.ZaloClientWeb.Demo.Scenarios;

public static class FriendDemo
{
    public static async Task RunAsync(ZaloApi api)
    {
        Console.WriteLine("\n--- Friend Operations ---");

        var friends = await api.GetAllFriendsAsync();
        Console.WriteLine(friends.IsSuccess ? $"Friends data: {friends.Data}" : $"Error: {friends.Error}");

        var requests = await api.GetFriendRequestStatusAsync();
        Console.WriteLine(requests.IsSuccess ? $"Requests: {requests.Data}" : $"Error: {requests.Error}");

        var closeFriends = await api.GetCloseFriendsAsync();
        Console.WriteLine(closeFriends.IsSuccess ? $"Close friends: {closeFriends.Data}" : $"Error: {closeFriends.Error}");

        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
    }
}