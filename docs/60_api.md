# API仕様

## TL;DR

TreeTopicのREST APIはマルチテナントをサポートし、OAuth2.0 + OpenID Connectで認証されます。全てのリクエストにはテナントIDが必要で、レスポンスは標準化されたJSON形式です。Swagger UIで完全なドキュメントを確認できます。

## API一覧

### 認証関連

#### ログイン
```
GET /{tenant}/auth/login
```

**説明**: OIDCプロバイダーへリダイレクト
**認証**: 不要
**レスポンス**: 302 Redirect to Provider

**例**:
```http
GET /company-a/auth/login?returnUrl=/rooms
```

#### コールバック
```
GET /{tenant}/auth/signin-oidc
```

**説明**: OIDC認証後のコールバック処理
**認証**: OIDCトークン
**レスポンス**: ローカルリダイレクト

#### 現在ユーザー情報
```
GET /{tenant}/auth/me
```

**説明**: 認証済みユーザーの情報を取得
**認証**: Cookie
**レスポンス**: 200 OK

**レスポンス例**:
```json
{
  "id": "masked-uuid",
  "email": "user@example.com",
  "displayName": "山田太郎",
  "iconUrl": "https://example.com/avatar.jpg",
  "roles": ["User"]
}
```

#### ログアウト
```
GET /{tenant}/auth/logout
```

**説明**: ローカルセッションを破棄
**認証**: Cookie
**レスポンス**: ログアウトページへのリダイレクト

### ルーム関連

#### ルーム一覧取得
```
GET /{tenant}/api/rooms
```

**説明**: テナントの全ルームを取得
**認証**: 必要
**クエリパラメータ**:
- `page`: ページ番号 (default: 1)
- `pageSize`: 1ページあたりの件数 (default: 20)
- `search`: 検索キーワード

**レスポンス例**:
```json
{
  "data": [
    {
      "id": "masked-uuid",
      "name": "プロジェクトA",
      "description": "プロジェクトAに関する議論",
      "iconUrl": "https://example.com/icon.png",
      "isPublic": true,
      "created": "2024-01-01T00:00:00Z",
      "createdBy": "masked-user-uuid",
      "lastMessage": {
        "id": "masked-msg-uuid",
        "body": "こんにちは！",
        "created": "2024-01-01T12:00:00Z"
      }
    }
  ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "total": 100
  }
}
```

#### ルーム作成
```
POST /{tenant}/api/rooms
```

**説明**: 新しいルームを作成
**認証**: 必要
**リクエストボdy**:
```json
{
  "name": "新規ルーム",
  "description": "説明文",
  "isPublic": true
}
```

**レスポンス**: 201 Created

#### ルーム詳細取得
```
GET /{tenant}/api/rooms/{id}
```

**説明**: 指定されたルームの詳細を取得
**認証**: 必要
**レスポンス**: 200 OK

**レスポンス例**:
```json
{
  "id": "masked-uuid",
  "name": "プロジェクトA",
  "description": "プロジェクトAに関する議論",
  "iconUrl": "https://example.com/icon.png",
  "isPublic": true,
  "created": "2024-01-01T00:00:00Z",
  "createdBy": "masked-user-uuid",
  "permissions": {
    "canEdit": true,
    "canDelete": false,
    "role": "Owner"
  },
  "topicCount": 25,
  "memberCount": 5
}
```

#### ルーム更新
```
PUT /{tenant}/api/rooms/{id}
```

**説明**: ルーム情報を更新
**認証**: 必要
**リクエストボdy**:
```json
{
  "name": "更新後のルーム名",
  "description": "更新後の説明文",
  "isPublic": false
}
```

**レスポンス**: 200 OK

#### ルーム削除
```
DELETE /{tenant}/api/rooms/{id}
```

**説明**: ルームを削除
**認証**: 必要（オーナーのみ）
**レスポンス**: 204 No Content

#### ルーム参加
```
POST /{tenant}/api/rooms/{id}/join
```

**説明**: ルームに参加
**認証**: 必要
**リクエストボdy**:
```json
{
  "role": "Member"
}
```

**レスポンス**: 200 OK

#### ルーム退出
```
POST /{tenant}/api/rooms/{id}/leave
```

**説明**: ルームから退出
**認証**: 必要
**レスポンス**: 204 No Content

### トピック関連

#### トピック一覧取得
```
GET /{tenant}/api/rooms/{roomId}/topics
```

**説明**: ルームの全トピックを取得
**認証**: 必要
**クエリパラメータ**:
- `parentId`: 親トピックID（指定すると子トピックを取得）
- `page`: ページ番号
- `pageSize`: 1ページあたりの件数

#### トピック作成
```
POST /{tenant}/api/topics
```

