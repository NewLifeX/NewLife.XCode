# NewLife.XCode - データミドルウェア / スーパーORM

[![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)](https://github.com/NewLifeX/newlife.xcode)
[![GitHub License](https://img.shields.io/github/license/newlifex/newlife.xcode?logo=github)](https://github.com/NewLifeX/newlife.xcode/blob/master/LICENSE)
[![Nuget Downloads](https://img.shields.io/nuget/dt/newlife.xcode?logo=nuget)](https://www.nuget.org/packages/NewLife.XCode)
[![Nuget](https://img.shields.io/nuget/v/newlife.xcode?logo=nuget)](https://www.nuget.org/packages/NewLife.XCode)

高性能 .NET データミドルウェア（スーパーORM）。CRUD + 極限パフォーマンス + 大規模データに特化。マルチレベルキャッシュ、自動マイグレーション、シャーディング、型付きクエリ、クロスデータベース移行を内蔵。MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB に対応。

[中文](Readme.MD) | [English](README.en.md) | [한국어](README.ko.md) | [Español](README.es.md) | [Français](README.fr.md)

## v12.0 ハイライト (2026-07-02)

- **IQueryable LINQ**: `Entity<T>.Query` が `IQueryable<T>` を返し、Where/OrderBy/Skip/Take/Count/First/ToList を完全サポート
- **WhereIf**: `WhereIf(condition, predicate)` と `FindAllWhereIf` による条件付きフィルタリング
- **ウィンドウ関数**: `SelectBuilder.Window` + `WindowFunction` で ROW_NUMBER/RANK/DENSE_RANK/集計ウィンドウ
- **二次キャッシュ**: `CacheInvalidator.Provider` で Redis によるプロセス間キャッシュ無効化
- **新規プロバイダ**: ClickHouse、DuckDB、MongoDB
- **Include**: `Include(Type)` で関連エンティティキャッシュの事前読込

ドキュメント: https://newlifex.com/xcode | QQ: 1600800 / 1600838 | ライセンス: MIT
