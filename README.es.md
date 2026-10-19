# NewLife.XCode - Middleware de Datos / Super ORM

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
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [한국어](README.ko.md) | [العربية](README.ar.md)
<!-- lang:end -->

Middleware de datos .NET de alto rendimiento (Super ORM), enfocado en CRUD + rendimiento extremo + datos masivos. Caché multinivel integrado, migración automática, fragmentación, consultas fuertemente tipadas, migración entre bases de datos. Compatible con MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / base de datos virtual Network / NovaDb.


> ⚠️ Esta es una traducción resumida y puede quedar desactualizada; el [README en chino](Readme.MD) es la fuente autoritativa.

## Novedades v12.0 (2026-07-02)

- **IQueryable LINQ**: `Entity<T>.Query` devuelve `IQueryable<T>` con soporte completo Where/OrderBy/Skip/Take/Count/First/ToList
- **WhereIf**: Filtrado condicional con `WhereIf(condition, predicate)` y `FindAllWhereIf`
- **Funciones de ventana**: `SelectBuilder.Window` + `WindowFunction` para ROW_NUMBER/RANK/DENSE_RANK/agregación
- **Caché secundario**: `CacheInvalidator.Provider` para invalidación de caché entre procesos vía Redis
- **Nuevos proveedores**: ClickHouse, DuckDB, MongoDB
- **Include**: `Include(Type)` para precarga de cachés de entidades relacionadas

Documentación: https://newlifex.com/xcode | QQ: 1600800 / 1600838 | Licencia: MIT
