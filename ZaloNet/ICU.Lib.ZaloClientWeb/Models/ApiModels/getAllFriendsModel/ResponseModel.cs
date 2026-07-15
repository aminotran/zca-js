namespace ICU.Lib.ZaloClientWeb.Models.ApiModels.getAllFriendsModel
{
    public class ResponseModel
    {
        public string userId { get; set; }
        public string username { get; set; }
        public string displayName { get; set; }
        public string zaloName { get; set; }
        public string avatar { get; set; }
        public string bgavatar { get; set; }
        public string cover { get; set; }
        public int gender { get; set; }
        public int dob { get; set; }
        public string sdob { get; set; }
        public string status { get; set; }
        public string phoneNumber { get; set; }
        public int isFr { get; set; }
        public int isBlocked { get; set; }
        public long lastActionTime { get; set; }
        public long lastUpdateTime { get; set; }
        public int isActive { get; set; }
        public int key { get; set; }
        public int type { get; set; }
        public int isActivePC { get; set; }
        public int isActiveWeb { get; set; }
        public int isValid { get; set; }
        public string userKey { get; set; }
        public int accountStatus { get; set; }
        public object oaInfo { get; set; }
        public int user_mode { get; set; }
        public string globalId { get; set; }
        public BizPkg bizPkg { get; set; }
        public int createdTs { get; set; }
        public int isEnterpriseAccount { get; set; }
        public object oa_status { get; set; }

        public class BizPkg
        {
            public Label label { get; set; }
            public int pkgId { get; set; }

            public class Label
            {
                public string VI { get; set; }
                public string EN { get; set; }
            }
        }
    }
}
