# TreeTopic セットアップガイド

TreeTopic はマルチテナント対応の ASP.NET Core アプリケーションです。
.NET Aspire を使用して全サービスを一元管理します。

## クイックスタート（推奨）

### 前提条件

- **.NET 10.0** 以上
- **Docker Desktop**
- **Git**

### セットアップ（3ステップ）

```bash
# 1. リポジトリをクローン
git clone <repository-url>
cd TreeTopic

# 2. 依存パッケージをインストール
dotnet restore

# 3. AppHost経由で全サービスを起動
dotnet run --project TreeTopic.AppHost -- --parameter keycloak-admin-password=admin123
```

**起動確認:**

| サービス | URL | 認証情報 |
|---------|-----|--------|
| **TreeTopic アプリケーション** | `https://localhost:5001` | - |
| **Aspire ダッシュボード** | コンソール出力のURL参照（通常 `http://localhost:19629`） | - |
| **Keycloak Admin Console** | `http://localhost:8080` | admin / admin123 |
| **PgAdmin** | コンソール出力のURL参照 | - |

---

## テナントの作成

アプリケーション起動後、最初のテナントを作成します。

詳細は **[テナント初期化ガイド](./docs/TENANT_SETUP.md)** を参照してください。

### クイック例

```bash
# 1. テナント登録
curl -X POST https://localhost:5001/api/tenant/register \
  -H "Content-Type: application/json" \
  -k \
  -d '{
    "identifier": "my-tenant",
    "name": "My Organization",
    "openIdConnectMetadataAddress": "http://localhost:8080/realms/treetopic/.well-known/openid-configuration",
    "openIdConnectClientId": "treetopic-app",
    "openIdConnectClientSecret": "your-secret",
    "roleClaimName": "roles"
  }'

# レスポンスから setupToken をコピー

# 2. テナント初期化
curl -X POST https://localhost:5001/my-tenant/api/setup \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {setupToken}" \
  -k \
  -d '{
    "adminUsername": "admin",
    "adminEmail": "admin@my-tenant.local",
    "adminPassword": "SecurePassword123!"
  }'
```

---

## 詳細セットアップ

### システム要件

| 環境 | 開発環境 | 本番環境 |
|------|---------|---------|
| .NET | 10.0 以上 | 10.0 以上 |
| PostgreSQL | Docker経由 | 外部インスタンス |
| Keycloak | Docker経由（自動） | 外部インスタンス |
| Docker | 必須 | 不要 |

### 構成パターン

#### パターン1: Aspire使用（推奨・開発環境）

すべて自動管理。接続文字列設定不要。

```bash
dotnet run --project TreeTopic.AppHost
```

**利点:**
- PostgreSQL, Keycloakを自動起動
- ダッシュボードで統一管理
- 開発効率が高い

#### パターン2: 手動管理（本番環境など）

外部のPostgreSQL/Keycloakを使用。

```bash
# TreeTopic単独で実行
cd TreeTopic
dotnet run
```

接続文字列を`appsettings.json`で指定：

```json
{
  "ConnectionStrings": {
    "TenantDb": "Host=your-postgres-host;Port=5432;Database=treetopic_tenants;User Id=postgres;Password=yourpassword;",
    "SharedApp": "Host=your-postgres-host;Port=5432;Database=treetopic_shared;User Id=postgres;Password=yourpassword;"
  }
}
```

---

## Aspire使用時の詳細

### AppHost の構成

`TreeTopic.AppHost/AppHost.cs` で以下を管理：

- PostgreSQL コンテナ（PgAdmin付き）
- Keycloak コンテナ（起動時に新規初期化）
- TreeTopicアプリケーション

**Realm 設定について:**

Keycloak は起動時に空の状態で初期化されます。Realm や ユーザーは Keycloak Admin Console から手動で作成してください。詳細は [テナント初期化ガイド](./docs/TENANT_SETUP.md) を参照してください。

### ダッシュボード

Aspireダッシュボードで確認可能：

- **Resources**: 起動中のサービス状態
- **Logs**: リアルタイムログ
- **Metrics**: CPU、メモリ使用率
- **Traces**: API呼び出しのトレース

### デフォルト認証情報

起動時に指定した Keycloak admin パスワードを使用：

```bash
dotnet run --project TreeTopic.AppHost -- --parameter keycloak-admin-password=your-password
```

