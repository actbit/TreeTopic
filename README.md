# TreeTopic

マルチテナント対応のチームコラボレーションプラットフォーム

## 概要

TreeTopicは、チーム向けのリアルタイムコラボレーションツールです。チャットルーム、トピックベースのディスカッション、ブレインストーミング機能を提供します。

### 主な機能

- **チャットルーム** - ルーム作成・参加、リアルタイムメッセージング
- **トピック管理** - トピックベースのディスカッション整理
- **ブレインストーミング** - ブレードボード、アイデア投票機能
- **ファイル共有** - アップロード、プレビュー、PDFビューアー
- **プッシュ通知** - Web Pushによる通知
- **パーミッション管理** - ロールベースアクセス制御（RBAC）

## 技術スタック

### バックエンド

| カテゴリ | 技術 |
|---------|------|
| Framework | ASP.NET Core 10.0 |
| Database | PostgreSQL (Entity Framework Core) |
| Authentication | OpenID Connect + Keycloak |
| Multi-tenant | Finbuckle.MultiTenant |
| Real-time | SignalR (@microsoft/signalr) |
| Encryption | AES-256 + BouncyCastle.Cryptography |
| Image Processing | SixLabors.ImageSharp |
| Web Push | Lib.Net.Http.WebPush |
| API Documentation | NSwag.AspNetCore |

### フロントエンド

| カテゴリ | 技術 |
|---------|------|
| Framework | SvelteKit 2.49 + Svelte 5.45 |
| Build | Vite 7.2 |
| Canvas | Fabric.js 7.1 |
| PDF | PDF.js 5.4 + PDF-Lib |
| CAPTCHA | Altcha |
| PWA | vite-plugin-pwa |

### インフラ

- **Container**: Docker
- **Orchestration**: .NET Aspire 13.0
- **Monitoring**: Aspire Dashboard + OpenTelemetry

## プロジェクト構成

```
TreeTopic/
├── TreeTopic/                    # メインアプリケーション
│   ├── Controllers/              # APIコントローラー
│   ├── Models/                   # データモデル
│   ├── Services/                 # ビジネスロジック
│   ├── Repositories/             # データアクセス層
│   ├── Hubs/                     # SignalRハブ
│   ├── Authentication/           # 認証関連
│   ├── Middleware/               # カスタムミドルウェア
│   ├── Permissions/              # パーミッション管理
│   ├── Filters/                  # ASP.NETフィルター
│   ├── Extensions/               # 拡張メソッド
│   └── TreeTopic.Client/         # フロントエンド (SvelteKit)
├── TreeTopic.AppHost/            # .NET Aspire ホスト
└── TreeTopic.ServiceDefaults/    # 共通設定
```

## 前提条件

- .NET 10.0 SDK
- Node.js 20+
- Docker Desktop
- PostgreSQL（Aspireで自動起動）
- Keycloak（Aspireで自動起動・開発環境のみ）

## セットアップ

### 1. リポジトリのクローン

```bash
git clone https://github.com/actbit/TreeTopic.git
cd TreeTopic
```

### 2. 依存パッケージのインストール

```bash
dotnet restore
```

### 3. 設定ファイルの準備

