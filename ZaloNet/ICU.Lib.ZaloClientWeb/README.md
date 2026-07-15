# ICU.Lib.ZaloClientWeb

[![.NET](https://img.shields.io/badge/.NET-Standard%202.1-512BD4)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

**ICU.Lib.ZaloClientWeb** là thư viện .NET không chính thức cho Zalo API, được port từ [zca-js](https://github.com/RFS-ADRENO/zca-js) (TypeScript) sang C#. Cho phép tương tác với Zalo API thông qua các phương thức mạnh mẽ, bao gồm gửi/nhận tin nhắn (text, sticker, link, video, voice, card, ảnh, file), quản lý nhóm, bạn bè, xử lý sự kiện real-time qua WebSocket với **strongly-typed event args**.

---

## 🚀 Tính năng chính

- **Authentication**: Cookie login, QR code login
- **Messaging**: Gửi text, sticker, link, video, voice, card, bank card, ảnh, file
- **Advanced Messaging**: Mention (@user), Quote (trả lời), TextStyle (Bold, Italic, Underline...), Urgency
- **Media Upload/Download**: Upload ảnh (jpg, png, webp, gif), video (mp4), file bất kỳ với chunk upload
- **Groups**: Tạo, quản lý nhóm, thêm/xóa thành viên, phân quyền
- **Friends**: Quản lý bạn bè, gửi/chấp nhận/từ chối lời mời kết bạn
- **Real-time WebSocket**: Nhận sự kiện tin nhắn, typing, reaction, seen, delivered, group/friend events với **strongly-typed data** (pattern matching)
- **Encryption**: AES-CBC, AES-GCM đầy đủ (port từ crypto-js)
- **Strongly-typed models**: POCO classes + discriminated union pattern cho event data
- **DI support**: `ZaloApiClient` injectable, giảm trùng lặp tham số khi gọi API
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
<PackageReference Include="System.Text.Json" Version="9.0.1" />
<PackageReference Include="System.Net.WebSockets.Client" Version="4.3.2" />
<PackageReference Include="System.Security.Cryptography.Algorithms" Version="4.3.1" />
<PackageReference Include="System.Threading.Channels" Version="9.0.1" />
<PackageReference Include="SixLabors.ImageSharp" Version="3.1.7" />
<PackageReference Include="QRCoder" Version="1.7.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.1" />
```

---

## 🏗️ Kiến trúc thư mục

```
ICU.Lib.ZaloClientWeb/
├── ZaloClient.cs               # ZaloClient + ZaloApi (~150 methods)
├── Auth/
│   ├── LoginHelper.cs          # Cookie login
│   └── QrLoginHelper.cs        # QR code login
├── Crypto/
│   ├── AesHelper.cs            # AES-CBC, AES-GCM
│   └── ParamsEncryptor.cs      # Mã hóa tham số Zalo
├── Exceptions/                 # 4 exception classes
├── Models/
│   ├── Auth: Credentials.cs, LoginInfo.cs, ZaloContext.cs, ZaloOptions.cs
│   ├── Messages: Message.cs (UserMessageInfo, GroupMessageInfo), MessageContent.cs (TextStyle, Style, Urgency, Mention), SendMessageResult.cs
│   ├── Events: Typing.cs, Undo.cs, SeenMessage.cs, DeliveredMessage.cs
│   ├── FriendEvent.cs          # 5 typed data classes + FriEvent wrapper
│   ├── GroupEvent.cs           # 7 typed data classes + GroupEvent wrapper
│   ├── UploadAttachmentResponse.cs  # Upload response types (image, video, file)
│   ├── ShareFileSettings.cs    # Strongly-typed file sharing settings
│   ├── QrLoginEvent.cs         # QR login event data
│   ├── User.cs                 # UserInfo, UserBasicInfo, UserSetting
│   ├── GroupInfo.cs            # GroupFullInfo, GroupSetting, GroupTopic
│   ├── ZBusiness.cs            # Business package
│   └── Types/                  # Enums (6 files)
├── Utils/
│   ├── ZaloUtils.cs            # 20+ utility functions
│   ├── ZaloApiClient.cs        # Injectable API client (DI-friendly, replaces ApiMethods)
│   ├── ApiMethods.cs           # [Deprecated] static API helpers — use ZaloApiClient instead
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

### 3. Gửi tin nhắn cơ bản

```csharp
// Text đơn giản (string → MessageContent tự động cast)
var result = await api.SendMessageAsync(
    threadId: "123456789",
    message: "Hello từ C#! 👋",
    threadType: ThreadType.User
);
```

### 4. Gửi tin nhắn nâng cao (Mention, Quote, Style, Urgency)

```csharp
// Tạo message với đầy đủ tính năng
var msg = new MessageContent
{
    Msg = "Xin chào @Minh, đây là tin nhắn quan trọng!",
    
    // Mention trong group (@user)
    Mentions = new List<MessageMention>
    {
        new() { Pos = 9, Uid = "123456789", Len = 5 }
    },
    
    // Text styles (Bold, Italic, Underline, Colors...)
    Styles = new List<Style>
    {
        new(TextStyle.Bold, 0, 8),        // "Xin chào" in đậm
        new(TextStyle.Red, 29, 18)        // "tin nhắn quan trọng" màu đỏ
    },
    
    // Urgency
    Urgency = Urgency.Important,
    
    // Time-to-live (ms)
    Ttl = 60000
};

var result = await api.SendMessageAsync(msg, threadId, ThreadType.Group);
```

### 5. Gửi link (tự động parse metadata)

```csharp
var result = await api.SendLinkAsync(
    threadId: "123456789",
    link: "https://example.com",
    msg: "Xem bài viết này!",
    threadType: ThreadType.User
);
```

### 6. Gửi video, voice, card

```csharp
// Video
await api.SendVideoAsync(
    threadId, videoUrl, thumbnailUrl,
    msg: "Video hay!", duration: 120, width: 1920, height: 1080
);

// Voice
await api.SendVoiceAsync(
    threadId, voiceUrl, duration: 30, msg: "Tin nhắn thoại"
);

// Contact card
await api.SendCardAsync(
    threadId, userId: 123456789, msg: "Liên hệ của tôi"
);
```

### 7. Upload ảnh/file và gửi

```csharp
// Upload file (jpg, png, webp, gif, mp4...)
var uploadResults = await api.UploadAttachmentAsync(
    new object[] { @"C:\path\to\image.jpg" },
    threadId: "123456789",
    type: ThreadType.User
);

// Gửi attachment đã upload
if (uploadResults.Count > 0)
{
    var result = await api.SendAttachmentMessageAsync(
        uploadResult: uploadResults[0],
        threadId: "123456789",
        message: "Ảnh đây!",
        threadType: ThreadType.User
    );
}
```

### 8. Lắng nghe sự kiện real-time (strongly-typed events)

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

### 9. Pattern matching với typed events

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

### 10. Dependency Injection với ZaloApiClient

```csharp
// ZaloApiClient hỗ trợ DI, giúp giảm trùng lặp tham số khi inject
// Thay vì gọi ApiMethods.CallGetApiAsync(ctx, httpClient, endpoint, params)
// bạn inject ZaloApiClient và gọi trực tiếp:

public class MyService
{
    private readonly ZaloApiClient _apiClient;

    public MyService(ZaloApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task DoSomething()
    {
        var result = await _apiClient.CallGetApiAsync("fetchAccountInfo");
        // Không cần truyền ZaloContext hay HttpClient nữa
    }
}

// Đăng ký trong DI container:
// services.AddSingleton(sp => new ZaloApiClient(context, httpClient));
```

Bạn cũng có thể truy cập `ZaloApiClient` thông qua `ZaloApi.ApiClient`:

```csharp
var api = await client.LoginAsync(credentials);
var apiClient = api.ApiClient; // ZaloApiClient instance
var result = await apiClient.CallGetApiAsync("fetchAccountInfo");
```

### 11. Gọi API + Deserialize

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

### MessageContent — Models cho gửi tin nhắn nâng cao:

| Model | Mô tả |
|---|---|
| `TextStyle` | Enum: Bold, Italic, Underline, StrikeThrough, Red, Orange, Yellow, Green, Small, Big, UnorderedList, OrderedList, Indent |
| `Style` | Style range (start, len, textStyle) |
| `Urgency` | Default, Important, Urgent |
| `MessageMention` | Mention (@user) với pos, uid, len |
| `SendMessageQuote` | Quote (trả lời) với msgId, uidFrom, content... |
| `MessageContent` | Full content: msg + styles + mentions + quote + urgency + ttl |

---

## 📊 Danh sách API methods (~150)

| Nhóm | Số lượng | Ví dụ |
|---|---|---|
| **User/Profile** | 12 | `GetUserInfoAsync`, `FindUserAsync`, `UpdateProfileAsync` |
| **Friends** | 19 | `GetAllFriendsAsync`, `SendFriendRequestAsync`, `BlockUserAsync` |
| **Messages** | 16 | `SendMessageAsync`, `SendStickerAsync`, `AddReactionAsync` |
| **Media & Files** | 6 | `SendLinkAsync`, `SendVideoAsync`, `SendVoiceAsync`, `SendCardAsync`, `UploadAttachmentAsync`, `SendAttachmentMessageAsync` |
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


