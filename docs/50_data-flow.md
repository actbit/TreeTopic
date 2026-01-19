# データフロー

## TL;DR

TreeTopicの主要ユースケース（ルーム作成、メッセージ送信、認証）の詳細な処理フローを説明します。各フローはController→Service→Repository→DBの順で実行され、SignalRによるリアルタイム更新が組み込まれています。

## ユースケース1: ルーム作成・管理

### 概要
ユーザーが新しい議論ルームを作成するまでの完全な処理フロー。

### 処理フロー詳細

```mermaid
sequenceDiagram
    participant Client as クライアント(SvelteKit)
    participant Ctrl as RoomController
    participant Auth as 認証ミドルウェア
    participant Service as RoomManagementService
    participant Repo as RoomRepository
    participant DB as PostgreSQL
    participant SignalR as MessageHub/RoomTopicHub
    participant File as ファイルシステム

    Client->>+Ctrl: POST /{tenant}/api/room
    Note over Client,Ctrl: リクエスト: {"name": "新しいルーム", "description": "議論用"}

    Auth->>Auth: クッキー認証検証
    Auth-->>-Ctrl: 認証成功 → CurrentUserId取得

    Ctrl->>Service: CreateRoomAsync(request, CurrentUserId)
    Note over Ctrl,Service: RoomManagementService呼び出し

    Service->>Service: ExecuteAsyncでラップ
    Note over Service: エラーハンドリングとトランザクション開始

    Service->>Service: Roomエンティティ生成
    Note over Service: Name, Description設定
    Service->>Service: CreatedBy = CurrentUserId
    Service->>Service: RoomPermissionの作成（オーナー権限）

    Service->>Repo: AddAsync(room)
    Note over Service,Repo: Room + RoomPermissionを一括登録

    Repo->>DB: INSERT INTO Rooms
    Repo->>DB: INSERT INTO RoomPermissions
    DB-->>Repo: 保存完了

    Service->>Service: MapToDto(room)
    Note over Service: レスポンス用DTOに変換

    Service->>SignalR: BroadcastRoomCreatedAsync(roomDto)
    Note over Service,SignalR: 全クライアントにルーム作成イベント通知

    Service-->>-Ctrl: Result<RoomDto>.Success
    Ctrl-->>-Client: 201 Created
    Note over Client: UIでルーム一覧更新

    alt ファイルアップロードあり
        Client->>+Ctrl: POST /{tenant}/api/room/{id}/icon
        Ctrl->>+File: アイコン画像保存
        File-->>-Ctrl: 保存完了
        Ctrl->>Service: UpdateRoomIconAsync
        Service->>Repo: UpdateAsync
        Repo->>DB: UPDATE Rooms
        DB-->>Repo: 更新完了
        Service->>SignalR: BroadcastRoomUpdatedAsync
        Service-->>-Ctrl: 更新完了
        Ctrl-->>-Client: 200 OK
    end
```

### 重要な処理ステップ

#### 1. 認証処理
```csharp
// RoomController.cs
[Authorize]
[HttpPost]
public async Task<IActionResult> CreateRoom(CreateRoomRequest request)
{
    // CurrentUserIdはAuthorize属性で自動設定
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    // ...
}
```

#### 2. ビジネスロジック
```csharp
// RoomManagementService.cs
public async Task<Result<RoomDto>> CreateRoomAsync(CreateRoomRequest request, Guid userId)
{
    return await ExecuteAsync(async () =>
    {
        var room = new Room
        {
            Name = request.Name,
            Description = request.Description,
            IsPublic = request.IsPublic,
            CreatedBy = userId,
            Created = DateTime.UtcNow
        };

        // オーナー権限の自動作成
        var roomPermission = new RoomPermission
        {
            RoomId = room.Id,
            UserId = userId,
            Role = Role.Owner,
            Added = DateTime.UtcNow
        };

        await _roomRepository.AddAsync(room);
        await _roomPermissionRepository.AddAsync(roomPermission);

        return MapToDto(room);
    });
}
```

#### 3. データ永続化
```csharp
// RoomRepository.cs
public async Task AddAsync(Room room)
{
    await _context.Rooms.AddAsync(room);
    await _context.SaveChangesAsync();
}
```

#### 4. リアルタイム通知
```csharp
// RoomTopicHub.cs
public async Task BroadcastRoomCreatedAsync(RoomDto room)
{
    await Clients.Group($"tenant-{room.TenantId}")
        .SendAsync("RoomCreated", room);
}
```

