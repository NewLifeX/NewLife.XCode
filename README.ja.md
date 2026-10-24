# NewLife.XCode - データミドルウェア / スーパーORM

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)

![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.XCode?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.XCode?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.XCode?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.XCode?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml)
[![locales](https://img.shields.io/badge/locales-31-2ea44f?logo=dotnet)](Doc/多语言本地化.md)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.XCode?logo=github)](https://github.com/NewLifeX/NewLife.XCode/graphs/contributors)

<!-- lang:start -->
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ この翻訳は構成を含め古くなっている可能性があります。正式な版は[英語版 README](README.md)です。

**NewLife.XCode** は、「CRUD + 極限のパフォーマンス + 大規模データ」に特化した高性能な .NET データミドルウェアです。マルチレベルキャッシュ、自動モデリング/マイグレーション、シャーディング、型付きクエリ、データベース間移行を備えています。MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / Network virtual database / NovaDb / SonnetDB などに対応しています。本番環境では 1 テーブルあたり数百億行に達し、クエリのスループットは（キャッシュ戦略により）数十億 QPS に達することがあります。

ドキュメント：https://newlifex.com/xcode   コミュニティ QQ グループ：1600800 / 1600838

**目次**：[クイックスタート](#クイックスタート) · [XCode を選ぶ理由](#xcode-を選ぶ理由) · [主な機能](#主な機能) · [エコシステムマップ](#エコシステムマップ) · [ドキュメント](#ドキュメント) · [コントリビュート](#コントリビュート) · [チーム](#チーム) · [ライセンス](#ライセンス)

## クイックスタート

### インストール

NuGet 参照（任意のプロジェクト）：

```
dotnet add package NewLife.XCode
```

またはテンプレートのスキャフォールドを使用します：

```
dotnet new install NewLife.Templates
dotnet new xcode --name Demo.Data
cd Demo.Data
dotnet build
```

> 強名証明書は組み込み済みです（newlife.snk）。企業向けプライベートフレームワーク用に再コンパイルできます。

### モデルの定義（db.xml）

```xml
<?xml version="1.0" encoding="utf-8"?>
<Tables Version="1.0" Output="MyEntity" NameSpace="Model" BaseClass="Entity" ConnName="DB">
  <Table Name="UserInfo" Description="ユーザー情報">
    <Columns>
      <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="会員 ID" />
      <Column Name="UserName" DataType="String" Nullable="False" Description="会員名" />
      <Column Name="Password" DataType="String" Nullable="False" Description="会員パスワード" />
      <Column Name="Age" DataType="Int32" Description="会員年齢" />
    </Columns>
  </Table>
</Tables>
```

### エンティティクラスの生成

- xcodetool.exe をディレクトリにダウンロードしてダブルクリックするか、次を実行します：`dotnet tool install xcodetool -g --prerelease` → `xcode db.xml`。
- 生成されるファイル：用户信息.cs（データクラス、上書きされます）+ 用户信息.Biz.cs（ビジネスクラス、上書きされません）。

### CRUD

```csharp
using XCode;

var user = new UserInfo { UserName = "山田太郎", Password = "123456", Age = 18 };
user.Insert();
Console.WriteLine("新しいレコードを挿入しました。ユーザー ID: " + user.Id);

var u2 = UserInfo.FindById(user.Id);
u2.Age = 19;
u2.Update();

var list = UserInfo.FindAll(UserInfo._.Age > 10 & UserInfo._.UserName == "山田太郎");
UserInfo.FindById(user.Id)?.Delete();
```

> キャッシュを使用する場合：エンティティ数が 1000 未満なら全テーブルキャッシュ、それ以外は通常のクエリ / 単一オブジェクトキャッシュを使用します。

## XCode を選ぶ理由

- **自社開発・独自実装**：中国で完全に開発され、サードパーティ製コンポーネントを一切使用していません。自ら管理でき、長期的な保守体制を備えています。
- **シンプルさ**：ビジネスシナリオの 90% は単一テーブルのエンティティ操作だけで済み、SQL を手書きする必要はありません。
- **Dapper 互換**：任意の SQL クエリを実行し、任意のエンティティモデルにマッピングできます。Dapper の使い方と完全に互換でありながら、キャッシュ、読み書き分離、トレーシングを追加しています。
- **極限のパフォーマンス**：主要パスでのマルチレベルキャッシュ + バッチ最適化 + インメモリ結合により、クエリ速度は生の SQL を大きく上回る可能性があります。
- **大規模データ**：ネイティブのページングとシャーディング戦略。本番プロジェクトでは 1 テーブルあたり数百億行に達し、ページングアルゴリズムは複数のデータベースに適応します。
- **安定性と成熟**：20 年以上の進化と幅広い本番検証を経ており、古い .NET Framework バージョン（net45 以降）との互換性を維持しています。
- **自動マイグレーション**：モデル/エンティティがそのままスキーマとなり、新しいフィールドはデプロイ時に自動同期されます。追加の SQL スクリプトは不要です。
- **分散への進化**：テーブル単位 / 時間 / 主キーによるネイティブなシャーディング戦略を備え、スムーズな分割とスケーリングを実現します。
- **プラグイン可能**：DAL 抽象化 + 分離されたプロバイダーにより、独自のデータベースアダプターをサポートします。

## 主な機能

<details>
<summary><b>モデリング &amp; コード生成</b> (5)</summary>

| 機能 | 主な特徴 |
| --- | --- |
| XML モデル（Tables/Columns/Indexes） | モデルが唯一の情報源であり、属性はインデックス、一意制約、既定値、説明、拡張プロパティをカバーします。 |
| コードジェネレーター | `xcodetool`（グローバルツール / exe）または VS の T4 テンプレートが、XML モデルからエンティティクラスを生成します。 |
| データクラス（`.cs`） | 生成のたびに上書きされます。フィールドと基本クエリを保持します。 |
| ビジネスクラス（`.Biz.cs`） | 初回生成後は上書きされません。カスタムロジックはここに記述します。 |
| 自動生成メソッド | インデックスと列から `FindByXxx` / `Search` ヘルパーメソッドとテンプレートが自動生成されます。 |

</details>

<details>
<summary><b>エンティティランタイム &amp; DAL</b> (7)</summary>

| コンポーネント | 役割 |
| --- | --- |
| `IEntity` / `Entity` | フィールド、検証、ライフサイクルをカプセル化したリッチなドメインモデル。 |
| `IEntityOperate` | 静的操作用のエントリポイント（Find / FindAll / Query など）。 |
| `EntityFactory` | エンティティメタデータキャッシュのファクトリ。 |
| `EntitySession`（`Meta.Session`） | 特定の接続/テーブルにおけるエンティティのコンテキスト。セッションレベルのキャッシュ/統計を持ちます。 |
| `Meta` | エンティティの静的コンテキスト（テーブルスキーマ、フィールド、キャッシュ、シャーディングポリシーなど）。 |
| `DAL` / `IDatabase` / `IDbSession` | 統一されたデータベースアクセスとメタデータ操作。 |
| `CheckTables()` | 起動時/初回アクセス時にスキーマを確認・同期します。必要に応じて非同期または同期で実行できます。 |

</details>

<details>
<summary><b>クエリ DSL</b> (8)</summary>

| 機能 | 主な特徴 |
| --- | --- |
| `IQueryable<T>` LINQ（v12.0） | `Entity<T>.Query` が `IQueryable<T>` を返し、`Where/OrderBy/Skip/Take/Count/First/ToList` などの標準 LINQ 操作をサポートします。 |
| `DAL.Select<T>()` Fluent クエリ（v12.1） | `DAL.Select<T>()` が現在の接続にバインドされた `IQueryable<T>` を返し、`WhereIf/Where/OrderBy/Skip/Take/Page/Count/FirstOrDefault/ToList` の完全なチェーン呼び出しをサポートします。 |
| ナビゲーションプロパティ（v12.1） | `HasOne`/`HasMany` Fluent API でナビゲーション関係を構成し、`Include` がパスを構築して一括読み込みし、`SelectBuilder` がナビゲーション関係をまたぐ JOIN をサポートします。 |
| `WhereIf` インライン条件 | `Query.WhereIf(condition, predicate)` と `FindAllWhereIf` により動的フィルタリングが可能です。 |
| `WhereExpression` | `&` / `\|` / `Contains` / `Between` / `Like` を連鎖させた式の合成。 |
| `Find` / `FindAll` / `Query` / `PageFindAll` | クエリのエントリポイント。`PageFindAll` にはページングが組み込まれています。 |
| `Pager` / `PageParameter` | ページング、並べ替え、`TotalCount` 統計、データスコープ拡張を保持します。 |
| ウィンドウ関数（v12.0） | `SelectBuilder.Window` + `WindowFunction` が ROW_NUMBER/RANK/DENSE_RANK および集計ウィンドウ関数をサポートします。 |

</details>

<details>
<summary><b>マルチレベルキャッシュシステム</b> (6)</summary>

読み取りが多く書き込みが少ないワークロード向けに設計されています：

| キャッシュ | 主な特徴 |
| --- | --- |
| `Meta.Count` | 高速な行数キャッシュ（1000 を超える場合はシステムインデックスを使用。SQLite は Max(identity) と非同期の正確な補正を使用）。 |
| `EntityCache`（`Meta.Cache.Entities`） | 全テーブルキャッシュ（1000 行未満の小さなテーブルに推奨。10000 行を超える場合は禁止）。 |
| `SingleCache`（`Meta.SingleCache[key]`） | 主キー/代替キーによる辞書レベルの単一オブジェクトキャッシュ。ユーザーのような頻繁なポイント参照に適しています。 |
| 有効期限ポリシー | 既定では 60 秒。期限切れの読み取りは古いデータを即座に返し、非同期で更新します。書き込みはキャッシュを即座に無効化/更新します。 |
| トランザクション認識 | トランザクション内では変更が蓄積され、コミット/ロールバック後にキャッシュが一律でクリアまたはロールバックされます。 |
| 二次キャッシュ（v12.0） | `CacheInvalidator.Provider` が Redis を注入し、バージョン機構を通じてプロセス間でエンティティキャッシュの無効化を調整します。 |

</details>

<details>
<summary><b>リバースエンジニアリング &amp; 移行</b> (3)</summary>

| 機能 | 主な特徴 |
| --- | --- |
| スキーマのリバースエンジニアリング | エンティティからテーブルスキーマとインデックスを逆生成・変更し、自動的に整合性を保ちます。 |
| エンティティ単位の移行ポリシー | テーブルレベルの `Migration` 設定（モデルの `Table` 属性 / `[BindTable]` / `TableItem` の起動時上書き）により、エンティティごとのリバースエンジニアリング戦略（Off / ReadOnly / On / Full）を制御します。 |
| モデル抽出とデータベース間移行 | 任意のデータベースからのモデル抽出 + データベース間のデータ移行（DataMigration コンパニオンツール）をサポートします。 |

</details>

<details>
<summary><b>シャーディング（ShardPolicy）</b> (3)</summary>

| 機能 | 主な特徴 |
| --- | --- |
| `ShardPolicy` 抽象化 | `TimeShardPolicy` の例：日付ごとにテーブル名 Table_yyyyMMdd。 |
| 透過的なルーティング | ビジネスコードは引き続き Find / Insert / Update を呼び出すだけで、実際のテーブル名を意識しません。 |
| カスタム戦略 | 主キーハッシュ / 時間 / 範囲の戦略をカスタマイズできます。 |

</details>

<details>
<summary><b>トランザクション &amp; キャッシュ整合性</b> (3)</summary>

| シナリオ | 主な特徴 |
| --- | --- |
| 非トランザクション書き込み | 書き込みは関連キャッシュを即座に期限切れにし、非同期で再構築します。同時に読み書きの整合性のため、インメモリオブジェクトの直接更新を試みます。 |
| コミット | トランザクション内では全テーブルキャッシュはクリアされず、ヒットしたオブジェクトのみローカルで変更されます。コミット後に一律でクリアまたは更新されます。 |
| ロールバック | ロールバック時には、トランザクション内で行った変更が元に戻されます。 |

</details>

<details>
<summary><b>パフォーマンス最適化</b> (6)</summary>

| 手法 | 主な特徴 |
| --- | --- |
| マルチレベルキャッシュ | ホットなクエリをデータベースから遠ざけます。 |
| バッチ書き込み | 複数フィールド・複数レコードの操作をマージします（内部最適化）。 |
| ページングアルゴリズム | データベース/バージョンごとに最適な戦略を選択します。 |
| 遅延/非同期 | 行数、モデルチェック、キャッシュ更新はすべて非同期で実行できます。 |
| リフレクションキャッシュ / デリゲート高速化 | エンティティのプロパティアクセサーを事前にウォームアップします。 |
| インメモリ関連付け | まずメインテーブルをキャッシュし、その後メモリ内で「疑似結合」を実行します。 |

</details>

<details>
<summary><b>データスコープ &amp; マルチテナント</b> (5)</summary>

| 機能 | 主な特徴 |
| --- | --- |
| データスコープ | 行レベルのデータアクセス制御。「すべて / 部門とサブ部門 / 部門 / 自分のみ / カスタム」の 5 つのスコープをサポートします。 |
| スコープの組み込み | `DataScopeContext` + `DataScopeInterceptor` が、クエリフィルターの適用、挿入時の既定の所有権設定、更新/削除時の検証を自動で行います。 |
| エンティティのオプトイン | エンティティが `IUserScope` / `IDepartmentScope` / `IDataScope` を実装します。部門管理者は自動的に可視スコープに含まれ、自分のデータには常にアクセスできます。 |
| マルチテナント | エンティティが `ITenantScope` を実装すると、`TenantInterceptor` がクエリにテナントフィルターを自動付加し、書き込み時にテナント識別子を検証/補完します。`TenantId` 列を持つビジネスクラスでは、EntityBuilder が自動的に `ITenantScope` を実装します。 |
| 詳細 | 詳細は `Doc/数据权限.md` と `Doc/权限管理.md` を参照してください。 |

</details>

<details>
<summary><b>マルチデータベース構成</b> (4)</summary>

| 機能 | 主な特徴 |
| --- | --- |
| 既定は SQLite | 開発時は SQLite が既定です（ゼロ構成）。 |
| 接続設定 | デプロイ時は構成ファイルまたは環境変数で `XCode_{ConnName}` を構成します。 |
| プロバイダー識別子 | 接続文字列にはプロバイダー識別子を含める必要があります（例：`provider=MySql;server=...;user=...;******;database=...;`）。 |
| 複数接続 | 同一プロセス内で複数のデータベース接続名（ConnName）を併用できます。 |

</details>

### 使用例

#### 動的な条件とページング

```csharp
public static IList<UserInfo> Search(String name, Int32 age, Pager p)
{
    var exp = new WhereExpression();
    if (!name.IsNullOrEmpty()) exp &= UserInfo._.UserName == name;
    if (age > 0) exp &= UserInfo._.Age == age;
    return UserInfo.FindAll(exp, p);
}
```

#### 指定フィールドの更新（キャッシュをバイパス。通常の使用には推奨しません）

```csharp
UserInfo.Update(UserInfo._.UserName == "山田太郎" & UserInfo._.Age == 18, UserInfo._.Id == 1);
```

#### カスタム並べ替えと列の射影

```csharp
var list = UserInfo.FindAll(UserInfo._.Age > 10, " Age DESC,Id ASC",
    string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 20);
```

#### インデックスと生成メソッド

モデルにインデックスを追加します：

```xml
<Indexes>
  <Index Columns="UserName" Unique="True" />
  <Index Columns="UserName,Age" />
</Indexes>
```

再生成後、`FindByUserName`、複合条件のヘルパーメソッド / Search テンプレートが自動的に生成されます。

エンティティキャッシュ戦略：`Meta.Session.Count < 1000` の場合はインメモリコレクションの検索を使用し、それ以外はデータベースまたは単一オブジェクトキャッシュを使用します。

```csharp
public static UserInfo FindByUserName(String userName)
{
    if (userName.IsNullOrEmpty()) return null;
    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserName.EqualIgnoreCase(userName));
    return Find(_.UserName == userName);
}
```

#### シャーディングの例

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(UserInfo.Id), Meta.Factory)
{
    ConnPolicy = "{0}",           // カスタマイズ可能なデータベース名ポリシー
    TablePolicy = "{0}_{1:yyyyMMdd}", // テーブル名テンプレート
    Step = TimeSpan.FromDays(1)
};
```

Insert / Query などの他のコードはすべて変更不要で、自動的に対応する日付テーブルへルーティングされます。

#### 元のサンプル（保持）

<details>
<summary><b>元の CRUD と複雑なクエリのサンプル</b></summary>

##### 作成・読み取り・更新・削除

```csharp
var user = new UserInfo();
user.UserName = "山田太郎";
user.Password = "123456";
user.Age = 18;
user.Insert();
Console.WriteLine("新しいレコードを挿入しました。ユーザー ID: " + user.Id);

var u = UserInfo.FindById(1);
u.UserName = "山田太郎";
u.Password = "123456";
u.Age = 19;
u.Update();

u = UserInfo.FindById(1);
Console.WriteLine($"ユーザー ID={u.Id} 年齢を更新しました: {u.Age}");
```

##### 複雑なクエリ

```csharp
var list = UserInfo.FindAll(UserInfo._.UserName == "山田太郎" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
```

</details>

### ベンチマーク

| 機能 | 指標（一般的な環境） | 備考 |
| ---- | ---------------- | ---- |
| シングルマシン Insert | 最大約 890k TPS | バッチ / キャッシュ + SQLite / RAM ディスクの組み合わせテスト |
| シングルマシン Query | 最大約 18 億 QPS | 小さなテーブル + エンティティキャッシュ / 単一オブジェクトキャッシュのシナリオ |
| テーブルあたりのデータ量 | 本番で数百億行 | ページング + シャーディング戦略 |

> 実際のパフォーマンスは、データベースの種類 / ハードウェア / シャーディング戦略 / キャッシュヒット率に依存します。

### FAQ

- 最初の Count が速いのはなぜですか？ `Meta.Count` はシステムインデックスを使用します。SQLite ではまず identity の最大値を取得し、その後非同期で精緻化する場合があります。
- 更新後、キャッシュはどれくらいで整合しますか？ 現在のプロセス内の書き込みは即座に反映されます。プロセス間ではキャッシュの期限切れを待つか、アクティブな無効化戦略を使用してください。
- 自動マイグレーションを無効にできますか？ `Migration=Off` を設定するか（グローバルまたはテーブル単位）、必要に応じて ReadOnly/On/Full を使用します。
- 複雑な複数テーブルのクエリは？ 複数の単一テーブルクエリ + インメモリ関連付けを推奨します（キャッシュヒット時は高速で、構造も明確です）。

## エコシステムマップ

NewLife は、独立したバージョン管理を行うオープンソースプロジェクトの階層型エコシステムです。以下のマップはすべての NewLife リポジトリで逐語的に同一の内容を共有しており、各プロジェクトはコードリポジトリと主要な NuGet パッケージにリンクしています。

| プロジェクト | 概要 | ダウンロード数 |
| --- | --- | --- |
| **基盤** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | ログ、構成、キャッシュ、ネットワーク、シリアライズ、スケジューリング、APM トレーシング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | データミドルウェア：単一テーブルで数百億件級、自動シャーディング、読み書き分離、10 種類以上のデータベースに対応。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高性能ネットワークライブラリ：単一マシンで 177 万 pps、14.5 Gbps（TCP 1KB、2026-10 実測）、400 万 Tcp 長接続。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | CS アプリケーションと IoT デバイス向けのプロトコル通信と RPC フレームワーク。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | .NET アプリケーションを OS デーモンとしてインストール：Windows サービスと Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | コンソール、Web、API、サービス、データ、IoT 向けの `dotnet new` プロジェクトテンプレート。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **データとメッセージング** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | マイクロ秒級の遅延、数百万 ops/s のスループット、メッセージキュー内蔵の Redis クライアント。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Apache RocketMQ と Alibaba Cloud Message Queue に対応した純マネージド RocketMQ クライアント。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT クライアントとサーバー。Alibaba Cloud IoT への接続を含みます。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 製ハイブリッドデータベース：組み込み/ネットワークの両モード、リレーショナル、時系列、メッセージキュー、KV を 1 つに。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | NewLife データスタック上に構築された MySql ドライバーとヘルパー層。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI、分散** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 高速管理プラットフォームフレームワーク：権限、SSO、OAuth サーバー、CRUD スキャフォールディング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 分散サービスプラットフォーム：ノード管理、APM、構成センター、登録センター、リリースセンター。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | シャーディング、リトライ、バックフィルに対応した分散タスクとリアルタイムデータのスケジューリングプラットフォーム。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 46 社の LLM プロバイダーに統一接続するクライアントと、すぐに使える Web チャットアプリ ChatAI。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **産業と IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | プロトコル契約とデバイス抽象を定義する IoT 標準ライブラリ。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | NewLife.IoT 上に実装された Modbus TCP/RTU/ASCII。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | NewLife.IoT 上に実装されたシーメンス PLC プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | NewLife.IoT 上に実装されたオムロン PLC プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | NewLife.IoT 上に実装された三菱 Melsec PLC プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | 産業データアクセス向けの OPC クライアント。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet ビル管理プロトコルスタック。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | ゲートウェイとデバイス管理を備えた軽量なオープンソース IoT プラットフォーム。 | — |
| **車載テレマティクス** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 車両位置情報プロトコル：70 以上のメッセージボディ、2011/2013/2019 に対応。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | 運輸管理部門向けの JT/T 809 プラットフォーム間プロトコル。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 車載ビデオ：RTP 解析、FLV/fMP4 リマックス、ライブ配信。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **ドメインコンポーネント** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Excel、Word、PPT、PDF など 15 以上のドキュメント形式を 1 パッケージで。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | クロスプラットフォーム音声：コーデック、デバイス IO、DSP、コンテナ、ストリーミング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | ビデオコーデック、コンテナ形式、フレーム処理、ストリーミング。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度地図、高徳地図、テンセント地図、天地図を統一インターフェイス化。ジオコーディングと座標変換に対応。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 位置情報：オフラインのメモリマップデータベースとオンライン API のフォールバック。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **ツールと製品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 開発者向けツールボックス：ネットワーク、シリアルポート、暗号化、正規表現、Modbus、MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | セルフホスト型 Git ソースコード管理システム。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 古いホストから起動した際に対応する .NET ランタイムを自動インストールします。 | — |

> バッジは各プロジェクトの主要パッケージを示します。テーマ、拡張、サテライトパッケージは各 NuGet ページを参照してください。ここに記載していない 30 以上のパッケージ（FTP、LoRa、Office 拡張、各種プロトコルドライバーなど）は [NewLifeX 組織](https://github.com/NewLifeX) と [newlifex.com](https://newlifex.com) で公開しています。この表は NewLife.Core の README を保守元としています。

## ドキュメント

- [XCode ドキュメントナビゲーション](Doc/XCode文档导航.md)：モジュール別に整理された完全なドキュメント索引
- [XCode ユーザーマニュアル](Doc/XCode使用手册.md)：エンドツーエンドの利用手順
- [アーキテクチャ設計](Doc/架构设计.md)：レイヤー、コンポーネント、主要な設計判断
- [多言語ローカライズ](Doc/多语言本地化.md)：メッセージモデル、言語テーブル、実行時の上書き
- オンラインドキュメント：https://newlifex.com/xcode
- [ChangeLog.md](ChangeLog.md)：バージョン履歴

## コントリビュート

### ビルドとテスト

```
dotnet restore
dotnet build -c Release
dotnet test XUnitTest.XCode -c Release
```

- テストケースは `XUnitTest.XCode` に追加してください。パフォーマンスに関わる提案には、ベンチマークまたは説明を添えてください。
- リポジトリの規約に従ってください（強名、マルチ TFM、XML コメント、条件付きコンパイル）。
- 公開 API の変更には移行戦略（Obsolete → 削除）が必要です。

### リリース

`v` プレフィックスのタグ（例：`v2026.1018`）をプッシュすると `.github/workflows/publish.yml` がトリガーされます。ワークフローは XCode、XCodeTool、およびプロバイダーパッケージ（`XCode.MySql`, `XCode.SQLite`, `XCode.SqlServer`, `XCode.Oracle`, `XCode.PostgreSQL`, `XCode.DaMeng`, `XCode.DB2`, `XCode.KingBase`, `XCode.HighGo`）をパックし、NuGet.org と GitHub Packages にプッシュします。

### フィードバック

- Issue / PR を歓迎します：パフォーマンス最適化 / 新しいデータベースプロバイダー / ドキュメントの改善。
- 提出前に `dotnet build` と関連するユニットテストが通ることを確認し、コードスタイルとコミットメッセージに関するリポジトリの規約に従ってください。
- バグや提案は [Issue](https://github.com/NewLifeX/NewLife.XCode/issues) で報告してください。Star と PR を歓迎します。

## チーム

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

2002 年に設立された NewLife チームは、20 年以上にわたり .NET と IoT のエコシステムに取り組み、100 近いオープンソースプロジェクトを維持し（うち 34 がこのマップの中核）、電力、物流、医療、産業制御、インターネットなどの業界にソフトウェアとハードウェアを一体化したソリューションを提供しています。「学問に先後なし、達する者が師である」を信条としています。

オープンソースコンポーネントは NuGet で累計 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 回ダウンロードされています（エコシステム全体、2026-10 時点）。

ウェブサイト：https://newlifex.com · オープンソース組織：https://github.com/NewLifeX

<details>
<summary>WeChat 公式アカウント：智能大石头（スキャンしてフォロー）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## ライセンス

MIT、完全オープン。商用利用 / 改変 / 再配布が可能です（プライベートデプロイでは「NewLife」を自社ブランドに置き換えられます）。強名証明書 `newlife.snk` は組み込み済みです。

> このプロジェクトが役に立ったら、Star を付けるか、チームで共有するか、Issue/PR で改善提案をお送りください。
