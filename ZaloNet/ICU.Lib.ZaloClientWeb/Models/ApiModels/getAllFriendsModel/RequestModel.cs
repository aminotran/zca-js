namespace ICU.Lib.ZaloClientWeb.Models.ApiModels.getAllFriendsModel
{
    public class RequestModel
    {
        public int incInvalid { get; set; } = 1;
        public int page { get; set; }
        public int count { get; set; }
        public int avatar_size { get; set; }
        public int actiontime { get; set; } = 0;
        public string imei { get; set; }

    }
}