### エラーハンドリング
- **入力バリデーション**: Room名の重複チェック
- **権限チェック**: 作成権限の確認
- **データベース制約**: 同時作成の競合検出
- **ファイル処理**: アイコン画像のサイズ・形式チェック

## ユースケース2: メッセージ送信・編集

### 概要
ユーザーがトピック内にメッセージを投稿し、リアルタイムで配信されるまでのフロー。

### 処理フロー詳細

```mermaid
sequenceDiagram
    participant Client as クライアント
    participant Ctrl as MessageController
    participant Auth as 認証
    participant Service as MessageManagementService
    participant TopicRepo as TopicRepository
    participant MessageRepo as MessageRepository
    participant FileRepo as FileRepository
    participant DB as PostgreSQL
    participant SignalR as SignalR Hubs
    participant FileSys as ファイルシステム

    Client->>+Ctrl: POST /{tenant}/api/message
    Note over Client: multipart/form-data<br/>TopicId, Body, Files

    Auth->>Auth: 認証検証
    Auth-->>-Ctrl: CurrentUserId取得

    Ctrl->>Service: CreateMessageAsync(request, CurrentUserId)
    Service->>Service: ExecuteAsyncでラップ

    alt トピック存在チェック
        Service->>TopicRepo: GetByIdAsync(TopicId)
        TopicRepo-->>Service: Topicエンティティ
        Service->>Service: トピックのRoomを取得
    else
        Service-->>Ctrl: Result<MessageDto>.NotFound
        Ctrl-->>-Client: 404 Not Found
    end

    Service->>Service: ルームユーザーチェック
    Service->>Service: RoomUserが未登録なら追加

    Service->>Service: Messageエンティティ作成
    Service->>Service: ReplyIdがあれば返信関係設定

    alt ファイルアップロードあり
        Service->>FileRepo: ProcessUploadedFilesAsync(Files, UserId, MessageId)
        FileRepo->>FileSys: ファイル保存
        FileSys-->>FileRepo: ファイルパス情報
        FileRepo-->>Service: Fileエンティティリスト
        Service->>Service: Filesプロパティに設定
    end

    Service->>MessageRepo: AddAsync(message)
    MessageRepo->>DB: INSERT INTO Messages
    DB-->>MessageRepo: 保存完了

    Service->>Service: 子トピック作成指示チェック
    alt ChildTopic指定あり
        Service->>Service: CreateChildTopicInternalAsync
        Service->>Service: メッセージを新トピックに移動
        Service->>SignalR: BroadcastTopicCreatedAsync
    end

    Service->>SignalR: BroadcastMessageCreatedAsync(messageDto)
    Service-->>-Ctrl: Result<MessageDto>.Success
    Ctrl-->>-Client: 201 Created

    alt 編集リクエスト
        Client->>+Ctrl: PUT /{tenant}/api/message/{id}
        Ctrl->>Service: UpdateMessageAsync(id, request, CurrentUserId)
        Service->>MessageRepo: GetByIdAsync(id)
        Service->>Service: 作成者チェック
        Service->>Service: 内容更新
        Service->>MessageRepo: SaveChangesAsync
        Service->>SignalR: BroadcastMessageUpdatedAsync
        Service-->>-Ctrl: 更新完了
        Ctrl-->>-Client: 200 OK
    end
```

### 重要な処理ステップ

#### 1. メッセージ作成
```csharp
// MessageManagementService.cs
public async Task<Result<MessageDto>> CreateMessageAsync(
    CreateMessageRequest request,
    Guid userId)
{
    return await ExecuteAsync(async () =>
    {
        // 1. トピック存在チェック
        var topic = await _topicRepository.GetByIdAsync(request.TopicId);
        if (topic == null)
            return Result<MessageDto>.NotFound("Topic not found");

        // 2. ルームユーザーチェックと登録
        await ResolveRoomUserAsync(topic.RoomId, userId);

        // 3. メッセージエンティティ作成
        var message = new Message
        {
            TopicId = topic.Id,
            Header = request.Header,
            Body = request.Body,
            ReplyId = request.ReplyId,
            CreatedBy = userId,
            Created = DateTime.UtcNow
        };

        // 4. ファイル処理
        if (request.Files != null && request.Files.Any())
        {
            message.Files = await ProcessUploadedFilesAsync(
                request.Files, userId, message.Id);
        }

        await _messageRepository.AddAsync(message);
        return MapToDto(message);
    });
}
```

