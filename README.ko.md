# NewLife.XCode - 데이터 미들웨어 / 슈퍼 ORM

[![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)](https://github.com/NewLifeX/newlife.xcode)


![GitHub License](https://img.shields.io/github/license/NewLifeX/NewLife.XCode?logo=github)
![NuGet Downloads](https://img.shields.io/nuget/dt/NewLife.XCode?logo=nuget)
![NuGet](https://img.shields.io/nuget/v/NewLife.XCode?logo=nuget)
![NuGet prerelease](https://img.shields.io/nuget/vpre/NewLife.XCode?label=dev%20nuget&logo=nuget)
[![test](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml/badge.svg?branch=master)](https://github.com/NewLifeX/NewLife.XCode/actions/workflows/test.yml)
![platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux%20%7C%20macOS%20%7C%20Android%20%7C%20iOS-blue)
![arch](https://img.shields.io/badge/arch-x64%20%7C%20arm64%20%7C%20loongarch64%20%7C%20riscv64%20%7C%20s390x-orange)
[![contributors](https://img.shields.io/github/contributors/NewLifeX/NewLife.XCode?logo=github)](https://github.com/NewLifeX/NewLife.XCode/graphs/contributors)

<!-- lang:start -->
🌐 [中文](Readme.MD) | [繁體中文](README.zh-TW.md) | [English](README.en.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [العربية](README.ar.md)
<!-- lang:end -->

고성능 .NET 데이터 미들웨어(슈퍼 ORM). CRUD + 극한 성능 + 대규모 데이터에 특화. 다중 레벨 캐싱, 자동 마이그레이션, 샤딩, 강타입 쿼리, 크로스 데이터베이스 마이그레이션 내장. MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / Network 가상 데이터베이스 / NovaDb 지원.


> ⚠️ 이 문서는 간략 번역본이며 업데이트가 지연될 수 있습니다. 최신 내용은 [중국어 README](Readme.MD)를 기준으로 합니다.

## v12.0 하이라이트 (2026-07-02)

- **IQueryable LINQ**: `Entity<T>.Query`가 `IQueryable<T>`를 반환, Where/OrderBy/Skip/Take/Count/First/ToList 완전 지원
- **WhereIf**: 조건부 필터링을 위한 `WhereIf(condition, predicate)` 및 `FindAllWhereIf`
- **윈도우 함수**: `SelectBuilder.Window` + `WindowFunction`으로 ROW_NUMBER/RANK/DENSE_RANK/집계 윈도우
- **2차 캐시**: Redis 기반 프로세스 간 캐시 무효화를 위한 `CacheInvalidator.Provider`
- **신규 프로바이더**: ClickHouse, DuckDB, MongoDB
- **Include**: 관련 엔티티 캐시 사전 로드를 위한 `Include(Type)`

문서: https://newlifex.com/xcode | QQ: 1600800 / 1600838 | 라이선스: MIT
