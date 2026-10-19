# NewLife.XCode - Data Middleware / Super ORM

[![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)](https://github.com/NewLifeX/newlife.xcode)


![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.XCode?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.XCode?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.XCode?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.XCode?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.XCode?logo=github)](https://github.com/NewLifeX/NewLife.XCode/graphs/contributors)

<!-- lang:start -->
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

High-performance .NET data middleware (Super ORM), focusing on CRUD + extreme performance + massive data. Built-in multi-level caching, auto migration, sharding, strongly-typed queries, cross-database migration. Supports MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / Network virtual database / NovaDb.


> ⚠️ This is a condensed translation that may lag behind the [Chinese README](Readme.MD), which is the authoritative source.

## v12.0 Highlights (2026-07-02)

- **IQueryable LINQ**: `Entity<T>.Query` returns `IQueryable<T>` with full Where/OrderBy/Skip/Take/Count/First/ToList support
- **WhereIf**: Conditional filtering with `WhereIf(condition, predicate)` and `FindAllWhereIf`
- **Window Functions**: `SelectBuilder.Window` + `WindowFunction` for ROW_NUMBER/RANK/DENSE_RANK/aggregate windows
- **Secondary Cache**: `CacheInvalidator.Provider` for Redis-based cross-process cache invalidation
- **New Providers**: ClickHouse, DuckDB, MongoDB
- **Include**: `Include(Type)` for eager-loading related entity caches

## Quick Start

```bash
dotnet add package NewLife.XCode
```

```csharp
// LINQ query
var list = User.Query.Where(u => u.Enable).OrderByDescending(u => u.Id).Skip(10).Take(20).ToList();

// Dynamic WhereIf
var list = User.Query
    .WhereIf(!name.IsNullOrEmpty(), u => u.Name.Contains(name))
    .WhereIf(age > 0, u => u.Age == age)
    .ToList();

// Window function
var sb = new SelectBuilder { Table = "User" };
sb.Window = WindowFunction.RowNumber("Id DESC");

// Distributed cache
CacheInvalidator.Provider = new FullRedis("server=127.0.0.1:6379;db=0");
```

Docs: https://newlifex.com/xcode | QQ: 1600800 / 1600838 | License: MIT
