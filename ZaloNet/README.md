# ICU.Lib.ZaloClientWeb

[![.NET](https://img.shields.io/badge/.NET-Standard%202.1-512BD4)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**ICU.Lib.ZaloClientWeb** là thư viện .NET không chính thức cho Zalo API, được port từ [zca-js](https://github.com/RFS-ADRENO/zca-js) (TypeScript) sang C#. Cho phép tương tác với Zalo API thông qua các phương thức mạnh mẽ, bao gồm gửi/nhận tin nhắn, quản lý nhóm, bạn bè, xử lý sự kiện real-time qua WebSocket, v.v.

---

## 🚀 Tính năng chính

- **Authentication**: Cookie login, QR code login
- **Messaging**: Gửi text, sticker, link, video, voice, card, bank card
- **Groups**: Tạo, quản lý nhóm, thêm/xóa thành viên, phân quyền
- **Friends**: Quản lý bạn bè, gửi/chấp nhận/từ chối lời mời kết bạn
- **Real-time WebSocket**: Nhận sự kiện tin nhắn, typing, reaction, seen, delivered, group/friend events
- **Encryption**: AES-CBC, AES-GCM đầy đủ (port từ crypto-js)
- **Strongly-typed models**: POCO classes cho tất cả response types
- **Mạnh mẽ & mở rộng**: Dễ dàng thêm API methods, tự do mở rộng

---

## 📋 Yêu cầu hệ thống

- [.NET SDK 8.0+](https://dotnet.microsoft.com/download) (hoặc .NET Core 3.1+)
- Target: `netstandard2.1` (tương thích .NET Framework 4.8+, .NET Core 3.0+, .NET 5+)

---

## 📦 Cài đặt

### Thêm vào project

```bash
# Thêm reference trực tiếp từ project
dotnet add reference ICU.Lib.ZaloClientWeb/ICU.Lib.ZaloClientWeb.csproj

# Hoặc build NuGet package
dotnet pack -c Release
```

### NuGet dependencies tự động được restore

```xml
<PackageReference Include="System.Text.Json" Version="8.0.5" />
<PackageReference Include="System.Net.WebSockets.Client" Version="4.3.2" />
<PackageReference Include="System.Security.Cryptography.Algorithms" Version="4.3.1" />
<PackageReference Include="System.Threading.Channels" Version="8.0.0" />
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.6" />
<PackageReference Include="QRCoder" Version="1.6.1" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.2" />
```

---

## 🏗️ Kiến trúc thư mục

```
ICU.Lib.ZaloClientWeb/
├── ZaloClient.cs               # Lớp chính: ZaloClient + ZaloApi (~100+ methods)
├── Auth/
│   ├── LoginHelper.cs          # Cookie login
│   └── QrLoginHelper.cs        # QR code login
├── Crypto/
│   ├── AesHelper.cs            # AES-CBC, AES-GCM
│   └── ParamsEncryptor.cs      # Mã hóa tham số Zalo
├── Exceptions/
│   ├── ZaloApiException.cs
│   ├── ZaloApiMissingImageMetadataGetter.cs
│   ├── ZaloApiLoginQrAborted.cs
│   └── ZaloApiLoginQrDeclined.cs
├── Models/
│   ├── Message.cs              # UserMessageInfo, GroupMessageInfo
│   ├── Reactions.cs            # Reaction + 50+ reaction constants
│   ├── Typing.cs               # UserTypingEvent, GroupTypingEvent
│   ├── Undo.cs                 # UndoEvent (message recall)
│   ├── User.cs                 # UserInfo, UserBasicInfo, UserSetting
│   ├── GroupInfo.cs            # GroupFullInfo, GroupSetting, GroupTopic
│   ├── GroupEvent.cs           # GroupEvent + Initialize factory
│   ├── FriendEvent.cs          # FriendEvent + Initialize factory
│   ├── SeenMessage.cs          # Seen events
│   ├── DeliveredMessage.cs     # Delivered events
│   ├── ZBusiness.cs            # Business package info
│   ├── Credentials.cs          # Login credentials
│   ├── LoginInfo.cs            # Login response
│   ├── ZaloContext.cs          # Session context
│   ├── ZaloOptions.cs          # Configuration
│   └── Types/                  # Enums: ThreadType, DestType, Gender,...
├── Utils/
│   ├── ZaloUtils.cs            # 20+ utility functions
│   ├── ApiMethods.cs           # API call helpers
│   ├── ImageHelper.cs          # File metadata
│   └── ZaloLogger.cs           # Logging
└── WebSocket/
    └── ZaloListener.cs         # Real-time event listener
```

---

## 📖 Hướng dẫn sử dụng

### 1. Khởi tạo client

```csharp
using ICU.Lib.ZaloClientWeb;
using ICU.Lib.ZaloClientWeb.Models;

var client = new ZaloClient(new ZaloOptions
{
    Logging = true,
    ApiType = 30,
    ApiVersion = 671
});
```

### 2. Đăng nhập bằng Cookie

```csharp
var credentials = new Credentials
{
    Imei = "your-imei-string",
    UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ...",
    Cookie = new List<CookieItem>
    {
        new() { Name = "zpsid", Value = "...", Domain = ".chat.zalo.me", Path = "/" },
        new() { Name = "zpw_sek", Value = "...", Domain = ".chat.zalo.me", Path = "/" },
        // ... thêm các cookies khác từ browser
    }
};

var api = await client.LoginAsync(credentials);
```

### 3. Đăng nhập bằng QR Code

```csharp
var api = await client.LoginWithQrAsync(
    qrPath: "qr.png",              // Lưu QR code ra file
    onQrCodeGenerated: (qrUrl) =>
    {
        Console.WriteLine($"Quét mã QR tại: {qrUrl}");
    }
);
```

### 4. Gửi tin nhắn

```csharp
// Gửi tin nhắn text
var result = await api.SendMessageAsync(
    threadId: "123456789",         // UID bạn bè hoặc Group ID
    message: "Hello từ C#! 👋",
    threadType: ThreadType.User    // hoặc ThreadType.Group
);

if (result.IsSuccess)
{
    Console.WriteLine("Gửi tin nhắn thành công!");
}
```

### 5. Lấy thông tin user

```csharp
var response = await api.GetUserInfoAsync(userId: 123456789);

if (response.IsSuccess)
{
    var displayName = response.Data.GetProperty("display_name").GetString();
    var avatar = response.Data.GetProperty("avatar").GetString();

    // Hoặc deserialize sang model
    var user = System.Text.Json.JsonSerializer.Deserialize<UserInfo>(
        response.Data.GetRawText()
    );
    Console.WriteLine($"User: {user?.DisplayName} - {user?.PhoneNumber}");
}
```

### 6. Lắng nghe sự kiện real-time

```csharp
// Đăng ký events trước khi start
api.Listener.MessageReceived += (sender, args) =>
{
    Console.WriteLine($"Tin nhắn mới: {args.Data}");
};

api.Listener.TypingReceived += (sender, args) =>
{
    Console.WriteLine("Ai đó đang gõ...");
};

api.Listener.Connected += (sender, args) =>
{
    Console.WriteLine("Đã kết nối WebSocket!");
};

api.Listener.Disconnected += (sender, args) =>
{
    Console.WriteLine($"Mất kết nối: {args.CloseReason}");
};

// Start listener
await api.Listener.StartAsync();

// Giữ kết nối
await Task.Delay(-1);
```

### 7. Quản lý nhóm

```csharp
// Tạo nhóm
var newGroup = await api.CreateGroupAsync("Nhóm .NET", new List<long> { 111, 222, 333 });

// Lấy thông tin nhóm
var groupInfo = await api.GetGroupInfoAsync("groupId-here");

// Thêm thành viên
await api.AddUserToGroupAsync("groupId", userId: 444);

// Rời nhóm
await api.LeaveGroupAsync("groupId");
```

### 8. Xử lý response

```csharp
// Pattern chung cho tất cả API
var result = await api.GetUserInfoAsync(123456);

if (result.IsSuccess)
{
    // result.Data là JsonElement - có thể inspect hoặc deserialize
    var json = result.Data.GetRawText();
    var myModel = JsonSerializer.Deserialize<MyModel>(json);
}
else
{
    Console.WriteLine($"Lỗi {result.ErrorCode}: {result.Error}");
}
```

---

## 📊 Danh sách API methods

| Nhóm | Số lượng | Phương thức |
|---|---|---|
| **User/Profile** | 12 | `GetUserInfoAsync`, `FindUserAsync`, `UpdateProfileAsync`, `ChangeAccountAvatarAsync`,... |
| **Friends** | 19 | `GetAllFriendsAsync`, `SendFriendRequestAsync`, `AcceptFriendRequestAsync`, `BlockUserAsync`,... |
| **Messages** | 16 | `SendMessageAsync`, `SendStickerAsync`, `SendVideoAsync`, `AddReactionAsync`,... |
| **Groups** | 27 | `CreateGroupAsync`, `AddUserToGroupAsync`, `ChangeGroupNameAsync`, `JoinGroupLinkAsync`,... |
| **Conversations** | 15 | `GetConversationAsync`, `SetPinnedConversationsAsync`, `AddUnreadMarkAsync`,... |
| **Stickers** | 4 | `GetStickersAsync`, `SearchStickerAsync`,... |
| **Polls** | 6 | `CreatePollAsync`, `VotePollAsync`, `LockPollAsync`,... |
| **Reminders** | 6 | `CreateReminderAsync`, `EditReminderAsync`,... |
| **Catalogs** | 9 | `CreateCatalogAsync`, `CreateProductCatalogAsync`,... |
| **Auto Reply** | 4 | `CreateAutoReplyAsync`, `GetAutoReplyListAsync`,... |
| **Quick Message** | 4 | `AddQuickMessageAsync`, `GetQuickMessageListAsync`,... |
| **Settings** | 6 | `GetSettingsAsync`, `SetMuteAsync`, `UpdateActiveStatusAsync`,... |
| **Other** | 8 | `KeepAliveAsync`, `LastOnlineAsync`, `GetQrAsync`, `ParseLinkAsync`,... |
| **Custom** | 1 | `CustomApiCallAsync` |
| **Total** | **~137** | |

---

## 🔐 Xử lý mã hóa

Thư viện sử dụng các thuật toán mã hóa tương thích với zca-js gốc:

| JS (crypto-js / Web Crypto) | C# (System.Security.Cryptography) |
|---|---|
| AES-CBC (zero IV, PKCS7) | `AesHelper.EncryptAesCbc` / `DecryptAesCbc` |
| AES-GCM (event data) | `AesHelper.DecryptEventDataGcm` |
| MD5 (sign key) | `System.Security.Cryptography.MD5` |
| Zlib inflate (pako) | `System.IO.Compression.DeflateStream` |

---

## 🤝 Đóng góp

Mọi đóng góp đều được chào đón! Vui lòng:

1. Fork project
2. Tạo branch feature (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m 'Add some AmazingFeature'`)
4. Push lên branch (`git push origin feature/AmazingFeature`)
5. Mở Pull Request

---

## 📄 Giấy phép

Distributed under the MIT License. See `LICENSE` for more information.

---

## 🙏 Credits

- [RFS-ADRENO](https://github.com/RFS-ADRENO) - Tác giả [zca-js](https://github.com/RFS-ADRENO/zca-js) gốc
- [@truong9c2208](https://github.com/truong9c2208), [@JustKemForFun](https://github.com/JustKemForFun) - Contributors