#### 2. ファイルアップロード処理
```csharp
private async List<File> ProcessUploadedFilesAsync(
    List<IFormFile> files,
    Guid userId,
    Guid messageId)
{
    var uploadedFiles = new List<File>();

    foreach (var file in files)
    {
        var fileExtension = Path.GetExtension(file.FileName);
        var storedName = $"{Guid.NewGuid()}{fileExtension}";
        var filePath = Path.Combine(
            "uploads",
            _tenantId.ToString(),
            userId.ToString(),
            messageId.ToString(),
            storedName);

        // ファイル保存
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        uploadedFiles.Add(new File
        {
            OriginalName = file.FileName,
            StoredName = storedName,
            FileSize = file.Length,
            MimeType = file.ContentType,
            FileUrl = $"/api/file/{storedName}"
        });
    }

    return uploadedFiles;
}
```

#### 3. リアルタイム配信
```csharp
// MessageHub.cs
public async Task BroadcastMessageCreatedAsync(MessageDto message)
{
    await Clients.Group($"topic-{message.TopicId}")
        .SendAsync("MessageCreated", message);
}

// RoomTopicHub.cs
public async Task BroadcastTopicCreatedAsync(TopicDto topic)
{
    await Clients.Group($"room-{topic.RoomId}")
        .SendAsync("TopicCreated", topic);
}
```

### 複雑なビジネスロジック

#### 子トピック作成とメッセージ移動
```csharp
private async Task CreateChildTopicInternalAsync(
    Message message,
    CreateChildTopicRequest request)
{
    // 新しいトピックを作成
    var childTopic = new Topic
    {
        RoomId = message.Topic.RoomId,
        Header = request.Header,
        Body = request.Body,
        ReplyId = message.TopicId,
        MessageId = message.Id,
        CreatedBy = message.CreatedBy,
        Created = DateTime.UtcNow
    };

    // メッセージを新しいトピックに移動
    message.TopicId = childTopic.Id;

    await _topicRepository.AddAsync(childTopic);
    await _messageRepository.SaveChangesAsync();
}
```

## ユースケース3: 認証・セッション管理

### 概要
ユーザーがOIDCプロバイダー経由でログインし、セッションを確立するまでのフロー。

### 処理フロー詳細

```mermaid
sequenceDiagram
    participant Client as クライアント
    participant Web as Webサーバー
    participant AuthCtrl as AuthController
    participant OIDC as OIDC Provider
    participant Cookie as Cookie認証
    participant UserManager as UserManager
    participant DB as PostgreSQL

    Client->>+Web: GET /{tenant}/auth/login?returnUrl=/rooms
    Web->>AuthCtrl: Login(returnUrl)

    AuthCtrl->>AuthCtrl: ValidateReturnUrl(returnUrl)
    AuthCtrl->>OIDC: Challenge("oidc")
    OIDC-->>-Client: 302 Redirect to Provider

    Client->>+OIDC: GET /authorize?...
    Note over Client,OIDC: ブラウザで認証画面表示

    User->>OIDC: ユーザー認証（Googleログイン）
    OIDC->>Client: IDトークン付きリダイレクト
    Client->>Web: GET /signin-oidc

    Web->>Cookie: ValidatePrincipalAsync
    Cookie->>Cookie: DecryptIdToken
    Cookie->>UserManager: FindByIdAsync(sub)
    UserManager-->>Cookie: Userエンティティ

    alt ユーザー存在しない
        Cookie->>UserManager: CreateAsync(newUser)
        UserManager-->>Cookie: User作成完了
    end

    Cookie->>Cookie: SignInAsync(User)
    Cookie-->>-Web: 認証完了

    Web->>AuthCtrl: ExternalLoginCallback
    AuthCtrl->>AuthCtrl: ProcessLoginResult
    AuthCtrl->>Web: Redirect(returnUrl)

    Web->>+Client: 302 Redirect to /rooms
    Note over Client: UIでユーザー情報更新

    Client->>+Web: GET /{tenant}/auth/me
    Web->>AuthCtrl: Me()
    AuthCtrl->>UserManager: FindByIdAsync(UserId)
    UserManager-->>AuthCtrl: ApplicationUser
    AuthCtrl->>AuthCtrl: MapToUserDto
    AuthCtrl-->>-Client: 200 OK (ユーザー情報)
    Note over Client: UIでプロフィール表示

    alt ログアウト
        Client->>+Web: GET /{tenant}/auth/logout
        Web->>Cookie: SignOutAsync(Cookies)
        Web->>AuthCtrl: Logout()
        AuthCtrl->>Web: Redirect to logout page
        Web-->>-Client: ログアウト完了
        Note over Client: セッションクリア
    end
```