`TreeTopic/appsettings.Development.json` を作成:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "TreeTopic": "Debug"
    }
  },
  "Vapid": {
    "PublicKey": "your-vapid-public-key",
    "PrivateKey": "your-vapid-private-key"
  }
}
```

### 4. 開発環境の起動

```bash
# Aspire経由で全サービス起動（PostgreSQL, Keycloak, pgAdmin含む）
dotnet run --project TreeTopic.AppHost -- --password keycloak-admin-password your-password
```

起動後、以下のサービスが利用可能になります：
- **TreeTopic API**: Aspire Dashboardで確認
- **Keycloak**: http://localhost:8080
- **pgAdmin**: Aspire Dashboardで確認
- **Aspire Dashboard**: 自動的にブラウザで開きます

## 設定項目

### 必須設定

| 設定キー | 説明 | 例 |
|---------|------|-----|
| `ConnectionStrings:TenantDb` | テナント管理DB接続文字列 | `Host=localhost;Port=5432;Database=treetopic_tenants;User Id=postgres;Password=password;` |
| `ConnectionStrings:SharedApp` | 共通アプリDB接続文字列 | `Host=localhost;Port=5432;Database=treetopic_shared;User Id=postgres;Password=password;` |
| `Encryption:Key` | 暗号化キー（Base64） | `uxfNDZcInUBovLRutTt0qxhnkdEdNyU7ttXTnu6ImgU=` |

### 認証設定

| 設定キー | 説明 |
|---------|------|
| `Authentication:PublicBaseUrl` | 公開ベースURL |
| `Authentication:CookieName` | Cookie名（デフォルト: `TreeTopic.Cookie`） |
| `Authentication:UseTenantAwareCookies` | テナント別Cookie（デフォルト: true） |

### Google OAuth設定（オプション）

```json
{
  "Google": {
    "ClientId": "your-google-client-id",
    "ClientSecret": "your-google-client-secret"
  }
}
```

### Web Push設定

```json
{
  "Vapid": {
    "PublicKey": "your-vapid-public-key",
    "PrivateKey": "your-vapid-private-key"
  }
}
```

### その他の設定

| 設定キー | 説明 | デフォルト値 |
|---------|------|-------------|
| `FileUpload:MaxFileSize` | 最大ファイルサイズ（バイト） | 31457280 (30MB) |
| `RateLimit:MaxRequestsPerHour` | テナント登録のレートリミット | 10回/時間 |
| `Cors:AllowedOrigins` | 許可するCORSオリジン | `[]` |

### リバースプロキシ設定

本番環境でリバースプロキシ（Nginx等）を使用する場合：

```json
{
  "ForwardedHeaders": {
    "Mode": "Auto",
    "KnownNetworks": ["172.16.0.0/12", "10.0.0.0/8"],
    "KnownProxies": []
  }
}
```

## 暗号化キー

### 概要

TreeTopicは機密データ（テナント接続文字列等）の保護に **AES-256-GCM** を使用しています。

| 項目 | 仕様 |
|-----|------|
| アルゴリズム | AES-256-GCM |
| キー長 | 32バイト（256ビット） |
| フォーマット | Base64エンコード |
| 認証タグ | 128ビット |
| Nonce | 96ビット（ランダム生成） |

### キー生成

暗号化キーは以下の方法で生成できます：

```bash
# .NETで生成
dotnet run --project TreeTopic -- --generate-key