**説明**: 新しいトピックを作成
**認証**: 必要
**リクエストボdy**:
```json
{
  "roomId": "room-uuid",
  "header": "議題",
  "body": "詳細内容",
  "replyId": "topic-uuid", // 返信先トピック
  "childTopic": { // 子トピックを作成する場合
    "header": "新しい議題",
    "body": "内容"
  }
}
```

**レスポンス**: 201 Created

#### トピック詳細取得
```
GET /{tenant}/api/topics/{id}
```

**説明**: 指定されたトピックの詳細を取得
**認証**: 必要
**レスポンス**: 200 OK

#### トピック更新
```
PUT /{tenant}/api/topics/{id}
```

**説明**: トピック情報を更新
**認証**: 必要（作成者または管理者）
**リクエストボdy**:
```json
{
  "header": "更新後のタイトル",
  "body": "更新後の内容"
}
```

**レスポンス**: 200 OK

### メッセージ関連

#### メッセージ一覧取得
```
GET /{tenant}/api/topics/{topicId}/messages
```

**説明**: トピックの全メッセージを取得
**認証**: 必要
**クエリパラメータ**:
- `page`: ページ番号
- `pageSize`: 1ページあたりの件数
- `afterId`: 指定ID以降のメッセージを取得

#### メッセージ作成
```
POST /{tenant}/api/messages
```

**説明**: 新しいメッセージを作成
**認証**: 必要
**Content-Type**: multipart/form-data
**リクエストパラメータ**:
- `topicId`: トピックID
- `header`: メッセージタイトル
- `body`: メッセージ本文
- `replyId`: 返信先メッセージID
- `files`: 添付ファイル（複数可）

**レスポンス**: 201 Created

#### メッセージ更新
```
PUT /{tenant}/api/messages/{id}
```

**説明**: メッセージを更新
**認証**: 必要（作成者のみ）
**リクエストボdy**:
```json
{
  "header": "更新後のタイトル",
  "body": "更新後の内容"
}
```

**レスポンス**: 200 OK

#### メッセージ削除
```
DELETE /{tenant}/api/messages/{id}
```

**説明**: メッセージを削除
**認証**: 必要（作成者または管理者）
**レスポンス**: 204 No Content

### ファイル関連

#### ファイルダウンロード
```
GET /{tenant}/api/file/{filename}
```

**説明**: 添付ファイルをダウンロード
**認証**: 必要
**レスポンス**: 200 OK with file

#### ファイル一覧取得
```
GET /{tenant}/api/messages/{messageId}/files
```

**説明**: メッセージの添付ファイル一覧を取得
**認証**: 必要
**レスポンス**: 200 OK

### 権限関連

#### ユーザー権限取得
```
GET /{tenant}/api/rooms/{roomId}/permissions
```

**説明**: ルームの全ユーザー権限を取得
**認証**: 必要（管理者以上）
**レスポンス**: 200 OK

#### ユーザー権限設定
```
POST /{tenant}/api/rooms/{roomId}/permissions
```

**説明**: ユーザー権限を設定
**認証**: 必要（オーナーまたは管理者）
**リクエストボdy**:
```json
{
  "userId": "user-uuid",
  "role": "Member"
}
```

**レスポンス**: 200 OK

### 共有関連

#### 共有URL生成
```
POST /{tenant}/api/share
```

**説明**: トピックまたはメッセージの共有URLを生成
**認証**: 必要
**リクエストボdy**:
```json
{
  "itemType": "Topic", // "Topic" または "Message"
  "itemId": "item-uuid",
  "hasPassword": true,
  "password": "password123",
  "expiresIn": 7 // 有効期間（日）
}
```

**レスポンス例**:
```json
{
  "shareCode": "abc123def",
  "shareUrl": "https://app.treetopic.com/share/abc123def",
  "expiresAt": "2024-01-08T00:00:00Z"
}
```

#### 共有アイテム取得
```
GET /{tenant}/api/share/{code}
```

**説明**: 共有アイテムの情報を取得
**認証**: 不要
**クエリパラメータ**:
- `password`: パスワード（設定されている場合）

**レスポンス**: 200 OK

## 認証方式

### 認証フロー

```mermaid
sequenceDiagram
    participant Client as クライアント
    participant API as TreeTopic API
    participant Auth as 認証ミドルウェア
    participant OIDC as OIDC Provider

    Client->>+API: リクエスト
    API->>Auth: 認証チェック
    alt 認証済み
        Auth-->>API: 認証成功
        API->>API: ビジネスロジック実行
        API-->>-Client: レスポンス
    else 未認証
        Auth-->>-Client: 401 Unauthorized
        Client->>+OIDC: ログインリクエスト
        OIDC-->>-Client: 認証後リダイレクト
        Client->>+API: 認証付きリクエスト
        API->>Auth: 認証チェック
        Auth-->>API: 認証成功
        API-->>-Client: レスポンス
    end
```

### 認証方法
1. **Cookie認証**: セッションベースの認証
2. **Bearer Token**: APIキー認証（将来対応予定）

