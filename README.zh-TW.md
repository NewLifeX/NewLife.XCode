# NewLife.XCode - 資料中介層 / 超級 ORM

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ 本翻譯可能落後，包括其結構；以[英文 README](README.md)為準。

高效能 .NET 資料中介層，聚焦「增刪改查 + 極致效能 + 海量資料」，內建多層快取、自動建模/移轉、分表分庫、強型別查詢、跨庫移轉，支援 MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / 達夢 / 金倉 / 瀚高 / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / Network 網路虛擬資料庫 / NovaDb 自研資料庫等。單表生產實踐達百億級，查詢吞吐可達十億級 QPS（搭配快取策略）。

文件：https://newlifex.com/xcode   社群交流 QQ 群：1600800 / 1600838

> ℹ️ 本繁體中文版與 [English README](README.md) 同批維護；若兩者內容不一致，以英文版為對外口徑。

**目錄**：[快速開始](#快速開始) · [為什麼選擇 XCode](#為什麼選擇-xcode) · [主要功能](#主要功能) · [生態版圖](#生態版圖) · [文件](#文件) · [參與貢獻](#參與貢獻) · [新生命開發團隊](#新生命開發團隊) · [授權條款](#授權條款)

## 快速開始

### 安裝

NuGet 參考（任一專案）：

```
dotnet add package NewLife.XCode
```

或使用範本骨架：

```
dotnet new install NewLife.Templates
dotnet new xcode --name Demo.Data
cd Demo.Data
dotnet build
```

> 強式命名憑證已內建（newlife.snk），可自行重新編譯用於企業私有框架。

### 定義模型（db.xml）

```xml
<?xml version="1.0" encoding="utf-8"?>
<Tables Version="1.0" Output="MyEntity" NameSpace="Model" BaseClass="Entity" ConnName="DB">
  <Table Name="UserInfo" Description="使用者資訊">
    <Columns>
      <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="會員 ID" />
      <Column Name="UserName" DataType="String" Nullable="False" Description="會員名稱" />
      <Column Name="Password" DataType="String" Nullable="False" Description="會員密碼" />
      <Column Name="Age" DataType="Int32" Description="會員年齡" />
    </Columns>
  </Table>
</Tables>
```

### 產生實體類別

- 下載 xcodetool.exe 置於目錄並雙擊，或：`dotnet tool install xcodetool -g --prerelease` → `xcode db.xml`。
- 產生：使用者資訊.cs（資料類別，會覆寫） + 使用者資訊.Biz.cs（業務類別，不再覆寫）。

### 增刪改查

```csharp
using XCode;

var user = new UserInfo { UserName = "張三", Password = "123456", Age = 18 };
user.Insert();
Console.WriteLine("插入一筆新資料，使用者 ID 為：" + user.Id);

var u2 = UserInfo.FindById(user.Id);
u2.Age = 19;
u2.Update();

var list = UserInfo.FindAll(UserInfo._.Age > 10 & UserInfo._.UserName == "張三");
UserInfo.FindById(user.Id)?.Delete();
```

> 使用快取時：實體數 <1000 → 整表快取；否則一般查詢 / 單物件快取。

## 為什麼選擇 XCode

- **國產原創**：完全由國人開發，未引用任何第三方元件，自主可控，長期維護有保障。
- **化繁為簡**：90% 業務只需單表實體操作，無須手寫 SQL。
- **相容 Dapper**：支援執行任意 SQL 查詢並對應到任意實體模型，完全相容 Dapper 用法，同時提供快取、讀寫分離與鏈路追蹤等增強能力。
- **極致效能**：核心主路徑多層快取 + 批次最佳化 + 記憶體聯結，查詢速度可遠高於直接寫 SQL。
- **海量資料**：原生分頁與分表策略，正式專案單表百億級，適配多種資料庫分頁演算法。
- **穩定成熟**：20+ 年演進，廣泛生產驗證；保持對舊版 .NET Framework 的相容（net45 起）。
- **自動移轉**：模型/實體即結構，新增欄位上線自動同步，無須額外 SQL 指令碼。
- **分散式演進**：原生表級 / 時間 / 主鍵分片策略，可平滑拆分擴容。
- **可插拔**：DAL 抽象 + Provider 分離，支援自訂資料庫轉接。

## 主要功能

<details>
<summary><b>模型與程式碼產生</b>（5 項）</summary>

| 功能點 | 主要特點 |
| --- | --- |
| XML 模型（Tables/Columns/Indexes） | 模型即唯一事實來源；屬性涵蓋索引、唯一約束、預設值、描述與擴充屬性。 |
| 程式碼產生器 | `xcodetool`（全域工具 / exe）或 VS T4 範本，將 XML 模型產生為實體類別。 |
| 資料類別（`.cs`） | 每次產生皆覆寫，承載欄位與基礎查詢。 |
| 業務類別（`.Biz.cs`） | 首次產生後不再覆寫，放置自訂邏輯。 |
| 自動方法 | 索引與欄位自動產生 `FindByXxx` / `Search` 輔助方法與範本。 |

</details>

<details>
<summary><b>實體執行階段與 DAL</b>（7 項）</summary>

| 元件 | 職責 |
| --- | --- |
| `IEntity` / `Entity` | 充血模型，封裝欄位、驗證、生命週期。 |
| `IEntityOperate` | 靜態操作入口（Find / FindAll / Query 等）。 |
| `EntityFactory` | 實體中繼資料快取工廠。 |
| `EntitySession`（`Meta.Session`） | 實體在特定連線/資料表上的內容，帶工作階段級快取/統計。 |
| `Meta` | 實體靜態內容（資料表結構、欄位、快取、分片策略等）。 |
| `DAL` / `IDatabase` / `IDbSession` | 統一的資料庫存取與中繼資料操作。 |
| `CheckTables()` | 啟動/首次存取時視需要非同步或同步檢查並同步資料表結構。 |

</details>

<details>
<summary><b>查詢 DSL</b>（8 項）</summary>

| 能力 | 主要特點 |
| --- | --- |
| `IQueryable<T>` LINQ 查詢（v12.0 新增） | `Entity<T>.Query` 傳回 `IQueryable<T>`，支援 `Where/OrderBy/Skip/Take/Count/First/ToList` 等標準 LINQ 操作。 |
| `DAL.Select<T>()` 串流查詢（v12.1 新增） | `DAL.Select<T>()` 傳回 `IQueryable<T>` 並自動繫結目前連線，支援 `WhereIf/Where/OrderBy/Skip/Take/Page/Count/FirstOrDefault/ToList` 全鏈式串流呼叫。 |
| 導覽屬性（v12.1 新增） | `HasOne`/`HasMany` Fluent API 設定導覽關聯，`Include` 路徑建構 + 批次載入，`SelectBuilder` 支援導覽關聯 JOIN。 |
| `WhereIf` 內嵌條件 | `Query.WhereIf(condition, predicate)` 與 `FindAllWhereIf` 實作動態條件篩選。 |
| `WhereExpression` | 鏈式 `&` / `\|` / `Contains` / `Between` / `Like` 等運算式組合。 |
| `Find` / `FindAll` / `Query` / `PageFindAll` | 查詢入口，`PageFindAll` 內建分頁。 |
| `Pager` / `PageParameter` | 攜帶分頁、排序、統計 `TotalCount`、資料權限擴充。 |
| 視窗函式（v12.0 新增） | `SelectBuilder.Window` + `WindowFunction` 支援 ROW_NUMBER/RANK/DENSE_RANK 及彙總視窗函式。 |

</details>

<details>
<summary><b>多層快取體系</b>（6 項）</summary>

針對「讀多寫少」模式設計：

| 快取 | 主要特點 |
| --- | --- |
| `Meta.Count` | 快速資料列計數快取（>1000 時走系統索引；SQLite 有 Max(自動遞增) + 非同步精確補償）。 |
| `EntityCache`（`Meta.Cache.Entities`） | 整表快取（小表 <1000 列推薦，>10000 列禁止）。 |
| `SingleCache`（`Meta.SingleCache[key]`） | 主鍵/替代鍵字典級單物件快取，適合頻繁單點查詢使用者等。 |
| 過期策略 | 預設 60s。過期存取立即傳回舊資料 + 非同步重新整理。寫入操作會立即使快取失效 / 更新。 |
| 交易感知 | 交易中累積修改，認可/回復後統一清除或回復快取。 |
| 第二層快取（v12.0 新增） | `CacheInvalidator.Provider` 注入 Redis，基於版本號機制跨處理序協調實體快取失效。 |

</details>

<details>
<summary><b>反向工程與移轉</b>（3 項）</summary>

| 能力 | 主要特點 |
| --- | --- |
| 資料表結構反向工程 | 依實體反向建立/修改資料表結構、索引，自動維持一致。 |
| 實體級移轉策略 | 支援資料表級 `Migration` 設定（模型 `Table` 屬性 / `[BindTable]` / `TableItem` 啟動覆寫），可依實體粒度控制反向工程策略（Off / ReadOnly / On / Full）。 |
| 模型擷取與跨庫移轉 | 支援任意資料庫間模型擷取 + 跨庫資料移轉（配套工具 DataMigration）。 |

</details>

<details>
<summary><b>分表分庫（ShardPolicy）</b>（3 項）</summary>

| 能力 | 主要特點 |
| --- | --- |
| `ShardPolicy` 抽象 | `TimeShardPolicy` 範例：依日期產生資料表名稱 Table_yyyyMMdd。 |
| 透明路由 | 業務層仍呼叫 Find / Insert / Update，不關心實際資料表名稱。 |
| 自訂策略 | 可自訂主鍵雜湊 / 時間 / 範圍策略。 |

</details>

<details>
<summary><b>交易與快取一致性</b>（3 項）</summary>

| 情境 | 主要特點 |
| --- | --- |
| 非交易寫入 | 寫入操作立即讓相關快取過期並非同步重建，同時嘗試直接更新記憶體物件以維持讀寫一致。 |
| 交易認可 | 交易中暫不清除整表快取，僅對命中物件做局部變更；認可後統一清除或重新整理。 |
| 交易回復 | 回復時復原交易內所做變更。 |

</details>

<details>
<summary><b>效能最佳化手段</b>（6 項）</summary>

| 手段 | 主要特點 |
| --- | --- |
| 多層快取 | 避免熱點查詢落庫。 |
| 批次寫入 | 合併多欄位與多筆記錄操作（內部最佳化）。 |
| 分頁演算法 | 針對不同 DB / 版本選擇最佳策略。 |
| 延遲/非同步 | 資料列數、模型檢查、快取重新整理皆可非同步。 |
| 反射快取 / 委派加速 | 實體屬性存取子預熱。 |
| 記憶體關聯 | 先快取主表，再於記憶體中做「偽聯結」。 |

</details>

<details>
<summary><b>資料權限與多租用戶</b>（5 項）</summary>

| 能力 | 主要特點 |
| --- | --- |
| 資料權限（Data Scope） | 資料列級資料存取控制，支援「全部 / 本部門及下級 / 本部門 / 僅本人 / 自訂」五種範圍。 |
| 權限接線 | `DataScopeContext` + `DataScopeInterceptor` 自動完成查詢篩選、新增預設歸屬、修改刪除驗證。 |
| 實體接入 | 實體實作 `IUserScope` / `IDepartmentScope` / `IDataScope` 即可接入；部門管理者自動納入可見範圍，本人資料始終可存取。 |
| 多租用戶 | 實體實作 `ITenantScope` 後，`TenantInterceptor` 自動在查詢時附加租用戶篩選、寫入時驗證/補充租用戶識別碼；EntityBuilder 為含 `TenantId` 欄位的業務類別自動實作 `ITenantScope`。 |
| 詳細用法 | 見 `Doc/数据权限.md`、`Doc/权限管理.md`。 |

</details>

<details>
<summary><b>多資料庫設定</b>（4 項）</summary>

| 能力 | 主要特點 |
| --- | --- |
| 預設 SQLite | 開發環境預設使用 SQLite（零設定）。 |
| 連線設定 | 發行時於設定檔或環境變數設定 `XCode_{ConnName}`。 |
| Provider 識別碼 | 連線字串需包含 provider 識別碼（例如：`provider=MySql;server=...;user=...;password=...;database=...;`）。 |
| 多連線共存 | 支援在同一處理序中混用多個資料庫連線名稱（ConnName）。 |

</details>

### 用法範例

#### 動態條件 / 分頁

```csharp
public static IList<UserInfo> Search(String name, Int32 age, Pager p)
{
    var exp = new WhereExpression();
    if (!name.IsNullOrEmpty()) exp &= UserInfo._.UserName == name;
    if (age > 0) exp &= UserInfo._.Age == age;
    return UserInfo.FindAll(exp, p);
}
```

#### 指定更新欄位（繞過快取，不建議常規使用）

```csharp
UserInfo.Update(UserInfo._.UserName == "李四" & UserInfo._.Age == 18, UserInfo._.Id == 1);
```

#### 自訂排序與欄位投影

```csharp
var list = UserInfo.FindAll(UserInfo._.Age > 10, " Age DESC,Id ASC",
    string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 20);
```

#### 索引 / 自動方法

在模型加入索引：

```xml
<Indexes>
  <Index Columns="UserName" Unique="True" />
  <Index Columns="UserName,Age" />
</Indexes>
```

重新產生後自動出現：`FindByUserName`、組合條件輔助方法 / Search 範本。

實體快取策略：`Meta.Session.Count < 1000` 使用記憶體集合查找，否則走資料庫或單物件快取。

```csharp
public static UserInfo FindByUserName(String userName)
{
    if (userName.IsNullOrEmpty()) return null;
    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserName.EqualIgnoreCase(userName));
    return Find(_.UserName == userName);
}
```

#### 分表分庫範例

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(UserInfo.Id), Meta.Factory)
{
    ConnPolicy = "{0}",           // 可自訂資料庫名稱策略
    TablePolicy = "{0}_{1:yyyyMMdd}", // 資料表名稱範本
    Step = TimeSpan.FromDays(1)
};
```

其餘 Insert / Query 等程式碼保持不變，會自動路由到對應日期資料表。

#### 原始範例（保留）

<details>
<summary><b>原始增刪改查與複雜查詢範例</b></summary>

##### 增刪改查

```csharp
var user = new UserInfo();
user.UserName = "張三";
user.Password = "123456";
user.Age = 18;
user.Insert();
Console.WriteLine("插入一筆新資料，使用者 ID 為：" + user.Id);

