# ICU.Lib.ZaloClientWeb

[![.NET](https://img.shields.io/badge/.NET-Standard%202.1-512BD4)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**ICU.Lib.ZaloClientWeb** là thư viện .NET không chính thức cho Zalo API, được port từ [zca-js](https://github.com/RFS-ADRENO/zca-js) (TypeScript) sang C#. Cho phép tương tác với Zalo API thông qua các phương thức mạnh mẽ, bao gồm gửi/nhận tin nhắn, quản lý nhóm, bạn bè, xử lý sự kiện real-time qua WebSocket với **strongly-typed event args**.

---

## 🚀 Tính năng chính

- **Authentication**: Cookie login, QR code login
- **Messaging**: Gửi text, sticker, link, video, voice, card, bank card
- **Groups**: Tạo, quản lý nhóm, thêm/xóa thành viên, phân quyền
- **Friends**: Quản lý bạn bè, gửi/chấp nhận/từ chối lời mời kết bạn
- **Real-time WebSocket**: Nhận sự kiện tin nhắn, typing, reaction, seen, delivered, group/friend events với **strongly-typed data** (pattern matching)
- **Encryption**: AES-CBC, AES-GCM đầy đủ (port từ crypto-js)
- **Strongly-typed models**: POCO classes + discriminated union pattern cho event data
- **Mạnh mẽ & mở rộng**: Dễ dàng thêm API methods, tự do mở rộng

---

## 📋 Yêu cầu hệ thống

- [.NET SDK 8.0+](https://dotnet.microsoft.com/download) (hoặc .NET Core 3.1+)
- Target: `netstandard2.1` (tương thích .NET Framework 4.8+, .NET Core 3.0+, .NET 5+)

---

## 📦 Cài đặt

```bash
dotnet add reference ICU.Lib.ZaloClientWeb/ICU.Lib.ZaloClientWeb.csproj
```

NuGet dependencies tự động restore:
```xml
<PackageReference Include="System.Text.Json" Version="8.0.5" />
<PackageReference Include="System.Net.WebSockets.Client" Version="4.3.2" />
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.6" />
<PackageReference Include="QRCoder" Version="1.6.1" />
```

---

## 🏗️ Kiến trúc thư mục

```
ICU.Lib.ZaloClientWeb/
├── ZaloClient.cs               # ZaloClient + ZaloApi (~137 methods)
├── Auth/
│   ├── LoginHelper.cs          # Cookie login
│   └── QrLoginHelper.cs        # QR code login
├── Crypto/
│   ├── AesHelper.cs            # AES-CBC, AES-GCM
│   └── ParamsEncryptor.cs      # Mã hóa tham số Zalo
├── Exceptions/                 # 4 exception classes
├── Models/
│   ├── Auth: Credentials.cs, LoginInfo.cs, ZaloContext.cs, ZaloOptions.cs
│   ├── Messages: Message.cs (UserMessageInfo, GroupMessageInfo), Reactions.cs
│   ├── Events: Typing.cs, Undo.cs, SeenMessage.cs, DeliveredMessage.cs
│   ├── FriendEvent.cs          # 5 typed data classes + FriEvent wrapper
│   ├── GroupEvent.cs           # 7 typed data classes + GroupEvent wrapper
│   ├── User.cs                 # UserInfo, UserBasicInfo, UserSetting
│   ├── GroupInfo.cs            # GroupFullInfo, GroupSetting, GroupTopic
│   ├── ZBusiness.cs            # Business package
│   └── Types/                  # Enums (6 files)
├── Utils/
│   ├── ZaloUtils.cs            # 20+ utility functions
│   ├── ApiMethods.cs           # API call helpers (GET/POST)
│   ├── ImageHelper.cs          # File metadata
│   └── ZaloLogger.cs           # Logging
└── WebSocket/
    └── ZaloListener.cs         # Real-time event listener (10+ strongly-typed events)
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

### 2. Đăng nhập

```csharp
// Cookie login
var api = await client.LoginAsync(new Credentials
{
    Imei = "your-imei",
    UserAgent = "Mozilla/5.0 ...",
    Cookie = new List<CookieItem>
    {
        new() { Name = "zpsid", Value = "...", Domain = ".chat.zalo.me", Path = "/" },
        new() { Name = "zpw_sek", Value = "...", Domain = ".chat.zalo.me", Path = "/" }
    }
});

// Hoặc QR login
var api = await client.LoginWithQrAsync(qrPath: "qr.png");
```

### 3. Gửi tin nhắn

```csharp
var result = await api.SendMessageAsync(
    threadId: "123456789",
    message: "Hello từ C#! 👋",
    threadType: ThreadType.User
);
```

### 4. Lắng nghe sự kiện real-time (strongly-typed events)

```csharp
// === Messages ===
api.Listener.MessageReceived += (sender, args) =>
{
    if (args.Message is UserMessageInfo userMsg)
    {
        Console.WriteLine($"Tin nhắn từ {userMsg.Data.DName}: {userMsg.Data.Content}");
    }
    else if (args.Message is GroupMessageInfo grpMsg)
    {
        Console.WriteLine($"Nhóm {grpMsg.ThreadId}: {grpMsg.Data.DName} nói: {grpMsg.Data.Content}");
    }
};

// === Typing indicators ===
api.Listener.TypingReceived += (sender, args) =>
{
    Console.WriteLine($"{(args.IsGroup ? "Nhóm" : "User")} {args.ThreadId}: {args.Uid} đang gõ...");
};

// === Reactions ===
api.Listener.ReactionReceived += (sender, args) =>
{
    foreach (var reaction in args.Reactions)
    {
        Console.WriteLine($"{reaction.ThreadId}: {reaction.Data.Content?.RIcon}");
    }
};

// === Group events (tất cả typed) ===
api.Listener.GroupEventReceived += (sender, groupEvent) =>
{
    switch (groupEvent.Data)
    {
        case GroupEventBaseData baseData:
            Console.WriteLine($"[{groupEvent.Act}] {baseData.GroupName}");
            break;
        case GroupEventJoinRequestData joinReq:
            Console.WriteLine($"{joinReq.Uids?.Length} người muốn vào nhóm");
            break;
        case GroupEventPinTopicData pinData:
            Console.WriteLine($"{pinData.ActorId} đã ghim bài");
            break;
        case GroupEventRemindRespondData remindResp:
            Console.WriteLine($"Nhắc nhở {remindResp.TopicId}");
            break;
    }
};

// === Friend events (tất cả typed) ===
api.Listener.FriendEventReceived += (sender, friendEvent) =>
{
    switch (friendEvent.Data)
    {
        case FriendEventRequestData req:
            Console.WriteLine($"Lời mời kết bạn từ {req.FromUid}: {req.Message}");
            break;
        case FriendEventUserData userData:
            Console.WriteLine($"{(friendEvent.Type == FriendEventType.Add ? "Kết bạn" : "Hủy kết bạn")} với {userData.Uid}");
            break;
        case FriendEventRejectUndoData rejectData:
            Console.WriteLine($"Từ chối/Hủy lời mời: {rejectData.ToUid}");
            break;
    }
};

// === Undo (thu hồi tin nhắn) ===
api.Listener.UndoReceived += (sender, args) =>
{
    if (args.Undo != null)
        Console.WriteLine($"Tin nhắn {args.Undo.MsgId} đã bị thu hồi");
};

// === Seen/Delivered ===
api.Listener.SeenReceived += (sender, args) =>
    Console.WriteLine($"Đã xem: {args.ThreadId}");

// Start listening
await api.Listener.StartAsync();
await Task.Delay(-1);
```

### 5. Pattern matching với typed events

```csharp
// FriendEvent.Data và GroupEvent.Data là object
// Dùng pattern matching để xử lý từng type:

listener.FriendEventReceived += (_, e) =>
{
    if (e.Data is FriendEventRequestData req && e.Type == FriendEventType.Request)
    {
        // Tự động chấp nhận lời mời kết bạn
        await api.AcceptFriendRequestAsync(long.Parse(req.FromUid));
    }
};

listener.GroupEventReceived += (_, e) =>
{
    if (e.Data is GroupEventBaseData baseData && e.Act == "join")
    {
        Console.WriteLine($"{baseData.SourceId} vừa tham gia {baseData.GroupName}");
    }
};
```

### 6. Gọi API + Deserialize

```csharp
// API trả về ZaloApiResponse<JsonElement>
var response = await api.GetUserInfoAsync(123456);

if (response.IsSuccess)
{
    // Deserialize sang model
    var user = JsonSerializer.Deserialize<UserInfo>(response.Data.GetRawText());
    Console.WriteLine($"User: {user?.DisplayName}");
}
```

---

## 📊 Models và typed data

### FriendEvent — 5 typed data classes cho 12 event types:

| Event Type | Data Class | Properties nổi bật |
|---|---|---|
| Add, Remove, Block, Unblock... | `FriendEventUserData` | `Uid` |
| Request | `FriendEventRequestData` | `ToUid`, `FromUid`, `Src`, `Message` |
| RejectRequest, UndoRequest | `FriendEventRejectUndoData` | `ToUid`, `FromUid` |
| SeenFriendRequest | `FriendEventSeenData` | `Uids[]` |
| PinCreate, PinUnpin | `FriendEventPinData` | `ConversationId`, `ActorId` |

### GroupEvent — 7 typed data classes cho 22 event types:

| Event Type | Data Class | Properties nổi bật |
|---|---|---|
| Join, Leave, RemoveMember, UpdateSetting... | `GroupEventBaseData` | `GroupId`, `GroupName`, `SourceId`, `UpdateMembers`, `GroupSetting` |
| JoinRequest | `GroupEventJoinRequestData` | `Uids[]`, `TotalPending` |
| NewPinTopic, UpdatePinTopic, UnpinTopic | `GroupEventPinTopicData` | `ActorId`, `GroupId`, `Topic` |
| ReorderPinTopic | `GroupEventReorderPinData` | `ActorId`, `GroupId` |
| UpdateBoard, RemoveBoard | `GroupEventBoardData` | `SourceId`, `GroupId`, `GroupName` |
| AcceptRemind, RejectRemind | `GroupEventRemindRespondData` | `TopicId`, `UpdateMembers[]` |
| RemindTopic | `GroupEventRemindTopicData` | `CreatorId`, `GroupId` |

---

## 📊 Danh sách API methods (~137)

| Nhóm | Số lượng | Ví dụ |
|---|---|---|
| **User/Profile** | 12 | `GetUserInfoAsync`, `FindUserAsync`, `UpdateProfileAsync` |
| **Friends** | 19 | `GetAllFriendsAsync`, `SendFriendRequestAsync`, `BlockUserAsync` |
| **Messages** | 16 | `SendMessageAsync`, `SendStickerAsync`, `AddReactionAsync` |
| **Groups** | 27 | `CreateGroupAsync`, `JoinGroupLinkAsync`, `ChangeGroupOwnerAsync` |
| **Conversations** | 15 | `PinConversationsAsync`, `AddUnreadMarkAsync`, `DeleteChatAsync` |
| **Stickers** | 4 | `GetStickersAsync`, `SearchStickerAsync` |
| **Polls** | 6 | `CreatePollAsync`, `VotePollAsync` |
| **Reminders** | 6 | `CreateReminderAsync`, `EditReminderAsync` |
| **Catalogs** | 9 | `CreateCatalogAsync`, `CreateProductCatalogAsync` |
| **Auto Reply** | 4 | `CreateAutoReplyAsync` |
| **Quick Message** | 4 | `AddQuickMessageAsync` |
| **Settings** | 6 | `GetSettingsAsync`, `SetMuteAsync` |
| **Other** | 8 | `KeepAliveAsync`, `LastOnlineAsync`, `ParseLinkAsync` |
| **Custom** | 1 | `CustomApiCallAsync` |

---

## 🔐 Xử lý mã hóa

| JS (crypto-js / Web Crypto) | C# (System.Security.Cryptography) |
|---|---|
| AES-CBC (zero IV, PKCS7) | `AesHelper.EncryptAesCbc` / `DecryptAesCbc` |
| AES-GCM (event data) | `AesHelper.DecryptEventDataGcm` |
| MD5 (sign key) | `System.Security.Cryptography.MD5` |
| Zlib inflate (pako) | `System.IO.Compression.DeflateStream` |

---

## 🤝 Đóng góp

Mọi đóng góp đều được chào đón! Vui lòng fork project và mở Pull Request.

## 📄 Giấy phép

MIT License.

## 🙏 Credits

- [RFS-ADRENO](https://github.com/RFS-ADRENO) — Tác giả [zca-js](https://github.com/RFS-ADRENO/zca-js) gốc
- [@truong9c2208](https://github.com/truong9c2208), [@JustKemForFun](https://github.com/JustKemForFun) — Contributors