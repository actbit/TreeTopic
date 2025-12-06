#import "@preview/slydst:0.1.5": *
#import "@preview/codly:1.3.0": *
#import "@preview/tablex:0.0.8": tablex

#set text(font: "Noto Sans JP") // 日本語フォントを設定

// プレゼンテーションの基本設定
#show: slides.with(
  title: "C#でマルチテナントと動的OIDC認証を実現する技術構成",
  ratio: 16/9,
  authors: ("近畿大学 マイコン部 竹内一希",),
  layout: "medium",
)

// --- カスタム関数と設定 ---
// codlyの設定（コードブロックの見栄えを良くする）
#show: codly-init.with()
#codly(
  languages: (
    csharp: (name: "C#", icon: "", color: rgb("#178600")),
  )
)

// Noteブロック用のスタイル
#let note(body) = {
  block(
    fill: luma(235),
    inset: 8pt,
    radius: 4pt,
    width: 100%,
    body
  )
}
// --- ここまで ---


= .NET Aspireで開発は"もっときれいに"なる

== Aspireが解決する開発の「面倒」なこと

分散アプリケーション開発には、多くの「面倒」がつきまといます。Aspireはそれらを解決します。

- "ローカルでDBも動かしたいけど、Dockerのセットアップが面倒..."
  → *`.AppHost`が解決*
- "サービスAからサービスBのURLはどうやって知るの？設定に書きたくない..."
  → *サービスディスカバリが解決*
- "全サービスのログを一箇所で見たい！問題追跡がつらい..."
  → *Aspireダッシュボードが解決*
- "一時的なネットワークエラーで処理が失敗して困る..."
  → *回復性 (Polly) が解決*

== 課題解決①: 複雑なローカル環境の簡素化

*Aspireの解決策*: `.AppHost`プロジェクトに、C\#コードでインフラ構成を記述できます。
#columns(2, gutter: 4pt)[
  #note([
    #set text(size: 10pt)
    ```csharp
    // src: TreeTopic.AppHost/AppHost.cs
    var builder = DistributedApplication.CreateBuilder(args);

    var postgres = builder.AddPostgres("postgres")
        .WithPgAdmin();

    var tenantDb = postgres.AddDatabase("treetopic-tenants");
    var appDb = postgres.AddDatabase("SharedApp");

    var projectBuilder = builder.AddProject<TreeTopic>("treetopic")
        .WithReference(tenantDb)
        .WithReference(appDb)
        .WaitFor(postgres);

    if (builder.Environment.IsDevelopment())
    {
        var keycloak = builder.AddKeycloak("keycloak", ...);
        projectBuilder.WithReference(keycloak);
    }
    builder.Build().Run();
    ```
  ])
]

  *主なポイント:*
  - `AddPostgres(...)`: PostgreSQLコンテナを定義。
  - `.WithPgAdmin()`: DB管理ツール(PgAdmin)を自動で追加。
  - `AddDatabase(...)`: コンテナ内にDBを作成。
  - `AddProject<...>(...)`: Web APIプロジェクトを追加。
  - `.WithReference(...)`: プロジェクトにDB等の接続情報を自動で連携。
  - `.WaitFor(...)`: DB等の準備が完了するまでプロジェクトの起動を待機。
  - `AddKeycloak(...)`: 開発用にKeycloak（認証サーバー）コンテナを追加。
  - `builder.Build().Run()`: Aspireアプリケーションをビルドして起動。

== 課題解決① (補足): 任意のコンテナを実行

`.AddPostgres()`のような定義済みのメソッドだけでなく、`AddContainer()`を使えば、どんなDockerコンテナでもAspireアプリケーションの一部として管理できます。

