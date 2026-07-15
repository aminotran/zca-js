using ICU.Lib.ZaloClientWeb.Models.Types;

namespace ICU.Lib.ZaloClientWeb.Models.ApiModels.findUserByUsernameModel
{
    public class RequestModel
    {
        public string user_name { get; set; }
        public int avatar_size { get; set; } = (int)AvatarSize.Small;
    }
}