### 重要な処理ステップ

#### 1. ログイン処理
```csharp
// AuthController.cs
[HttpGet]
public IActionResult Login(string? returnUrl)
{
    // returnUrlのバリデーション
    if (!string.IsNullOrEmpty(returnUrl) &&
        !IsValidReturnUrl(returnUrl, _tenantResolver.CurrentTenant))
    {
        return BadRequest("Invalid return URL");
    }

    // OIDCプロバイダーへリダイレクト
    var properties = new AuthenticationProperties
    {
        RedirectUri = Url.Action(nameof(ExternalLoginCallback)),
        Items = { { "returnUrl", returnUrl } }
    };

    return Challenge(properties, "oidc");
}
```

#### 2. コールバック処理
```csharp
[HttpGet]
public async Task<IActionResult> ExternalLoginCallback()
{
    var authenticateResult = await HttpContext.AuthenticateAsync("oidc");

    if (!authenticateResult.Succeeded)
        return RedirectToAction("Login");

    var externalId = authenticateResult.Principal
        .FindFirstValue(ClaimTypes.NameIdentifier);

    // 既存ユーザー検索
    var user = await _userManager.FindByIdAsync(externalId);

    if (user == null)
    {
        // 新規ユーザー作成
        user = new ApplicationUser
        {
            Id = Guid.Parse(externalId),
            Email = authenticateResult.Principal
                .FindFirstValue(ClaimTypes.Email),
            DisplayName = authenticateResult.Principal
                .FindFirstValue("name"),
            IconUrl = authenticateResult.Principal
                .FindFirstValue("picture")
        };

        await _userManager.CreateAsync(user);
    }

    // セッション確立
    await HttpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(
            new ClaimsIdentity(
                authenticateResult.Principal.Claims,
                CookieAuthenticationDefaults.AuthenticationScheme)),
        new AuthenticationProperties { IsPersistent = true });

    // リダイレクト
    var returnUrl = authenticateResult.Properties?.Items["returnUrl"];
    return Redirect(returnUrl ?? "/");
}
```

#### 3. セッション管理
```csharp
// CookieAuthenticationConfiguration.cs
services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".AspNetCore.Identity.Application";
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
    });
```

### セキュリティ対策

#### returnUrl検証
```csharp
private bool IsValidReturnUrl(string returnUrl, string? currentTenant)
{
    // 絶対URLは拒否
    if (Url.IsLocalUrl(returnUrl))
    {
        // 同一テナント内のURLのみ許可
        if (!string.IsNullOrEmpty(currentTenant))
        {
            var normalizedTenantPath = $"/{currentTenant}";
            return returnUrl.StartsWith(
                normalizedTenantPath,
                StringComparison.OrdinalIgnoreCase);
        }
        return true;
    }
    return false;
}
```

#### テナント境界の確認
```csharp
// リクエスト処理時のテナント検証
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public async Task Invoke(HttpContext context)
    {
        var tenant = _tenantResolver.ResolveTenant(context);

        if (tenant == null)
        {
            context.Response.StatusCode = 404;
            return;
        }

        context.Items["CurrentTenant"] = tenant;
        await _next(context);
    }
}
```

---

**コードへの導線**
- **ルーム作成**: `TreeTopic/Controllers/RoomController.cs`
- **メッセージ処理**: `TreeTopic/Controllers/MessageController.cs`
- **認証処理**: `TreeTopic/Controllers/AuthController.cs`
- **ビジネスロジック**: `TreeTopic/Services/`
- **リポジトリ**: `TreeTopic/Repositories/`
- **SignalR**: `TreeTopic/Hubs/`

**参照 (根拠)**
- `TreeTopic/Controllers/RoomController.cs` - ルームAPI実装
- `TreeTopic/Controllers/MessageController.cs` - メッセージAPI実装
- `TreeTopic/Controllers/AuthController.cs` - 認証API実装
- `TreeTopic/Services/RoomManagementService.cs` - ルームサービス
- `TreeTopic/Services/MessageManagementService.cs` - メッセージサービス
- `TreeTopic/Hubs/MessageHub.cs` - メッセージハブ
- `TreeTopic/Hubs/RoomTopicHub.cs` - トピックハブ
- `TreeTopic/Repositories/` - リポジトリ実装