# NewLife.XCode - Middleware de Datos / Super ORM

[![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)](https://github.com/NewLifeX/newlife.xcode)
[![GitHub License](https://img.shields.io/github/license/newlifex/newlife.xcode?logo=github)](https://github.com/NewLifeX/newlife.xcode/blob/master/LICENSE)
[![Nuget Downloads](https://img.shields.io/nuget/dt/newlife.xcode?logo=nuget)](https://www.nuget.org/packages/NewLife.XCode)
[![Nuget](https://img.shields.io/nuget/v/newlife.xcode?logo=nuget)](https://www.nuget.org/packages/NewLife.XCode)

Middleware de datos .NET de alto rendimiento (Super ORM), enfocado en CRUD + rendimiento extremo + datos masivos. Caché multinivel integrado, migración automática, fragmentación, consultas fuertemente tipadas, migración entre bases de datos. Compatible con MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB.

[中文](Readme.MD) | [English](README.en.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md)

## Novedades v12.0 (2026-07-02)

- **IQueryable LINQ**: `Entity<T>.Query` devuelve `IQueryable<T>` con soporte completo Where/OrderBy/Skip/Take/Count/First/ToList
- **WhereIf**: Filtrado condicional con `WhereIf(condition, predicate)` y `FindAllWhereIf`
- **Funciones de ventana**: `SelectBuilder.Window` + `WindowFunction` para ROW_NUMBER/RANK/DENSE_RANK/agregación
- **Caché secundario**: `CacheInvalidator.Provider` para invalidación de caché entre procesos vía Redis
- **Nuevos proveedores**: ClickHouse, DuckDB, MongoDB
- **Include**: `Include(Type)` para precarga de cachés de entidades relacionadas

Documentación: https://newlifex.com/xcode | QQ: 1600800 / 1600838 | Licencia: MIT
