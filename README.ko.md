# NewLife.XCode - 데이터 미들웨어 / 슈퍼 ORM

[![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)](https://github.com/NewLifeX/newlife.xcode)
[![GitHub License](https://img.shields.io/github/license/newlifex/newlife.xcode?logo=github)](https://github.com/NewLifeX/newlife.xcode/blob/master/LICENSE)
[![Nuget Downloads](https://img.shields.io/nuget/dt/newlife.xcode?logo=nuget)](https://www.nuget.org/packages/NewLife.XCode)
[![Nuget](https://img.shields.io/nuget/v/newlife.xcode?logo=nuget)](https://www.nuget.org/packages/NewLife.XCode)

고성능 .NET 데이터 미들웨어(슈퍼 ORM). CRUD + 극한 성능 + 대규모 데이터에 특화. 다중 레벨 캐싱, 자동 마이그레이션, 샤딩, 강타입 쿼리, 크로스 데이터베이스 마이그레이션 내장. MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB 지원.

[中文](Readme.MD) | [English](README.en.md) | [日本語](README.ja.md) | [Español](README.es.md) | [Français](README.fr.md)

## v12.0 하이라이트 (2026-07-02)

- **IQueryable LINQ**: `Entity<T>.Query`가 `IQueryable<T>`를 반환, Where/OrderBy/Skip/Take/Count/First/ToList 완전 지원
- **WhereIf**: 조건부 필터링을 위한 `WhereIf(condition, predicate)` 및 `FindAllWhereIf`
- **윈도우 함수**: `SelectBuilder.Window` + `WindowFunction`으로 ROW_NUMBER/RANK/DENSE_RANK/집계 윈도우
- **2차 캐시**: Redis 기반 프로세스 간 캐시 무효화를 위한 `CacheInvalidator.Provider`
- **신규 프로바이더**: ClickHouse, DuckDB, MongoDB
- **Include**: 관련 엔티티 캐시 사전 로드를 위한 `Include(Type)`

문서: https://newlifex.com/xcode | QQ: 1600800 / 1600838 | 라이선스: MIT
