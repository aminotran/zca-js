using System.Collections.Generic;

namespace ICU.Lib.ZaloClientWeb.Models;

/// <summary>
/// Login information returned by Zalo API after successful authentication.
/// </summary>
public class LoginInfo
{
    public long Uid { get; set; }
    public string? ZpwEnk { get; set; }
    public string? ZpwWs { get; set; }
    public Dictionary<string, string[]> ZpwServiceMapV3 { get; set; } = new();
    public string? Send2MeId { get; set; }
    public string? Language { get; set; }
    public string? PublicIp { get; set; }
    public int Haspcclient { get; set; }
}