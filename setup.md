# TreeTopic セットアップガイド

TreeTopic はマルチテナント対応の ASP.NET Core アプリケーションです。
.NET Aspire を使用して全サービスを一元管理します。

## クイックスタート（推奨）

### 前提条件

- **.NET 9.0** 以上
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
| .NET | 9.0 以上 | 9.0 以上 |
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

## FreeBSDへのデプロイ

本番環境としてFreeBSDサーバーにTreeTopicをデプロイする手順です。

### デプロイの全体像

```
Windows開発環境           FreeBSD本番環境
┌─────────────┐          ┌─────────────┐
│  .NET 9.0   │ publish  │   .NET 9.0   │
│             │ ──────→  │             │
│ TreeTopic   │   scp    │ TreeTopic   │
│             │          │  Keycloak   │
│             │          │  PostgreSQL │
│             │          │    nginx    │
└─────────────┘          └─────────────┘
```

### 1. FreeBSD環境構築

#### 1.1 前提条件パッケージのインストール

```bash
# パッケージインストール
pkg install -y dotnet9      # .NET 9 (TreeTopic実行用)
pkg install -y openjdk21    # OpenJDK 21 (Keycloak用)
pkg install -y nginx        # Webサーバー
pkg install -y postgresql17-server  # データベース

# バージョン確認
dotnet --version   # 9.0.xxx
java --version     # openjdk 21.x.x
```

#### 1.2 ディレクトリ構成

```
/root/
└── TreeTopic/
    └── publish/              # TreeTopicアプリケーション
        ├── TreeTopic.dll
        ├── appsettings.Production.json
        └── Fonts/
            └── NotoSansJP-Bold.ttf

/usr/local/share/java/keycloak/  # Keycloakインストール先
├── bin/
│   └── kc.sh               # 起動スクリプト
├── conf/
│   └── keycloak.conf       # Keycloak設定ファイル
└── data/                   # データディレクトリ

/usr/local/etc/
├── nginx/
│   ├── nginx.conf          # nginx設定ファイル
│   └── ssl/
│       ├── server.crt      # SSL証明書
│       └── server.key      # SSL秘密鍵
└── rc.d/
    └── treetopic           # TreeTopicサービススクリプト

/etc/
├── ssh/
│   └── sshd_config         # SSH設定
├── pf.conf                 # ファイアウォール設定
└── rc.conf                 # サービス自動起動設定

/var/db/postgres/data/      # PostgreSQLデータ
/var/log/
├── keycloak/               # Keycloakログ
├── treetopic.log           # TreeTopicログ
└── postgres/logfile        # PostgreSQLログ
```

#### 1.3 SSH設定

```bash
# 設定ファイル編集
vi /etc/ssh/sshd_config
```

**変更内容:**
```
PermitRootLogin yes
PasswordAuthentication yes
PubkeyAuthentication yes
```

```bash
# サービス有効化
sysrc sshd_enable=YES
service sshd restart
```

#### 1.4 ファイアウォール（pf）設定

```bash
# 設定ファイル作成
vi /etc/pf.conf
```

**内容:**
```
ext_if="em0"
tcp_services = "{ 22, 80, 443, 8443 }"
set skip on lo0
block in all
pass out all keep state
pass in on $ext_if inet proto tcp from any to any port $tcp_services
```

```bash
# 有効化
sysrc pf_enable=YES
sysrc pf_rules="/etc/pf.conf"
pfctl -f /etc/pf.conf
pfctl -e
```

#### 1.5 PostgreSQL設定

```bash
# データディレクトリ作成
mkdir -p /var/db/postgres/data
chown postgres:postgres /var/db/postgres/data

# データベース初期化
su -m postgres -c "/usr/local/bin/initdb -D /var/db/postgres/data --locale ja_JP.UTF-8 --encoding UTF8"

# 起動
su -m postgres -c "/usr/local/bin/pg_ctl -D /var/db/postgres/data -l /var/db/postgres/logfile start"

# ユーザー・データベース作成
su -m postgres -c "psql -d postgres -c \"ALTER USER postgres WITH PASSWORD 'postgres';\""
su -m postgres -c "psql -d postgres -c \"CREATE USER treetopic WITH PASSWORD 'treetopic';\""
su -m postgres -c "psql -d postgres -c \"CREATE DATABASE treetopic_tenants;\""
su -m postgres -c "psql -d postgres -c \"CREATE DATABASE treetopic_shared;\""
su -m postgres -c "psql -d postgres -c \"CREATE DATABASE keycloak;\""

# 権限付与
su -m postgres -c "psql -d keycloak -c \"GRANT ALL ON SCHEMA public TO treetopic; GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO treetopic; GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO treetopic;\""

# 自動起動設定
sysrc postgresql_enable=YES
```