*例: 開発用のメールサーバー(`Mailpit`)を追加*
#note([
  ```csharp
  // Docker Hubの任意のイメージを指定
  var mailpit = builder.AddContainer("mailpit", "axllent/mailpit")
                       .WithHttpEndpoint(targetPort: 8025, hostPort: 1025);

  ```

])

  - `AddContainer("name", "image:tag")`: 好きなDockerイメージでコンテナを定義します。
  - `.WithHttpEndpoint(...)`: コンテナのポートをホストに公開し、サービスディスカバリの対象にします。
  - `.WithReference(mailpit)`: `treetopic`プロジェクトに`mailpit`のURL(`http://localhost:1025`など)が環境変数として自動で連携されます。

== 課題解決① (補足): Next.js(Node.js)フロントエンドの実行

Aspireでは、Node.jsベースのフロントエンドもバックエンドと同様に管理できます。

*開発時 (`npm` / ホットリロード)*
#note([
```csharp
// `npm run dev` を実行し、ホットリロードを有効化
var frontend = builder.AddNpmApp("frontend", "../path/to/next-app")
                      .WithHttpEndpoint(port: 3000);
```
])

*本番環境 (Docker)*
#note([
```csharp
// ビルド済みのDockerイメージを実行
var frontend = builder.AddContainer("frontend", "my-next-app:latest")
                      .WithHttpEndpoint(port: 3000);
```
])

== 課題解決②: サービス間の「見えない」問題を可視化

#align(center, image("images/スクリーンショット 2025-12-06 002849.png", width: 70%))

*課題*: マイクロサービス間で問題が起きた時、どのサービスのログを見ればいいか分からない。リクエストの追跡が困難。

*Aspireの解決策*: *Aspireダッシュボード*
- *構造化ログ*: 全サービスのログをまとめて確認・検索。
- *分散トレース*: リクエストがどのサービスを経由したかを可視化し、ボトルネックやエラー箇所を特定。
- *メトリクス*: 各サービスのCPU・メモリ使用量を監視。

== Copilot in Aspire Dashboard

.NET Aspireのダッシュボードには、GitHub Copilotが統合されています。これにより、AIデバッグアシスタントとして、開発者はより迅速に問題解決を行えます。

#columns(2, gutter: 12pt)[
  
    *ダッシュボードでのCopilot活用例:*
    - 大量のログメッセージを要約
    - 複数サービスにまたがるエラーの根本原因を調査
    - 分散トレースからパフォーマンスの問題を特定
    - 不明なエラーコードの意味を解説
  

  #align(center, image("images/スクリーンショット 2025-12-06 002908.png", width: 100%))
]


== 課題解決③: 全サービスで「品質」を標準化

*課題*: サービスごとにログ形式が違ったり、回復性ポリシー（リトライなど）を実装し忘れたりする。

*Aspireの解決策*: `.ServiceDefaults`プロジェクト
- *OpenTelemetry*: 統一形式のログ・トレース・メトリクスを自動収集。
- *標準の回復性*: `HttpClient`にリトライ処理などを自動追加。
- *ヘルスチェック*: 標準化された方法で稼働状況を報告。

各サービスは`builder.AddServiceDefaults()`の一行を追加するだけで、これらのベストプラクティスを導入できます。
#(src: `TreeTopic/Program.cs`)

= Finbuckle.MultiTenant: マルチテナント実装

== テナント解決の仕組み（リクエストの流れ）

#align(center, image("images/マルチテナント図1.drawio.png", width: 80%))

1.  *リクエスト到着*: 認証済みユーザーからのリクエストが到着します。
2.  *ミドルウェア実行*: `Finbuckle.MultiTenant`のミドルウェアが実行されます。
3.  *ClaimStrategy起動*: `CustomClaimStrategy` (src: `Services/CustomClaimStrategy.cs`) が呼び出され、ユーザーのJWTから`tenant`クレーム（例: "tenant-a"）を読み取ります。
4.  *Store起動*: `EFCoreMultiTenantStore` (src: `Services/EFCoreMultiTenantStore.cs`) が、テナントカタログDBに "tenant-a" を問い合わせ、対応するテナント構成情報を取得します。
5.  *テナント情報設定*: 取得されたテナント情報（`ApplicationTenantInfo`）が、このリクエストのコンテキストに設定されます。
6.  *DB接続切り替え*: `ApplicationDbContext`が使用される際、このテナント情報に基づいた正しいデータベース接続文字列が透過的に使用されます。