Keycloak Admin Console: `http://localhost:8080`
- ユーザー: `admin`
- パスワード: `your-password`

---

## 外部サービスの接続（本番環境など）

### PostgreSQL

既存のPostgreSQLインスタンスを使用：

1. データベース作成
   ```sql
   CREATE DATABASE treetopic_tenants;
   CREATE DATABASE treetopic_shared;
   ```

2. `appsettings.json` で接続文字列指定
   ```json
   {
     "ConnectionStrings": {
       "TenantDb": "Host=your-host;Port=5432;Database=treetopic_tenants;User Id=postgres;Password=yourpassword;",
       "SharedApp": "Host=your-host;Port=5432;Database=treetopic_shared;User Id=postgres;Password=yourpassword;"
     }
   }
   ```

3. マイグレーション実行
   ```bash
   dotnet ef database update --context TenantCatalogDbContext
   ```

### Keycloak

既存のKeycloakインスタンスを使用する場合、`AppHost.cs` の Keycloak 設定をコメント化：

```csharp
// if (builder.Environment.IsDevelopment())
// {
//     var keycloak = builder.AddKeycloak("keycloak", port: 8080)
//         .WithDataVolume()
//         .WithRealmImport("./KeycloakRealms");
//     projectBuilder.WithReference(keycloak).WaitFor(keycloak);
// }
```

その後、`Program.cs` で認証を設定：

```csharp
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        realm: "treetopic",
        options =>
        {
            if (!builder.Environment.IsDevelopment())
            {
                options.Authority = "https://your-keycloak-host/realms/treetopic";
                options.RequireHttpsMetadata = true;
            }
        });
```

---

## トラブルシューティング

### Aspireダッシュボードが起動しない

```bash
# プロセスをクリア
dotnet clean
dotnet build TreeTopic.AppHost

# 再起動
dotnet run --project TreeTopic.AppHost
```

### PostgreSQL接続エラー

```
InvalidOperationException: TenantDb connection string not configured
```

**確認事項:**
- Docker が起動しているか: `docker ps`
- PostgreSQL コンテナが起動しているか
- ファイアウォール設定

### Keycloak接続エラー

```
Unable to connect to Keycloak
```

**確認事項:**
- Keycloak コンテナが起動しているか: `docker ps`
- ポート8080が競合していないか

### マイグレーションエラー

```bash
# データベースをリセット
dotnet ef database drop --context TenantCatalogDbContext --force
dotnet ef database update --context TenantCatalogDbContext
```

---

## プロジェクト構成

```
TreeTopic/
├── TreeTopic/                      # メインアプリケーション
│   ├── Controllers/
│   ├── Models/
│   ├── Services/
│   ├── Program.cs
│   └── appsettings.json
│
├── TreeTopic.AppHost/              # Aspireホスト
│   ├── AppHost.cs                  # サービスオーケストレーション
│   ├── KeycloakRealms/
│   │   └── treetopic-realm.json    # Keycloak設定
│   └── TreeTopic.AppHost.csproj
│
└── TreeTopic.ServiceDefaults/      # 共通設定
```

---

## 開発時のコマンド

```bash
# 全サービス起動（推奨）
dotnet run --project TreeTopic.AppHost

# TreeTopic単独実行
cd TreeTopic && dotnet run

# マイグレーション実行
dotnet ef database update --context TenantCatalogDbContext

# 新しいマイグレーション作成
dotnet ef migrations add MigrationName --context TenantCatalogDbContext

# ビルド
dotnet build

# テスト実行
dotnet test
```

---

---

## ドキュメント

| ドキュメント | 説明 |
|-----------|------|
| **[テナント初期化ガイド](./docs/TENANT_SETUP.md)** | テナント作成・初期化の詳細手順 |
| **[Keycloak Well-Known](./docs/KEYCLOAK_WELLKNOWN.md)** | OpenID Connect メタデータについて |
| **[Aspire 開発ガイド](./docs/ASPIRE_DEVELOPMENT.md)** | Aspire を使用した開発環境の詳細 |

---

## 参考資料

- [.NET Aspire 公式ドキュメント](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Entity Framework Core マイグレーション](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [Keycloak ドキュメント](https://www.keycloak.org/documentation)
- [Finbuckle.MultiTenant](https://docs.finbuckle.com/multitenant/)
- [OpenID Connect Discovery](https://openid.net/specs/openid-connect-discovery-1_0.html)
