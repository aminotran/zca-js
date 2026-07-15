namespace ICU.Lib.ZaloClientWeb.Models.ApiModels.getUserInfoModel
{
    public class RequestModel
    {
        public int phonebook_version { get; set; }
        public List<string> friend_pversion_map { get; set; }
        public int avatar_size { get; set; }
        public string language { get; set; }
        public int show_online_status { get; set; } = 1;
        public string imei { get; set; }

    }
}