== 詳細: データ分離の実現

テナントごとのデータ分離は、`DbContext`が使われる際に、動的に接続文字列を切り替えることで実現されます。

#note([
  ```csharp
  // src: TreeTopic/Program.cs
  builder.Services.AddDbContext<ApplicationDbContext>((sp, options) =>
  {
      options.UseMultiTenantDatabase(sp);
  });
  ```
])
  `ApplicationDbContext`をDIコンテナに登録する際、ファクトリ関数を指定します。この中で自作の拡張メソッド`UseMultiTenantDatabase()`を呼び出します。
  
  このメソッドが内部で`IMultiTenantContextAccessor`を使い、現在のテナント情報から接続文字列を取得して、透過的に`DbContext`に設定します。
これにより、同じ`ApplicationDbContext`のコードを使いながらも、リクエストのテナントに応じて接続先のDBが自動で切り替わります。


= 動的OIDC認証

== テナントごとの動的なOIDC認証

このプロジェクト最大の特徴は、テナントごとに異なるOIDCプロバイダ（認証サーバー）を動的に切り替えられる点です。

- *なぜ必要か？*: 顧客（テナント）が自社で使用している既存の認証基盤（例: 自社運用のKeycloak, Azure AD）でログインしたい、というエンタープライズ向けの要求に応えるため。
- *実現方法*: ASP.NET CoreのOIDC認証イベントをフックし、リクエストの都度、テナントに応じたOIDC設定を動的に適用します。
  `Extensions/OpenIdConnectExtensions.cs`

== 動的OIDC認証の仕組み（イベントの流れ）

1.  *ログイン開始*: ユーザーがテナント "tenant-a" を指定してログインを開始します。（例: `/auth/login?tenant=tenant-a`）
2.  *OIDCイベント発生*: OIDCの`OnRedirectToIdentityProvider`イベントが発生します。
3. *設定取得*: イベントハンドラ内で、"tenant-a" のOIDC構成（KeycloakのエンドポイントURLや`ClientId`等）をテナントカタログDBから取得し、OIDCプロバイダのWell-knownエンドポイントから自動で設定情報を取得します。
4.  *OIDCオプション動的設定*: 取得した情報で、このリクエストのOIDCオプションを*動的に上書き*します。
5.  *リダイレクト*: ユーザーは "tenant-a" 専用のKeycloak認証画面にリダイレクトされます。
6.  *認証成功後イベント*: 認証後、`OnAuthorizationCodeReceived`イベントが発生します。
7.  *ClientSecret取得・トークン要求*: ここで再度テナントを特定し、対応する`ClientSecret`をDBから（復号して）取得し、アクセストークンを要求します。これにより、テナントごとの秘密情報を安全に利用できます。

= アーキテクチャの相乗効果

これら3つの技術は、以下のように連携して動作します。

1.  *開発時*: 開発者が *`.NET Aspire`* でプロジェクトを起動すると、APIサーバー、DB、Keycloakが自動で立ち上がります。
2.  *認証時*: ユーザーがログインしようとすると、*`動的OIDC`* の仕組みが働き、リクエストに応じたテナントの認証サーバーへリダイレクトされます。
3.  *認証後*: 認証が成功すると、ユーザーのクレームに`tenant`識別子が追加されます。
4.  *APIアクセス時*: 以降のAPIリクエストでは、*`Finbuckle.MultiTenant`* の`CustomClaimStrategy`が`tenant`クレームを読み取り、現在のテナントを確定させ、DBアクセス時に適切なテナントデータベースへ接続します。

この連携により、柔軟性と拡張性、そしてセキュリティを高いレベルで両立した、モダンなマルチテナントアプリケーションが実現されています。