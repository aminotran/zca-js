using System.Text.Json.Serialization;

namespace ICU.Lib.ZaloClientWeb.Models.ApiModels.getUserInfoModel
{
    public class ResponseModel
    {
        // Sử dụng Dictionary để hứng các key động (ví dụ: "297940807224808271")
        [JsonPropertyName("unchanged_profiles")]
        public Dictionary<string, UserProfile> UnchangedProfiles { get; set; }

        [JsonPropertyName("phonebook_version")]
        public long PhonebookVersion { get; set; }

        // Sử dụng Dictionary tương tự cho changed_profiles
        [JsonPropertyName("changed_profiles")]
        public Dictionary<string, UserProfile> ChangedProfiles { get; set; }


        public class BizPkg
        {
            [JsonPropertyName("label")]
            public Label label { get; set; }

            [JsonPropertyName("pkgId")]
            public int PkgId { get; set; }

            public class Label
            {
                public string VI { get; set; }
                public string EN { get; set; }
            }
        }

        public class UserProfile
        {
            [JsonPropertyName("userId")]
            public string UserId { get; set; }

            [JsonPropertyName("username")]
            public string Username { get; set; }

            [JsonPropertyName("displayName")]
            public string DisplayName { get; set; }

            [JsonPropertyName("zaloName")]
            public string ZaloName { get; set; }

            [JsonPropertyName("avatar")]
            public string Avatar { get; set; }

            [JsonPropertyName("bgavatar")]
            public string Bgavatar { get; set; }

            [JsonPropertyName("cover")]
            public string Cover { get; set; }

            [JsonPropertyName("gender")]
            public int Gender { get; set; }

            [JsonPropertyName("dob")]
            public int Dob { get; set; }

            [JsonPropertyName("sdob")]
            public string Sdob { get; set; }

            [JsonPropertyName("status")]
            public string Status { get; set; }

            [JsonPropertyName("phoneNumber")]
            public string PhoneNumber { get; set; }

            [JsonPropertyName("isFr")]
            public int IsFr { get; set; }

            [JsonPropertyName("isBlocked")]
            public int IsBlocked { get; set; }

            [JsonPropertyName("lastActionTime")]
            public long LastActionTime { get; set; }

            [JsonPropertyName("lastUpdateTime")]
            public long LastUpdateTime { get; set; }

            [JsonPropertyName("isActive")]
            public int IsActive { get; set; }

            [JsonPropertyName("key")]
            public int Key { get; set; }

            [JsonPropertyName("type")]
            public int Type { get; set; }

            [JsonPropertyName("isActivePC")]
            public int IsActivePC { get; set; }

            [JsonPropertyName("isActiveWeb")]
            public int IsActiveWeb { get; set; }

            [JsonPropertyName("isValid")]
            public int IsValid { get; set; }

            [JsonPropertyName("userKey")]
            public string UserKey { get; set; }

            [JsonPropertyName("accountStatus")]
            public int AccountStatus { get; set; }

            [JsonPropertyName("oaInfo")]
            public object OaInfo { get; set; }

            [JsonPropertyName("user_mode")]
            public int UserMode { get; set; }

            [JsonPropertyName("globalId")]
            public string GlobalId { get; set; }

            [JsonPropertyName("bizPkg")]
            public BizPkg BizPkg { get; set; }

            [JsonPropertyName("createdTs")]
            public long CreatedTs { get; set; }

            [JsonPropertyName("isEnterpriseAccount")]
            public int IsEnterpriseAccount { get; set; }

            [JsonPropertyName("oa_status")]
            public object OaStatus { get; set; }
        }
    }
}
