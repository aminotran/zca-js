namespace ICU.Lib.ZaloClientWeb.Models.Types;

/// <summary>
/// Bank BIN codes supported by Zalo for bank card transfers.
/// Based on https://developers.zalo.me/docs/zalo-notification-service/phu-luc/danh-sach-bin-code
/// Equivalent to BinBankCard enum in zca-js Enum.ts.
/// </summary>
public enum BinBankCard
{
    /// <summary>NH TMCP An Bình</summary>
    ABBank = 970425,
    /// <summary>NH TMCP Á Châu</summary>
    ACB = 970416,
    /// <summary>NH Nông nghiệp và Phát triển Nông thôn Việt Nam</summary>
    Agribank = 970405,
    /// <summary>NH TMCP Đầu tư và Phát triển Việt Nam</summary>
    BIDV = 970418,
    /// <summary>NH TMCP Bản Việt</summary>
    BVBank = 970454,
    /// <summary>NH TMCP Bắc Á</summary>
    BacA_Bank = 970409,
    /// <summary>NH TMCP Bảo Việt</summary>
    BaoViet_Bank = 970438,
    /// <summary>NH số CAKE by VPBank - TMCP Việt Nam Thịnh Vượng</summary>
    CAKE = 546034,
    /// <summary>NH Thương mại TNHH MTV Xây dựng Việt Nam</summary>
    CB_Bank = 970444,
    /// <summary>NH TNHH MTV CIMB Việt Nam</summary>
    CIMB_Bank = 422589,
    /// <summary>NH Hợp tác xã Việt Nam</summary>
    Coop_Bank = 970446,
    /// <summary>NH TNHH MTV Phát triển Singapore - CN TP. Hồ Chí Minh</summary>
    DBS_Bank = 796500,
    /// <summary>NH TMCP Đông Á</summary>
    DongA_Bank = 970406,
    /// <summary>NH TMCP Xuất Nhập khẩu Việt Nam</summary>
    Eximbank = 970431,
    /// <summary>NH TMCP Dầu khí Toàn cầu</summary>
    GPBank = 970408,
    /// <summary>NH TMCP Phát triển TP. Hồ Chí Minh</summary>
    HDBank = 970437,
    /// <summary>NH TNHH MTV HSBC (Việt Nam)</summary>
    HSBC = 458761,
    /// <summary>NH TNHH MTV Hong Leong Việt Nam</summary>
    HongLeong_Bank = 970442,
    /// <summary>NH Công nghiệp Hàn Quốc - CN TP. Hồ Chí Minh</summary>
    IBK_HCM = 970456,
    /// <summary>NH Công nghiệp Hàn Quốc - CN Hà Nội</summary>
    IBK_HN = 970455,
    /// <summary>NH TNHH Indovina</summary>
    Indovina_Bank = 970434,
    /// <summary>NH Đại chúng TNHH Kasikornbank - CN TP. Hồ Chí Minh</summary>
    KBank = 668888,
    /// <summary>NH TMCP Kiên Long</summary>
    KienlongBank = 970452,
    /// <summary>NH Kookmin - CN TP. Hồ Chí Minh</summary>
    Kookmin_Bank_HCM = 970463,
    /// <summary>NH Kookmin - CN Hà Nội</summary>
    Kookmin_Bank_HN = 970462,
    /// <summary>NH TMCP Lộc Phát Việt Nam</summary>
    LPBank = 970449,
    /// <summary>NH TMCP Quân đội</summary>
    MB_Bank = 970422,
    /// <summary>NH TMCP Hàng Hải</summary>
    MSB = 970426,
    /// <summary>NH TMCP Quốc Dân</summary>
    NCB = 970419,
    /// <summary>NH TMCP Nam Á</summary>
    Nam_A_Bank = 970428,
    /// <summary>NH Nonghyup - CN Hà Nội</summary>
    NongHyup_Bank = 801011,
    /// <summary>NH TMCP Phương Đông</summary>
    OCB = 970448,
    /// <summary>NH Thương mại TNHH MTV Đại Dương</summary>
    Ocean_Bank = 970414,
    /// <summary>NH TMCP Thịnh vượng và Phát triển</summary>
    PGBank = 970430,
    /// <summary>NH TMCP Đại Chúng Việt Nam</summary>
    PVcomBank = 970412,
    /// <summary>NH TNHH MTV Public Việt Nam</summary>
    Public_Bank_Vietnam = 970439,
    /// <summary>NH TMCP Sài Gòn</summary>
    SCB = 970429,
    /// <summary>NH TMCP Sài Gòn - Hà Nội</summary>
    SHB = 970443,
    /// <summary>NH TMCP Sài Gòn Thương Tín</summary>
    Sacombank = 970403,
    /// <summary>NH TMCP Sài Gòn Công Thương</summary>
    Saigon_Bank = 970400,
    /// <summary>NH TMCP Đông Nam Á</summary>
    SeABank = 970440,
    /// <summary>NH TNHH MTV Shinhan Việt Nam</summary>
    Shinhan_Bank = 970424,
    /// <summary>NH TNHH MTV Standard Chartered Bank Việt Nam</summary>
    Standard_Chartered_Vietnam = 970410,
    /// <summary>NH số TNEX</summary>
    TNEX = 9704261,
    /// <summary>NH TMCP Tiên Phong</summary>
    TPBank = 970423,
    /// <summary>NH TMCP Kỹ thương Việt Nam</summary>
    Techcombank = 970407,
    /// <summary>NH số Timo by Bản Việt Bank</summary>
    Timo = 963388,
    /// <summary>NH số UBank by VPBank</summary>
    UBank = 546035,
    /// <summary>NH United Overseas Bank Việt Nam</summary>
    United_Overseas_Bank_Vietnam = 970458,
    /// <summary>NH TMCP Quốc tế Việt Nam</summary>
    VIB = 970441,
    /// <summary>NH TMCP Việt Nam Thịnh Vượng</summary>
    VPBank = 970432,
    /// <summary>NH Liên doanh Việt - Nga</summary>
    VRB = 970421,
    /// <summary>NH TMCP Việt Á</summary>
    VietABank = 970427,
    /// <summary>NH TMCP Việt Nam Thương Tín</summary>
    VietBank = 970433,
    /// <summary>NH TMCP Ngoại Thương Việt Nam</summary>
    Vietcombank = 970436,
    /// <summary>NH TMCP Công thương Việt Nam</summary>
    VietinBank = 970415,
    /// <summary>NH TNHH MTV Woori Việt Nam</summary>
    Woori_Bank = 970457,
}