### 認証ヘッダー
```http
Authorization: Bearer <access-token>
```

### クッキー設定
```http
Set-Cookie: .AspNetCore.Identity.Application=...;
    Path=/;
    SameSite=None;
    Secure;
    Expires=2024-02-19T00:00:00Z;
```

## エラーレスポンス

### 標準エラーレスポンス
```json
{
  "success": false,
  "error": {
    "code": "NotFound",
    "message": "リソースが見つかりません",
    "details": "指定されたトピックは存在しません"
  },
  "timestamp": "2024-01-01T00:00:00Z"
}
```

### エラーコード一覧

| ステータスコード | エラーコード | 説明 |
|----------------|-------------|------|
| 400 | BadRequest | リクエスト形式不正 |
| 401 | Unauthorized | 認証が必要 |
| 403 | Forbidden | 権限不足 |
| 404 | NotFound | リソース不存在 |
| 409 | Conflict | リソース競合 |
| 422 | Unprocessable | バリデーションエラー |
| 429 | TooManyRequests | リクエスト過多 |
| 500 | ServerError | サーバーエラー |

### バリデーションエラー
```json
{
  "success": false,
  "error": {
    "code": "ValidationError",
    "message": "入力値の検証に失敗しました",
    "errors": [
      {
        "field": "name",
        "message": "ルーム名は必須です"
      },
      {
        "field": "email",
        "message": "有効なメールアドレスを入力してください"
      }
    ]
  }
}
```

## レートリミット

### 制限ポリシー
- **リクエスト数**: 100リクエスト/分/ユーザー
- **ファイルアップロード**: 10回/日/ユーザー
- **同時接続数**: 最大5セッション/ユーザー

### レスポンスヘッダー
```http
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 95
X-RateLimit-Reset: 1640995200
```

## ウェブフック

### ウェブフックイベント

| イベント | 説明 |
|---------|------|
| `room.created` | ルームが作成された |
| `room.updated` | ルームが更新された |
| `room.deleted` | ルームが削除された |
| `topic.created` | トピックが作成された |
| `topic.updated` | トピックが更新された |
| `message.created` | メッセージが作成された |
| `message.updated` | メッセージが更新された |
| `message.deleted` | メッセージが削除された |

### ウェブフック登録
```
POST /{tenant}/api/webhooks
```

**リクエストボdy**:
```json
{
  "url": "https://your-service.com/webhook",
  "events": ["room.created", "message.created"],
  "secret": "webhook-secret"
}
```

### ウェブフックペイロード例
```json
{
  "event": "message.created",
  "tenantId": "tenant-uuid",
  "data": {
    "id": "msg-uuid",
    "roomId": "room-uuid",
    "topicId": "topic-uuid",
    "body": "新しいメッセージ",
    "createdBy": "user-uuid",
    "created": "2024-01-01T00:00:00Z"
  },
  "signature": "sha256=..."
}
```

## OpenAPI定義

### スキーマ定義の場所
- **バックエンド**: `TreeTopic/Program.cs` (行696-703)
- **フロントエンド**: `TreeTopic/TreeTopic.Client/src/lib/types/api.types.ts`
- **自動生成**: `npm run generate:api`

### スキーマバージョン
- **OpenAPI**: 3.0.0
- **JSON Schema**: 2020-12

### カスタムタイプ

#### MaskedGuid
```json
{
  "type": "string",
  "format": "uuid",
  "pattern": "[a-zA-Z0-9]{8}-[a-zA-Z0-9]{4}-[a-zA-Z0-9]{4}-[a-zA-Z0-9]{4}-[a-zA-Z0-9]{12}"
}
```

#### Result<T>
```json
{
  "type": "object",
  "properties": {
    "success": { "type": "boolean" },
    "data": { "$ref": "#/components/schemas/T" },
    "error": { "$ref": "#/components/schemas/Error" }
  }
}
```

---

**コードへの導線**
- **APIコントローラ**: `TreeTopic/Controllers/`
- **Swagger設定**: `TreeTopic/Program.cs` (行696-703)
- **OpenAPI生成**: `TreeTopic/TreeTopic.Client/src/lib/types/`
- **認証ミドルウェア**: `TreeTopic/Authentication/`
- **エラーハンドリング**: `TreeTopic/Common/Result.cs`

**参照 (根拠)**
- `TreeTopic/Controllers/AuthController.cs` - 認証API実装
- `TreeTopic/Controllers/RoomController.cs` - ルームAPI実装
- `TreeTopic/Controllers/MessageController.cs` - メッセージAPI実装
- `TreeTopic/Program.cs` - API設定とSwagger有効化
- `TreeTopic/TreeTopic.Client/src/lib/types/api.types.ts` - TypeScript型定義
- `TreeTopic/Common/Result.cs` - 統一レスポンス型