# NewLife.XCode - データミドルウェア / スーパーORM

[![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)](https://github.com/NewLifeX/newlife.xcode)


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

高性能 .NET データミドルウェア（スーパーORM）。CRUD + 極限パフォーマンス + 大規模データに特化。マルチレベルキャッシュ、自動マイグレーション、シャーディング、型付きクエリ、クロスデータベース移行を内蔵。MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / Network 仮想データベース / NovaDb に対応。


> ⚠️ 本翻訳は更新が遅れる場合があります。正式な内容は[英語 README](README.md)を参照してください。

## v12.0 ハイライト (2026-07-02)

- **IQueryable LINQ**: `Entity<T>.Query` が `IQueryable<T>` を返し、Where/OrderBy/Skip/Take/Count/First/ToList を完全サポート
- **WhereIf**: `WhereIf(condition, predicate)` と `FindAllWhereIf` による条件付きフィルタリング
- **ウィンドウ関数**: `SelectBuilder.Window` + `WindowFunction` で ROW_NUMBER/RANK/DENSE_RANK/集計ウィンドウ
- **二次キャッシュ**: `CacheInvalidator.Provider` で Redis によるプロセス間キャッシュ無効化
- **新規プロバイダ**: ClickHouse、DuckDB、MongoDB
- **Include**: `Include(Type)` で関連エンティティキャッシュの事前読込

ドキュメント: https://newlifex.com/xcode | QQ: 1600800 / 1600838 | ライセンス: MIT