# または OpenSSL で生成
openssl rand -base64 32
```

### 設定方法

環境変数（推奨）:
```bash
export ENCRYPTION_KEY="生成したBase64キー"
```

または `appsettings.json`:
```json
{
  "Encryption": {
    "Key": "生成したBase64キー"
  }
}
```

### テナントキー暗号化

各テナントの接続文字列は、マスターキーで暗号化されたテナント固有キーで二重に暗号化されます：

1. **マスターキー**: `Encryption:Key` で設定
2. **テナントキー**: テナント作成時に自動生成され、マスターキーで暗号化されて保存
3. **接続文字列**: テナントキーで暗号化されて保存

## OpenID Connect (OIDC) 設定

### 概要

TreeTopicはテナントごとに異なるOIDCプロバイダーを設定できます。

- **開発環境**: Keycloak（Aspireで自動起動）
- **本番環境**: 任意のOIDCプロバイダー（Keycloak、Google、Auth0等）

### テナント別OIDC設定

テナント登録時にOIDC設定を指定します：

```json
{
  "OpenIdConnect": {
    "Issuer": "https://your-keycloak.com/realms/your-realm",
    "AuthorizationEndpoint": "https://your-keycloak.com/realms/your-realm/protocol/openid-connect/auth",
    "TokenEndpoint": "https://your-keycloak.com/realms/your-realm/protocol/openid-connect/token",
    "JwksUri": "https://your-keycloak.com/realms/your-realm/protocol/openid-connect/certs",
    "EndSessionEndpoint": "https://your-keycloak.com/realms/your-realm/protocol/openid-connect/logout"
  }
}
```

### OIDCメタデータ項目

| 項目 | 説明 | 必須 |
|-----|------|-----|
| `Issuer` | プロバイダーの発行者URL | ○ |
| `AuthorizationEndpoint` | 認証エンドポイント | ○ |
| `TokenEndpoint` | トークンエンドポイント | ○ |
| `JwksUri` | 公開鍵（JWKS）エンドポイント | ○ |
| `EndSessionEndpoint` | ログアウトエンドポイント | △ |

### デフォルトスコープ

以下のスコープが自動的に要求されます：
- `openid`
- `profile`
- `email`

### Google プロバイダー設定例

`appsettings.json` にデフォルトプロバイダーを設定：

```json
{
  "Google": {
    "ClientId": "your-google-client-id.apps.googleusercontent.com",
    "ClientSecret": "your-google-client-secret"
  },
  "OpenIdConnect": {
    "Providers": {
      "Google": {
        "Authority": "https://accounts.google.com",
        "AuthorizationEndpoint": "https://accounts.google.com/o/oauth2/v2/auth",
        "TokenEndpoint": "https://oauth2.googleapis.com/token",
        "JwksUri": "https://www.googleapis.com/oauth2/v3/certs"
      }
    }
  }
}
```

### Keycloak設定例

Keycloakのレルムを作成し、以下の情報をテナント登録時に指定：

```
Issuer:                https://keycloak.example.com/realms/my-realm
AuthorizationEndpoint: https://keycloak.example.com/realms/my-realm/protocol/openid-connect/auth
TokenEndpoint:         https://keycloak.example.com/realms/my-realm/protocol/openid-connect/token
JwksUri:               https://keycloak.example.com/realms/my-realm/protocol/openid-connect/certs
EndSessionEndpoint:    https://keycloak.example.com/realms/my-realm/protocol/openid-connect/logout
```

### 認証フロー

1. ユーザーが `/{tenant}/api/auth/login` にアクセス
2. テナントのOIDC設定に基づきプロバイダーへリダイレクト
3. 認証成功後、`/auth/signin-oidc` にコールバック
4. セッションCookieを発行してリダイレクト

## 主要エンドポイント

| エンドポイント | メソッド | 説明 |
|--------------|---------|------|
| `/api/tenants/register` | POST | テナント登録 |
| `/{tenant}/api/setup` | POST | テナント初期化 |
| `/api/auth/login` | POST | ログイン |
| `/api/auth/logout` | POST | ログアウト |
| `/{tenant}/api/rooms` | GET/POST | ルーム管理 |
| `/{tenant}/api/topics` | GET/POST | トピック管理 |
| `/{tenant}/api/messages` | POST | メッセージ送信 |
| `/{tenant}/api/files` | POST | ファイルアップロード |
| `/{tenant}/api/brainstorm` | POST | ブレインストーミング |

## マルチテナント構成

### 概要

TreeTopicは **Finbuckle.MultiTenant** をベースにした完全なマルチテナントSaaSアーキテクチャを採用しています。

| 項目 | 説明 |
|-----|------|
| テナント識別 | URLパス `/{tenant}` で識別 |
| データベース分離 | テナントごとに独立したDB |
| キャッシュ | テナント情報を5分間キャッシュ |
| 認証 | テナントごとに異なるOIDCプロバイダー |

### データベース構成

```
┌─────────────────────────────────────────────────────┐
│                  TenantCatalogDB                     │
│              (treetopic_tenants)                    │
│  ┌─────────────────┐  ┌─────────────────────────┐  │
│  │ ApplicationTenant│  │ ApplicationTenantDetail │  │
│  │     Info         │──│  - ConnectionString     │  │
│  │  - Id           │  │  - EncryptionKey        │  │
│  │  - Identifier   │  │  - OIDC Settings        │  │
│  │  - Name         │  │  - DbProvider           │  │
│  └─────────────────┘  └─────────────────────────┘  │
└─────────────────────────────────────────────────────┘
                         │
         ┌───────────────┼───────────────┐
         ▼               ▼               ▼
   ┌──────────┐    ┌──────────┐    ┌──────────┐
   │ Tenant A │    │ Tenant B │    │ Tenant C │
   │   DB     │    │   DB     │    │   DB     │
   └──────────┘    └──────────┘    └──────────┘
```

- **テナントカタログDB**: `treetopic_tenants` - 全テナントのメタデータ管理
- **テナント別DB**: テナント登録時に自動作成・マイグレーション
- **共通アプリDB**: `treetopic_shared` - 共通データ（将来拡張用）

### テナント識別子

テナント識別子（Identifier）の仕様：

| 項目 | 仕様 |
|-----|------|
| 文字数 | 1〜50文字 |
| 使用可能文字 | 英数字、ハイフン（`-`）、アンダースコア（`_`） |
| 例 | `company-a`, `my_team`, `project123` |

### テナント作成フロー

```
1. POST /api/tenants/register
   ├── テナント情報をカタログDBに登録
   ├── テナント固有の暗号化キーを生成
   ├── 接続文字列を暗号化して保存
   └── セットアップトークンを発行