var u = UserInfo.FindById(1);
u.UserName = "張三";
u.Password = "123456";
u.Age = 19;
u.Update();

u = UserInfo.FindById(1);
Console.WriteLine($"使用者 ID={u.Id} 已修改歲數，歲數為：{u.Age}");
```

##### 複雜查詢

```csharp
var list = UserInfo.FindAll(UserInfo._.UserName == "張三" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
```

</details>

### 實戰指標

| 能力 | 指標（典型環境） | 說明 |
| ---- | ---------------- | ---- |
| 單機 Insert | 最高 ~89 萬 TPS | 批次 / 快取 + SQLite / 記憶體磁碟組合測試 |
| 單機 Query | 最高 ~18 億 QPS | 小表 + 實體快取 / 單物件快取情境 |
| 單表資料量 | 生產百億級 | 分頁 + 分表策略 |

> 實際效能取決於資料庫類型 / 硬體 / 分表策略 / 快取命中率。

### 常見問題（FAQ）

- 為什麼第一次 Count 很快？`Meta.Count` 走系統索引；SQLite 首次可能取自動遞增值再非同步精確計算。
- 資料更新後多久快取一致？本處理序寫入立即反映；跨處理序需等待快取過期或主動失效策略。
- 可否關閉自動移轉？設定 `Migration=Off`（全域或資料表級），或視需要使用 ReadOnly/On/Full。
- 複雜多表查詢？建議多次單表 + 記憶體關聯（快取命中更快 & 結構更清晰）。

## 生態版圖

NewLife 生態依相依性分層，各專案獨立版本、獨立發行。下方版圖在所有 NewLife 儲存庫中逐字一致；每個專案連結到程式碼儲存庫與主 NuGet 套件。

| 專案 | 一句話定位 | 下載量 |
| --- | --- | --- |
| **基礎底座** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 日誌、設定、快取、網路、序列化、排程與 APM 追蹤。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 資料中介層：單表百億級、自動分表、讀寫分離，支援 10 多種資料庫。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 高效能網路庫：單機 177 萬 pps、14.5 Gbps（TCP 1KB，2026-10 實測），400 萬 Tcp 長連線。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 協定通訊與 RPC 框架，面向 CS 應用程式與物聯網裝置。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | 將 .NET 應用程式安裝為作業系統常駐服務：Windows 服務與 Linux systemd。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | `dotnet new` 專案範本：主控台、Web、API、服務、資料與物聯網。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **資料儲存與訊息** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | Redis 用戶端：微秒級延遲、百萬級吞吐，內建訊息佇列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | 純受控 RocketMQ 用戶端，支援 Apache RocketMQ 與阿里雲訊息佇列。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | MQTT 用戶端與服務端，含阿里雲物聯網接入。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 混合式資料庫：內嵌或網路雙模，關係型、時序、訊息佇列與 KV 於一體。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | 建構於 NewLife 資料堆疊之上的 MySql 驅動與輔助層。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **Web、AI 與分散式** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 快速後台開發框架：權限、SSO、OAuth 服務端與 CRUD 樣板。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 分散式服務平台：節點管理、APM、設定中心、註冊中心與發佈中心。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 分散式任務與即時資料排程平台，支援分片、重試與回溯補算。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 統一接入 46 家大模型服務商，附帶開箱即用的 ChatAI Web 對話應用。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **工業與物聯網** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 物聯網標準庫，定義通訊協定契約與裝置抽象。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | 建構於 NewLife.IoT 之上的 Modbus TCP/RTU/ASCII 實作。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | 建構於 NewLife.IoT 之上的西門子 PLC 協定。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | 建構於 NewLife.IoT 之上的歐姆龍 PLC 協定。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | 建構於 NewLife.IoT 之上的三菱 Melsec PLC 協定。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | 工業資料存取的 OPC 用戶端。 | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 樓宇自動化協定堆疊。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 開源輕量級物聯網平台，含閘道與裝置管理。 | — |
| **車聯網** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 車輛定位協定：70+ 訊息體，涵蓋 2011/2013/2019 三個版本。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | JT/T 809 部標平台互聯協定，面向運輸管理部門。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 車載視訊：RTP 解析、FLV/fMP4 轉封裝與直播推送。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **領域元件** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | 一個套件涵蓋 Excel、Word、PPT、PDF 等 15+ 文件格式。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 跨平台音訊：編解碼、裝置 IO、DSP、容器與串流處理。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 視訊編解碼、封裝格式、影格處理與串流。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 百度／高德／騰訊／天地圖統一介面，支援地理編碼與座標轉換。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | IPv4 歸屬地：離線記憶體映射資料庫 + 線上 API 雙來源。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **工具與產品** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 開發者工具箱：網路、序列埠、加解密、正規表示式、Modbus 與 MQTT。 | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 自架 Git 原始碼管理系統。 | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 從較舊的宿主啟動時，自動安裝相符的 .NET 執行階段。 | — |

> 徽章取各專案主套件；主題套件、擴充套件與衛星套件見對應 NuGet 頁面。未在表中列出的 30 多個套件（FTP、LoRa、Office 擴充、各類協定驅動等）見 [NewLifeX 組織](https://github.com/NewLifeX) 與 [newlifex.com](https://newlifex.com)。本表以 NewLife.Core README 為維護來源。

## 文件

- [XCode 文件導覽](Doc/XCode文档导航.md)：依模組分組的完整文件索引
- [XCode 使用手冊](Doc/XCode使用手册.md)：端到端使用流程
- [架構設計](Doc/架构设计.md)：分層、模組元件與關鍵設計決策
- [多語言當地語系化](Doc/多语言本地化.md)：訊息模型、語言表與執行階段覆寫
- 線上文件：https://newlifex.com/xcode
- [ChangeLog.md](ChangeLog.md)：版本記錄

## 參與貢獻

### 建置與測試

```
dotnet restore
dotnet build -c Release
dotnet test XUnitTest.XCode -c Release
```

- 可在 `XUnitTest.XCode` 中補充案例；效能相關建議請附基準測試或說明。
- 請遵循儲存庫慣例（強式命名、多 TFM、XML 註解、條件編譯）。
- 公開 API 變更需提供移轉策略（Obsolete → 移除）。

### 發行

推送帶 `v` 前綴的 tag（例如 `v2026.1018`）以觸發 `.github/workflows/publish.yml`。工作流程會封裝 XCode、XCodeTool 與各資料庫提供者套件（`XCode.MySql`、`XCode.SQLite`、`XCode.SqlServer`、`XCode.Oracle`、`XCode.PostgreSQL`、`XCode.DaMeng`、`XCode.DB2`、`XCode.KingBase`、`XCode.HighGo`），並推送到 NuGet.org 與 GitHub Packages。

### 回饋

- 歡迎提交 Issue / PR：效能最佳化 / 新增資料庫 Provider / 文件改進。
- 提交前請確保 `dotnet build` 與相關單元測試通過；程式碼風格與提交訊息請遵循儲存庫慣例。
- 缺陷與建議請提 [Issue](https://github.com/NewLifeX/NewLife.XCode/issues)，歡迎 Star 與 PR。

## 新生命開發團隊

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

新生命團隊成立於 2002 年，二十餘年專注於 .NET 與物聯網技術生態，主導開源專案近百個（生態版圖收錄 34 個主幹專案），為電力、物流、醫療、工控、網際網路等產業提供軟硬體整合解決方案；以「學無先後，達者為師」為信條。

開源元件在 NuGet 累計下載 <!--nuget-total:start-->6.48M<!--nuget-total:end--> 次（全生態口徑，截至 2026-10）。

網站：https://newlifex.com · 開源組織：https://github.com/NewLifeX

<details>
<summary>微信公眾號：智能大石頭（掃描 QR Code 關注）</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## 授權條款

MIT，完全開放。允許商業使用 / 修改 / 再散布（可將「NewLife」替換為企業品牌以進行私有化部署）。強式命名憑證 `newlife.snk` 已內建。

> 如果本專案對你有幫助，歡迎 Star、分享給團隊，或透過 Issue/PR 回饋改進建議。
