# Keycloak Well-Known エンドポイント

## 概要

Well-Known エンドポイントは、OpenID Connect クライアントが Keycloak から**認証・認可に必要なメタデータを自動取得**するための仕組みです。

クライアント側で手動設定の代わりに、Keycloak から提供されるメタデータを参照することで、設定の一元化と保守性の向上を実現します。

---

## Well-Known エンドポイント URL

```
https://your-keycloak-host/realms/{realm-name}/.well-known/openid-configuration
```

**開発環境での例:**
```
http://localhost:8080/realms/treetopic/.well-known/openid-configuration
```

---

## レスポンス例

```json
{
  "issuer": "http://localhost:8080/realms/treetopic",
  "authorization_endpoint": "http://localhost:8080/realms/treetopic/protocol/openid-connect/auth",
  "token_endpoint": "http://localhost:8080/realms/treetopic/protocol/openid-connect/token",
  "introspection_endpoint": "http://localhost:8080/realms/treetopic/protocol/openid-connect/introspect",
  "userinfo_endpoint": "http://localhost:8080/realms/treetopic/protocol/openid-connect/userinfo",
  "end_session_endpoint": "http://localhost:8080/realms/treetopic/protocol/openid-connect/logout",
  "jwks_uri": "http://localhost:8080/realms/treetopic/protocol/openid-connect/certs",
  "check_session_iframe": "http://localhost:8080/realms/treetopic/protocol/openid-connect/login-status-iframe.html",
  "grant_types_supported": [
    "authorization_code",
    "implicit",
    "refresh_token",
    "password",
    "client_credentials"
  ],
  "response_types_supported": [
    "code",
    "none",
    "id_token",
    "token",
    "id_token token",
    "code id_token",
    "code token",
    "code id_token token"
  ],
  "response_modes_supported": [
    "query",
    "fragment",
    "form_post"
  ],
  "subject_types_supported": [
    "public",
    "pairwise"
  ],
  "id_token_signing_alg_values_supported": [
    "PS384",
    "ES384",
    "RS256",
    "ES256",
    "HS512",
    "HS256",
    "HS384",
    "ES512",
    "PS256",
    "PS512",
    "RS384",
    "RS512"
  ],
  "id_token_encryption_alg_values_supported": [
    "RSA-OAEP",
    "RSA-ECB"
  ],
  "response_types_supported": [
    "code",
    "none",
    "id_token",
    "token"
  ],
  "token_endpoint_auth_methods_supported": [
    "private_key_jwt",
    "public",
    "client_secret_basic",
    "client_secret_post",
    "client_secret_jwt"
  ],
  "token_endpoint_auth_signing_alg_values_supported": [
    "PS384",
    "ES384",
    "RS256",
    "ES256",
    "HS512",
    "HS256",
    "HS384",
    "ES512",
    "PS256",
    "PS512",
    "RS384",
    "RS512"
  ],
  "claims_supported": [
    "sub",
    "iss",
    "auth_time",
    "name",
    "preferred_username",
    "given_name",
    "family_name",
    "email",
    "email_verified"
  ],
  "claim_types_supported": [
    "normal"
  ],
  "claims_parameter_supported": true,
  "scopes_supported": [
    "openid",
    "phone",
    "email",
    "address",
    "profile",
    "offline_access"
  ],
  "request_parameter_supported": true,
  "request_uri_parameter_supported": true,
  "require_request_uri_registration": false
}
```

---

## 主要なエンドポイント

| エンドポイント | 用途 |
|-----------|------|
| **authorization_endpoint** | ログイン画面へのリダイレクト |
| **token_endpoint** | アクセストークンの取得 |
| **userinfo_endpoint** | ユーザー情報の取得 |
| **jwks_uri** | JWT署名の公開鍵を取得 |
| **end_session_endpoint** | ログアウト処理 |
| **introspection_endpoint** | トークンの有効性確認 |

---

## .NET での使用方法

### 方法1: Well-Known を自動利用（推奨）

```csharp
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddOpenIdConnect(options =>
    {
        options.Authority = "http://localhost:8080/realms/treetopic";
        options.ClientId = "treetopic-app";
        options.ClientSecret = "your-client-secret";
        options.ResponseType = OpenIdConnectResponseType.Code;

        // Well-Known から自動取得
        // - authorization_endpoint
        // - token_endpoint
        // - userinfo_endpoint
        // - jwks_uri
        // などが自動で設定される
    });
```