2. テナントDB自動作成
   ├── 指定されたDBプロバイダー（PostgreSQL/MySQL）でDB作成
   └── マイグレーション実行

3. POST /{tenant}/api/setup
   ├── セットアップトークン検証
   ├── 管理者ユーザー作成（OIDC未設定時）
   └── 初期ロール・パーミッション設定
```

### テナント設定項目

`ApplicationTenantDetail` で管理される設定：

| 設定カテゴリ | 項目 | 説明 |
|------------|------|------|
| **データベース** | `DbProvider` | `PostgreSQL` または `MySQL` |
| | `ConnectionString` | 暗号化された接続文字列 |
| | `TenantEncryptionKey` | テナント固有の暗号化キー |
| **OIDC** | `OpenIdConnectAuthority` | OIDCプロバイダーのAuthority |
| | `OpenIdConnectClientId` | クライアントID |
| | `OpenIdConnectClientSecret` | クライアントシークレット |
| | `OpenIdConnect*Endpoint` | 各種エンドポイント |
| **ロール同期** | `RoleClaimName` | OIDCトークンからロールを取得するクレーム名 |
| **UUID難読化** | `TenantObfuscationKeyK0/K1` | MaskedUUID用の難読化キー |

### OIDCロール同期

テナントで `RoleClaimName` を設定すると、OIDCログイン時に自動的にロールが同期されます：

```
OIDC Token                    TreeTopic
┌─────────────┐              ┌─────────────┐
│ "roles": [  │──同期──▶     │ Application │
│   "admin",  │              │    Role     │
│   "user"    │              │             │
│ ]           │              │             │
└─────────────┘              └─────────────┘
```

**注意**: OIDCロール同期が有効な場合、手動でのロール割り当ては無効になります。

### テナント別Cookie

デフォルトでテナントごとに独立したセッションCookieが発行されます：

```
https://example.com/tenant-a/ → Cookie: TreeTopic.Cookie (path=/tenant-a)
https://example.com/tenant-b/ → Cookie: TreeTopic.Cookie (path=/tenant-b)
```

設定で無効化も可能：
```json
{
  "Authentication": {
    "UseTenantAwareCookies": false
  }
}
```

## 認証・認可

- **認証**: Keycloak + OpenID Connect（開発環境）/ 任意のOIDCプロバイダー
- **認可**: ロールベースアクセス制御（RBAC）
- **パーミッション**: ルーム単位・トピック単位のきめ細かい制御
- **CAPTCHA**: Altchaによるボット対策

## フロントエンド開発

```bash
cd TreeTopic/TreeTopic/TreeTopic.Client

# 依存パッケージインストール
npm install

# 開発サーバー起動
npm run dev

# 型チェック
npm run check

# 本番ビルド
npm run build
```

## Docker本番デプロイ

```bash
# イメージビルド
docker build -t treetopic -f TreeTopic/Dockerfile .

# コンテナ起動
docker run -p 8080:8080 \
  -e ConnectionStrings__TenantDb="Host=..." \
  -e ConnectionStrings__SharedApp="Host=..." \
  -e Encryption__Key="..." \
  -e Authentication__PublicBaseUrl="https://your-domain.com" \
  treetopic
```

### ポート

- `8080`: HTTP
- `8081`: HTTPS（設定時）

## 環境変数

| 変数名 | 説明 |
|--------|------|
| `ConnectionStrings__TenantDb` | テナントDB接続文字列 |
| `ConnectionStrings__SharedApp` | 共通DB接続文字列 |
| `Encryption__Key` | 暗号化キー（Base64） |
| `Authentication__PublicBaseUrl` | 公開ベースURL |
| `Google__ClientId` | Google OAuth クライアントID |
| `Google__ClientSecret` | Google OAuth クライアントシークレット |

## トラブルシューティング

### よくある問題

**データベース接続エラー**
- PostgreSQLが起動しているか確認
- 接続文字列の認証情報を確認

**Keycloak接続エラー**
- Keycloakコンテナが起動しているか確認
- 管理者パスワードが正しいか確認

**CORS エラー**
- `Cors:AllowedOrigins` にフロントエンドのURLを追加

## ライセンス

[MIT License](LICENSE.txt)

Copyright © actbit
