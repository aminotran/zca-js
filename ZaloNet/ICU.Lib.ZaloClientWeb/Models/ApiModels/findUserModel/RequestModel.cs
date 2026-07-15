using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Models.ApiModels.findUserModel
{
    public class RequestModel
    {
        public string phone { get; set; }
        public int avatar_size { get; set; } = (int)AvatarSize.Small;
        public string language { get; set; }
        public string imei { get; set; }
        public int reqSrc { get; set; }
    }
}