ASP.NET Core は `Authority` から自動的に`.well-known/openid-configuration` にアクセスしてメタデータを取得します。

### 方法2: JWT Bearer 認証

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "http://localhost:8080/realms/treetopic";
        options.Audience = "treetopic-api";

        // Well-Known から jwks_uri を取得
        // JWT署名の検証に使用
    });
```

---

## Well-Known の動作フロー

```
1. クライアント起動
     ↓
2. Authority の .well-known/openid-configuration にアクセス
     ↓
3. Keycloak がメタデータを返却
     ↓
4. クライアントが以下を自動設定
   - authorization_endpoint
   - token_endpoint
   - jwks_uri
   - など
     ↓
5. ユーザーがログイン要求
     ↓
6. authorization_endpoint へリダイレクト
     ↓
7. ログイン後、token_endpoint でトークン取得
     ↓
8. jwks_uri でJWT署名を検証
```

---

## 開発環境での確認

### cURL で確認

```bash
curl http://localhost:8080/realms/treetopic/.well-known/openid-configuration | jq
```

### ブラウザで確認

```
http://localhost:8080/realms/treetopic/.well-known/openid-configuration
```

---

## TreeTopic での設定

### Program.cs の現在の設定

```csharp
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        serviceName: "keycloak",
        realm: "treetopic",
        options =>
        {
            options.Audience = "treetopic-app";
            if (builder.Environment.IsDevelopment())
            {
                options.RequireHttpsMetadata = false;
            }
        });
```

**動作:**
1. Aspire の Service Discovery で `keycloak` のエンドポイントを取得
2. Well-Known エンドポイントから `jwks_uri` を自動取得
3. JWT トークンの署名を検証

### AppHost での設定

```csharp
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);

var keycloak = builder.AddKeycloak("keycloak", port: 8080, adminPassword: keycloakAdminPassword)
    .WithDataVolume();

projectBuilder
    .WithReference(keycloak)
```

**動作:**
- Aspire が Keycloak のコンテナを管理
- Service Discovery により、クライアントが自動的に Keycloak を検出
- Well-Known から認証設定を取得

**Realm の初期化:**
- Keycloak は起動時に空の状態で初期化
- Realm 作成は手動で Admin Console から実施
- Well-Known はRealm作成後にアクセス可能

---

## トラブルシューティング

### "OpenIdConnectConfigurationNotFound" エラー

```
Microsoft.IdentityModel.Protocols.HttpRequestException:
IDX20803: Unable to obtain configuration from: 'http://localhost:8080/realms/treetopic/.well-known/openid-configuration'
```

**原因:**
- Keycloak が起動していない
- Realm名が間違っている
- Well-Known エンドポイントにアクセスできない

**解決:**
```bash
# Well-Known エンドポイントが応答しているか確認
curl http://localhost:8080/realms/treetopic/.well-known/openid-configuration

# Keycloak が起動しているか確認
docker ps | grep keycloak
```

### "jwks_uri not accessible" エラー

```
IDX20804: Unable to retrieve document from: 'http://localhost:8080/realms/treetopic/protocol/openid-connect/certs'
```

**原因:**
- JWT 署名の公開鍵を取得できない
- Keycloak が HTTPS を要求している

**解決:**
```csharp
// 開発環境で HTTPS 検証を無効化
options.RequireHttpsMetadata = false;
```

---

## セキュリティに関する注意

### 本番環境では必ず HTTPS を使用

```csharp
// 本番環境
if (!builder.Environment.IsDevelopment())
{
    options.RequireHttpsMetadata = true;  // 必須
    options.Authority = "https://your-keycloak-domain/realms/treetopic";
}
```

### Well-Known は公開情報

Well-Known エンドポイントのレスポンスは**認証なしでアクセス可能**です。機密情報は含まれていません。

```bash
# 認証なしでアクセス可能
curl http://localhost:8080/realms/treetopic/.well-known/openid-configuration
```

---

## 参考資料

- [OpenID Connect Discovery](https://openid.net/specs/openid-connect-discovery-1_0.html)
- [Keycloak OpenID Connect](https://www.keycloak.org/docs/latest/securing_apps/#_oidc)
- [ASP.NET Core OpenID Connect](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/oidc)
- [Microsoft Identity Platform](https://learn.microsoft.com/en-us/azure/active-directory/develop/active-directory-v2-protocols-oidc)
