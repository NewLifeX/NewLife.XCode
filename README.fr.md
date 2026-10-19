# NewLife.XCode - Middleware de Données / Super ORM

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
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

Middleware de données .NET haute performance (Super ORM), axé sur CRUD + performance extrême + données massives. Cache multi-niveaux intégré, migration automatique, partitionnement, requêtes fortement typées, migration inter-bases. Compatible avec MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / base de données virtuelle Network / NovaDb.


> ⚠️ Cette traduction est condensée et peut être en retard ; le [README chinois](Readme.MD) fait foi.

## Nouveautés v12.0 (2026-07-02)

- **IQueryable LINQ**: `Entity<T>.Query` retourne `IQueryable<T>` avec prise en charge complète Where/OrderBy/Skip/Take/Count/First/ToList
- **WhereIf**: Filtrage conditionnel avec `WhereIf(condition, predicate)` et `FindAllWhereIf`
- **Fonctions de fenêtrage**: `SelectBuilder.Window` + `WindowFunction` pour ROW_NUMBER/RANK/DENSE_RANK/agrégation
- **Cache secondaire**: `CacheInvalidator.Provider` pour invalidation de cache inter-processus via Redis
- **Nouveaux fournisseurs**: ClickHouse, DuckDB, MongoDB
- **Include**: `Include(Type)` pour préchargement des caches d'entités liées

Documentation: https://newlifex.com/xcode | QQ: 1600800 / 1600838 | Licence: MIT
