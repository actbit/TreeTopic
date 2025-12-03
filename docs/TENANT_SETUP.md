# テナント初期化ガイド

TreeTopicはマルチテナント対応です。本ガイドではテナントの初期作成から初期化までの流れを説明します。

---

## テナント作成フロー

### 1. テナントの登録

API エンドポイント: `POST /api/tenant/register`

#### リクエスト例

```bash
curl -X POST http://localhost:5001/api/tenant/register \
  -H "Content-Type: application/json" \
  -d '{
    "identifier": "tenant-01",
    "name": "Tenant One",
    "dbProvider": "postgres",
    "openIdConnectMetadataAddress": "http://localhost:8080/realms/treetopic/.well-known/openid-configuration",
    "openIdConnectClientId": "treetopic-app",
    "openIdConnectClientSecret": "your-client-secret",
    "roleClaimName": "roles"
  }'
```

#### リクエストパラメータ

| パラメータ | 型 | 必須 | 説明 |
|-----------|-----|------|------|
| **identifier** | string | ✅ | テナント識別子（ユニーク） |
| **name** | string | ✅ | テナント名 |
| **dbProvider** | string | ❌ | データベースプロバイダー（デフォルト: postgres） |
| **connectionString** | string | ❌ | カスタム接続文字列（未指定時は SharedApp を使用） |
| **openIdConnectMetadataAddress** | string | ❌ | Keycloak メタデータURL |
| **openIdConnectClientId** | string | ❌ | Keycloak クライアントID |
| **openIdConnectClientSecret** | string | ❌ | Keycloak クライアントシークレット |
| **roleClaimName** | string | ❌ | ロール情報を含むClaimの名前 |

#### レスポンス例

```json
{
  "identifier": "tenant-01",
  "setupToken": "abc123def456ghi789"
}
```

**setupToken について:**
- **1時間の有効期限**を持つトークン
- テナント初期化時に使用
- 以降のAPI呼び出しでヘッダーに含める

---

### 2. 自動実行される処理

テナント登録時に以下が自動的に実行されます：

#### 2.1 テナント情報の保存

- TenantCatalog DB に テナント情報を記録
- 暗号化キー生成・保存
- Connection String の暗号化・保存

#### 2.2 データベースマイグレーション

- テナント専用DB を作成
- テーブル・スキーマを初期化
- 外部キー制約を設定

#### 2.3 セットアップトークン生成

- テナント初期化用のトークンを生成
- Hash化して保存
- **有効期限: 1時間**

---

### 3. テナント初期化（setupToken を使用）

#### リクエスト例

```bash
curl -X POST http://localhost:5001/{tenant-identifier}/api/setup \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {setupToken}" \
  -d '{
    "adminUsername": "admin",
    "adminEmail": "admin@tenant-01.local",
    "adminPassword": "SecurePassword123!"
  }'
```

**動作:**
- セットアップトークンで初期管理者ユーザーを作成
- テナント用アプリケーション DB を初期化
- トークンを無効化（1時間後に自動失効）

---

## Aspire 環境でのテナント作成

### 前提条件

1. AppHost が起動している
   ```bash
   dotnet run --project TreeTopic.AppHost -- --parameter keycloak-admin-password=admin123
   ```

2. Keycloak がアクセス可能
   ```
   http://localhost:8080
   ```

### テナント作成手順

#### ステップ0: Keycloak で Realm を作成

1. Keycloak Admin Console にアクセス
   ```
   http://localhost:8080
   admin / {your-keycloak-admin-password}
   ```

2. Realm を新規作成
   - 左上の **Master** → **Create Realm**
   - **Realm name**: `treetopic`
   - **Create** をクリック

#### ステップ1: Keycloak でクライアントを作成（テナント用）

1. `treetopic` Realm を選択済みの状態で続行

3. Clients → Create client
   - Client ID: `tenant-01-app`
   - Client authentication: On

4. Credentials から Client secret をコピー

#### ステップ2: テナントを登録

```bash
curl -X POST https://localhost:5001/api/tenant/register \
  -H "Content-Type: application/json" \
  -k \
  -d '{
    "identifier": "tenant-01",
    "name": "My First Tenant",
    "openIdConnectMetadataAddress": "http://localhost:8080/realms/treetopic/.well-known/openid-configuration",
    "openIdConnectClientId": "tenant-01-app",
    "openIdConnectClientSecret": "your-copied-client-secret",
    "roleClaimName": "roles"
  }'
```

**注:**
- `-k` フラグで自己署名証明書を許可（開発環境のみ）
- レスポンスから `setupToken` をコピー

#### ステップ3: テナントを初期化

```bash
SETUP_TOKEN="abc123def456ghi789"  # 上記で取得したトークン
TENANT_ID="tenant-01"

curl -X POST https://localhost:5001/${TENANT_ID}/api/setup \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer ${SETUP_TOKEN}" \
  -k \
  -d '{
    "adminUsername": "admin",
    "adminEmail": "admin@tenant-01.local",
    "adminPassword": "SecurePassword123!"
  }'
```

---

## テナント情報の確認

### すべてのテナント一覧を取得

```bash
curl -X GET https://localhost:5001/api/tenant \
  -H "Authorization: Bearer {admin-token}" \
  -k
```

**必要な権限:** Admin ロール

### 特定のテナント情報を取得

```bash
curl -X GET https://localhost:5001/api/tenant/tenant-01 \
  -H "Authorization: Bearer {admin-token}" \
  -k
```

---

## テナント削除

```bash
curl -X DELETE https://localhost:5001/api/tenant/{tenant-id} \
  -H "Authorization: Bearer {admin-token}" \
  -k
```

**警告:** テナント削除時に、テナント専用DB のデータも削除されます（復旧不可）

---

## トラブルシューティング

### "setupToken is invalid or expired" エラー

**原因:** セットアップトークンが1時間以上前に生成された

**解決:** テナントを再度登録して新しいトークンを取得

### "Tenant with identifier already exists" エラー

**原因:** 同じ identifier のテナントが既に存在

**解決:** 別の identifier を使用するか、既存テナントを削除

### "Failed to retrieve OIDC metadata" エラー

**原因:** Keycloak メタデータアドレスが不正、または Keycloak にアクセスできない

**解決:**
```bash
# メタデータエンドポイントが応答しているか確認
curl http://localhost:8080/realms/treetopic/.well-known/openid-configuration
```

### テナントDB マイグレーション失敗

**原因:** 接続文字列が不正、またはデータベースにアクセスできない

**解決:**
```bash
# 接続文字列を確認
# PostgreSQL が起動しているか確認
docker ps | grep postgres
```

---

## セキュリティのベストプラクティス

### 本番環境での推奨設定

1. **HTTPS 必須**
   ```bash
   curl -X POST https://your-domain/api/tenant/register
   ```

2. **setupToken の管理**
   - セキュアなチャネル経由でのみ送信
   - ログに記録しない
   - 有効期限後は使用不可

3. **Keycloak Client シークレット**
   - 環境変数または Key Vault から読み込む
   - リクエストボディに含めない

4. **テナント登録エンドポイント**
   - API Gateway や認可層で保護
   - レート制限を設定

---

## 参考資料

- [Finbuckle.MultiTenant ドキュメント](https://docs.finbuckle.com/multitenant/)
- [Entity Framework Core マイグレーション](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [OpenID Connect](https://openid.net/connect/)