#### 1.6 Keycloak設定

```bash
# パッケージインストール
pkg install -y keycloak

# 設定ファイル編集
vi /usr/local/share/java/keycloak/conf/keycloak.conf
```

**内容:**
```
# Database
db=postgres
db-username=treetopic
db-password=treetopic
db-url=jdbc:postgresql://localhost:5432/keycloak

# HTTP
http-enabled=true
http-port=8080
https-port=8443

# Hostname
hostname=localhost
hostname-strict=false

# Proxy (for nginx)
proxy=edge

# Health & Metrics
health-enabled=true
metrics-enabled=true
admin-enabled=true
```

```bash
# ユーザー・ディレクトリ作成
pw user add keycloak -c "Keycloak User" -d /nonexistent -s /usr/sbin/nologin
mkdir -p /var/log/keycloak /var/run/keycloak
chown -R keycloak:keycloak /var/log/keycloak /var/run/keycloak /usr/local/share/java/keycloak

# サービス登録
sysrc keycloak_enable=YES
sysrc keycloak_env="KEYCLOAK_ADMIN=admin KEYCLOAK_ADMIN_PASSWORD=admin"

# ビルド
service keycloak build

# 管理者ユーザー作成（初回のみ）
cd /usr/local/share/java/keycloak
KEYCLOAK_ADMIN=admin KEYCLOAK_ADMIN_PASSWORD=admin bin/kc.sh start --optimized --bootstrap-admin-username=admin --bootstrap-admin-password=admin &
sleep 50
curl -s -X POST "http://localhost:8080/realms/master/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=admin" -d "password=admin" \
  -d "grant_type=password" -d "client_id=admin-cli" | grep access_token
pkill -f keycloak

# サービス起動
service keycloak start
```

#### 1.7 nginx設定

```bash
# パッケージインストール
pkg install -y nginx
sysrc nginx_enable=YES

# SSL証明書ディレクトリ作成
mkdir -p /usr/local/etc/nginx/ssl

# 自己署名証明書作成
cd /usr/local/etc/nginx/ssl
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout server.key \
  -out server.crt \
  -subj "/CN=192.168.1.46"

# nginx設定ファイル編集
vi /usr/local/etc/nginx/nginx.conf
```

**内容:**
```nginx
worker_processes auto;
events { worker_connections 1024; }
http {
    include mime.types;
    default_type application/octet-stream;
    sendfile on;
    keepalive_timeout 65;

    upstream treetopic { server 127.0.0.1:5000; }
    upstream keycloak { server 127.0.0.1:8080; }

    # HTTP → HTTPS リダイレクト
    server {
        listen 80;
        return 301 https://$host$request_uri;
    }

    # TreeTopic (HTTPS 443)
    server {
        listen 443 ssl;
        ssl_certificate /usr/local/etc/nginx/ssl/server.crt;
        ssl_certificate_key /usr/local/etc/nginx/ssl/server.key;

        location / {
            proxy_pass http://treetopic;
            proxy_http_version 1.1;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
            # SignalR/WebSocket対応
            proxy_set_header Upgrade $http_upgrade;
            proxy_set_header Connection "upgrade";
            proxy_read_timeout 86400;
        }
    }

    # Keycloak (HTTPS 8443)
    server {
        listen 8443 ssl;
        ssl_certificate /usr/local/etc/nginx/ssl/server.crt;
        ssl_certificate_key /usr/local/etc/nginx/ssl/server.key;

        location / {
            proxy_pass http://keycloak;
            proxy_http_version 1.1;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
    }
}
```

```bash
# nginx起動
service nginx start
```

### 2. Windows環境でのビルド

#### 2.1 プロジェクトのビルドと公開

```powershell
# プロジェクトディレクトリに移動
cd C:\path\to\TreeTopic

# 依存関係復元
dotnet restore

# 公開（Release構成）
dotnet publish TreeTopic/TreeTopic.csproj -c Release -o publish/
```

#### 2.2 フォントの確認

