# NewLife.XCode - Middleware de Données / Super ORM

[![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)](https://github.com/NewLifeX/newlife.xcode)
[![GitHub License](https://img.shields.io/github/license/newlifex/newlife.xcode?logo=github)](https://github.com/NewLifeX/newlife.xcode/blob/master/LICENSE)
[![Nuget Downloads](https://img.shields.io/nuget/dt/newlife.xcode?logo=nuget)](https://www.nuget.org/packages/NewLife.XCode)
[![Nuget](https://img.shields.io/nuget/v/newlife.xcode?logo=nuget)](https://www.nuget.org/packages/NewLife.XCode)

Middleware de données .NET haute performance (Super ORM), axé sur CRUD + performance extrême + données massives. Cache multi-niveaux intégré, migration automatique, partitionnement, requêtes fortement typées, migration inter-bases. Compatible avec MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB.

[中文](Readme.MD) | [English](README.en.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Español](README.es.md)

## Nouveautés v12.0 (2026-07-02)

- **IQueryable LINQ**: `Entity<T>.Query` retourne `IQueryable<T>` avec prise en charge complète Where/OrderBy/Skip/Take/Count/First/ToList
- **WhereIf**: Filtrage conditionnel avec `WhereIf(condition, predicate)` et `FindAllWhereIf`
- **Fonctions de fenêtrage**: `SelectBuilder.Window` + `WindowFunction` pour ROW_NUMBER/RANK/DENSE_RANK/agrégation
- **Cache secondaire**: `CacheInvalidator.Provider` pour invalidation de cache inter-processus via Redis
- **Nouveaux fournisseurs**: ClickHouse, DuckDB, MongoDB
- **Include**: `Include(Type)` pour préchargement des caches d'entités liées

Documentation: https://newlifex.com/xcode | QQ: 1600800 / 1600838 | Licence: MIT
