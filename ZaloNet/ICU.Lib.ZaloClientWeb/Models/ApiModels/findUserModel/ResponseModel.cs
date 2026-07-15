using System.Text.Json.Serialization;

namespace ICU.Lib.ZaloClientWeb.Models.ApiModels.findUserModel
{
    public class ResponseModel
    {
        [JsonPropertyName("avatar")]
        public string avatar { get; set; }

        [JsonPropertyName("cover")]
        public string cover { get; set; }

        [JsonPropertyName("status")]
        public string status { get; set; }

        [JsonPropertyName("gender")]
        public int gender { get; set; }

        [JsonPropertyName("dob")]
        public int dob { get; set; }

        [JsonPropertyName("sdob")]
        public string sdob { get; set; }

        [JsonPropertyName("globalId")]
        public string globalId { get; set; }

        [JsonPropertyName("bizPkg")]
        public BizPkg bizPkg { get; set; }

        [JsonPropertyName("isEnterpriseAccount")]
        public int isEnterpriseAccount { get; set; }

        [JsonPropertyName("uid")]
        public string uid { get; set; }

        [JsonPropertyName("zalo_name")]
        public string zalo_name { get; set; }

        [JsonPropertyName("display_name")]
        public string display_name { get; set; }

        public class BizPkg
        {
            [JsonPropertyName("pkgId")]
            public int pkgId { get; set; }
        }
    }
}