`TreeTopic/Fonts/NotoSansJP-Bold.ttf` がプロジェクトに含まれていることを確認してください。csprojで自動コピー設定済みの場合は、publishフォルダーに自動的に含まれます。

### 3. FreeBSDへの転送

#### 3.1 アプリケーションの転送

```powershell
# publishフォルダーをFreeBSDへ転送
scp -r publish root@192.168.1.46:/root/TreeTopic/
```

### 4. TreeTopicの設定と起動

#### 4.1 フォント確認（必要に応じて）

```bash
# フォントが含まれているか確認
ls -la /root/TreeTopic/publish/Fonts/
# NotoSansJP-Bold.ttf があればOK

# 含まれていない場合の対処
mkdir -p /root/TreeTopic/publish/Fonts
fetch -o /root/TreeTopic/publish/Fonts/NotoSansJP-Bold.ttf \
  "https://github.com/googlefonts/noto-cjk/raw/main/Sans/OTF/Japanese/NotoSansJP-Bold.otf"
```

#### 4.2 設定ファイル作成

```bash
vi /root/TreeTopic/publish/appsettings.Production.json
```

**内容:**
```json
{
  "ConnectionStrings": {
    "TenantDb": "Host=localhost;Port=5432;Database=treetopic_tenants;Username=postgres;Password=postgres",
    "SharedApp": "Host=localhost;Port=5432;Database=treetopic_shared;Username=postgres;Password=postgres"
  },
  "Authentication": {
    "PublicBaseUrl": "https://192.168.1.46"
  }
}
```

#### 4.3 サービススクリプト作成

```bash
vi /usr/local/etc/rc.d/treetopic
```

**内容:**
```bash
#!/bin/sh
# PROVIDE: treetopic
# REQUIRE: DAEMON postgresql keycloak
# KEYWORD: shutdown

. /etc/rc.subr

name="treetopic"
rcvar="treetopic_enable"
desc="TreeTopic Application"

load_rc_config $name

: ${treetopic_enable:=NO}
: ${treetopic_user:=root}
: ${treetopic_dir:=/root/TreeTopic/publish}
: ${treetopic_env:=}

pidfile="/var/run/treetopic.pid"
command="/usr/sbin/daemon"

start_cmd="treetopic_start"

treetopic_start()
{
    echo "Starting treetopic."
    cd ${treetopic_dir}
    ${command} -u ${treetopic_user} -o /var/log/treetopic.log -t treetopic -P ${pidfile} \
        /usr/bin/env ASPNETCORE_ENVIRONMENT=Production ${treetopic_env} \
        /usr/local/bin/dotnet TreeTopic.dll --urls "http://0.0.0.0:5000"
}

run_rc_command "$1"
```

```bash
# 実行権限付与
chmod +x /usr/local/etc/rc.d/treetopic

# ログディレクトリ作成
mkdir -p /var/log/treetopic

# 自動起動設定
sysrc treetopic_enable=YES
sysrc treetopic_dir=/root/TreeTopic/publish

# 起動
service treetopic start
```

### 5. サービス操作コマンド

#### サービス状態確認

```bash
service sshd status
service postgresql status
service keycloak status
service nginx status
service treetopic status
```

#### サービス起動/停止/再起動

```bash
# TreeTopic
service treetopic start
service treetopic stop
service treetopic restart

# Keycloak
service keycloak start
service keycloak stop
service keycloak restart

# nginx
service nginx start
service nginx stop
service nginx restart
```

#### ログ確認

```bash
# TreeTopicログ
tail -f /var/log/treetopic.log

# Keycloakログ
tail -f /var/log/keycloak/keycloak.log

# PostgreSQLログ
tail -f /var/db/postgres/logfile
```

### 6. アクセス情報

| サービス | URL | 備考 |
|---------|-----|------|
| TreeTopic | https://192.168.1.46/ | メインアプリケーション |
| Keycloak | https://192.168.1.46:8443/ | 認証基盤 |
| Keycloak管理 | https://192.168.1.46:8443/admin/master/console/ | admin / admin |
| SSH | ssh root@192.168.1.46 | ポート22 |

### 7. 開放ポート

| ポート | 用途 |
|-------|------|
| 22 | SSH |
| 80 | HTTP (HTTPSへリダイレクト) |
| 443 | HTTPS (TreeTopic) |
| 8443 | HTTPS (Keycloak) |

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
