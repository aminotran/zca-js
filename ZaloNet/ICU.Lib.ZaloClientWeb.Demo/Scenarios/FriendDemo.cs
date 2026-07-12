using ICU.Lib.ZaloClientWeb;

namespace ICU.Lib.ZaloClientWeb.Demo.Scenarios;

public static class FriendDemo
{
    public static async Task RunAsync(ZaloApi api)
    {
        Console.WriteLine("\n--- Friend Operations ---");

        var friends = await api.GetAllFriendsAsync();
        Console.WriteLine(friends.IsSuccess ? $"Friends data: {friends.Data}" : $"Error: {friends.Error}");

        // getFriendRequestStatus requires a friend userId, skip in this demo
        Console.WriteLine("  (Skipping getFriendRequestStatus - requires friend ID)");

        // getCloseFriends endpoint is deprecated (returns 404), skipping
        Console.WriteLine("  (Skipping getCloseFriends - deprecated endpoint)");

        Console.WriteLine("Press Enter to continue...");
        Console.ReadLine();
    }
}