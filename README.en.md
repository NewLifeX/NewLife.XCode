# NewLife.XCode - Data Middleware / Super ORM

[![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)](https://github.com/NewLifeX/newlife.xcode)
[![GitHub License](https://img.shields.io/github/license/newlifex/newlife.xcode?logo=github)](https://github.com/NewLifeX/newlife.xcode/blob/master/LICENSE)
[![Nuget Downloads](https://img.shields.io/nuget/dt/newlife.xcode?logo=nuget)](https://www.nuget.org/packages/NewLife.XCode)
[![Nuget](https://img.shields.io/nuget/v/newlife.xcode?logo=nuget)](https://www.nuget.org/packages/NewLife.XCode)

High-performance .NET data middleware (Super ORM), focusing on CRUD + extreme performance + massive data. Built-in multi-level caching, auto migration, sharding, strongly-typed queries, cross-database migration. Supports MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB.

[中文](Readme.MD) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Español](README.es.md) | [Français](README.fr.md)

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
