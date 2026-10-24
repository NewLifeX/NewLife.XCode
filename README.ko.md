# NewLife.XCode - 데이터 미들웨어 / 슈퍼 ORM

![GitHub top language](https://img.shields.io/github/languages/top/newlifex/newlife.xcode?logo=github)

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
🌐 [English](README.md) | [简体中文](README.zh-CN.md) | [繁體中文](README.zh-TW.md) | [Español](README.es.md) | [Deutsch](README.de.md) | [日本語](README.ja.md) | [Français](README.fr.md) | [Português](README.pt.md) | [Русский](README.ru.md) | [العربية](README.ar.md)
<!-- lang:end -->

> ⚠️ 이 번역은 구조를 포함하여 오래되었을 수 있으며, 기준 문서는 [영어 README](README.md)입니다.

**NewLife.XCode**는 "CRUD + 극한 성능 + 대규모 데이터"에 집중한 고성능 .NET 데이터 미들웨어입니다. 다중 레벨 캐싱, 자동 모델링/마이그레이션, 샤딩, 강타입 쿼리 및 데이터베이스 간 마이그레이션을 포함합니다. MySQL / SQLite / SqlServer / Oracle / PostgreSQL / TDengine / InfluxDB / DaMeng / KingBase / HighGo / DB2 / ClickHouse / DuckDB / MongoDB / Hana (SAP HANA) / IRIS (InterSystems) / VastBase / Network virtual database / NovaDb / SonnetDB 등을 지원합니다. 운영 환경에서는 테이블당 수백억 행에 이르며, 쿼리 처리량은 (캐시 전략에 따라) 수십억 QPS에 달할 수 있습니다.

문서: https://newlifex.com/xcode   커뮤니티 QQ 그룹: 1600800 / 1600838

**목차**: [빠른 시작](#빠른-시작) · [XCode를 선택하는 이유](#xcode를-선택하는-이유) · [주요 기능](#주요-기능) · [생태계 지도](#생태계-지도) · [문서](#문서) · [기여](#기여) · [팀](#팀) · [라이선스](#라이선스)

## 빠른 시작

### 설치

NuGet 참조(모든 프로젝트):

```
dotnet add package NewLife.XCode
```

또는 템플릿 스캐폴드 사용:

```
dotnet new install NewLife.Templates
dotnet new xcode --name Demo.Data
cd Demo.Data
dotnet build
```

> 강력한 이름(strong-name) 인증서가 내장되어 있습니다(newlife.snk). 기업 전용 프레임워크로 재컴파일할 수 있습니다.

### 모델 정의(db.xml)

```xml
<?xml version="1.0" encoding="utf-8"?>
<Tables Version="1.0" Output="MyEntity" NameSpace="Model" BaseClass="Entity" ConnName="DB">
  <Table Name="UserInfo" Description="사용자 정보">
    <Columns>
      <Column Name="Id" DataType="Int32" Identity="True" PrimaryKey="True" Description="회원 ID" />
      <Column Name="UserName" DataType="String" Nullable="False" Description="회원 이름" />
      <Column Name="Password" DataType="String" Nullable="False" Description="회원 비밀번호" />
      <Column Name="Age" DataType="Int32" Description="회원 나이" />
    </Columns>
  </Table>
</Tables>
```

### 엔티티 클래스 생성

- xcodetool.exe를 해당 디렉터리에 내려받아 더블 클릭하거나, 다음을 실행합니다: `dotnet tool install xcodetool -g --prerelease` → `xcode db.xml`.
- 생성 파일: 用户信息.cs(데이터 클래스, 덮어쓰기됨) + 用户信息.Biz.cs(비즈니스 클래스, 덮어쓰지 않음).

### CRUD

```csharp
using XCode;

var user = new UserInfo { UserName = "홍길동", Password = "123456", Age = 18 };
user.Insert();
Console.WriteLine("새 레코드가 삽입되었습니다. 사용자 ID: " + user.Id);

var u2 = UserInfo.FindById(user.Id);
u2.Age = 19;
u2.Update();

var list = UserInfo.FindAll(UserInfo._.Age > 10 & UserInfo._.UserName == "홍길동");
UserInfo.FindById(user.Id)?.Delete();
```

> 캐시를 사용할 때: 엔티티가 1000개 미만이면 전체 테이블 캐시, 그보다 많으면 일반 쿼리 / 단일 객체 캐시를 사용합니다.

## XCode를 선택하는 이유

- **자체 개발 및 독창성**: 서드파티 컴포넌트 없이 중국에서 완전히 자체 개발했으며, 직접 통제하고 장기 유지보수가 보장됩니다.
- **단순성**: 비즈니스 시나리오의 90%는 수작업 SQL 없이 단일 테이블 엔티티 작업만으로 충분합니다.
- **Dapper 호환**: 임의의 SQL 쿼리를 실행해 어떤 엔티티 모델에도 매핑할 수 있습니다. Dapper 사용 방식과 완전히 호환되면서 캐싱, 읽기/쓰기 분리 및 추적이 추가됩니다.
- **극한 성능**: 핵심 경로의 다중 레벨 캐시 + 배치 최적화 + 인메모리 조인으로, 쿼리 속도가 원시 SQL보다 훨씬 빠를 수 있습니다.
- **대규모 데이터**: 네이티브 페이징 및 샤딩 전략을 제공하며, 운영 프로젝트에서 테이블당 수백억 행에 도달합니다. 페이징 알고리즘은 여러 데이터베이스에 맞게 적응합니다.
- **안정성과 성숙도**: 20여 년의 발전과 폭넓은 운영 검증을 거쳤으며, 오래된 .NET Framework 버전(net45부터)과의 호환성을 유지합니다.
- **자동 마이그레이션**: 모델/엔티티가 곧 스키마이며, 배포 시 새 필드가 자동으로 동기화되어 별도의 SQL 스크립트가 필요 없습니다.
- **분산 진화**: 테이블 수준 / 시간 / 기본 키 샤딩 전략을 기본 제공하여 원활하게 분할하고 확장할 수 있습니다.
- **플러그형**: DAL 추상화 + 분리된 프로바이더 구조이며, 사용자 정의 데이터베이스 어댑터를 지원합니다.

## 주요 기능

<details>
<summary><b>모델링 및 코드 생성</b> (5)</summary>

| 기능 | 주요 특징 |
| --- | --- |
| XML 모델(Tables/Columns/Indexes) | 모델이 유일한 진실의 원천이며, 속성은 인덱스, 고유 제약, 기본값, 설명 및 확장 속성을 포괄합니다. |
| 코드 생성기 | `xcodetool`(전역 도구 / exe) 또는 VS T4 템플릿이 XML 모델을 엔티티 클래스로 변환합니다. |
| 데이터 클래스(`.cs`) | 생성할 때마다 덮어써지며, 필드와 기본 쿼리를 담습니다. |
| 비즈니스 클래스(`.Biz.cs`) | 최초 생성 후에는 덮어쓰지 않으며, 사용자 정의 로직을 여기에 둡니다. |
| 자동 생성 메서드 | 인덱스와 컬럼에서 `FindByXxx` / `Search` 헬퍼 메서드와 템플릿이 자동으로 생성됩니다. |

</details>

<details>
<summary><b>엔티티 런타임 및 DAL</b> (7)</summary>

| 구성 요소 | 역할 |
| --- | --- |
| `IEntity` / `Entity` | 필드, 유효성 검사, 수명 주기를 캡슐화한 풍부한 도메인 모델입니다. |
| `IEntityOperate` | 정적 작업 진입점(Find / FindAll / Query 등)입니다. |
| `EntityFactory` | 엔티티 메타데이터 캐시 팩토리입니다. |
| `EntitySession` (`Meta.Session`) | 특정 연결/테이블에서 엔티티의 컨텍스트로, 세션 수준 캐시/통계를 제공합니다. |
| `Meta` | 엔티티 정적 컨텍스트(테이블 스키마, 필드, 캐시, 샤딩 정책 등)입니다. |
| `DAL` / `IDatabase` / `IDbSession` | 통합 데이터베이스 액세스 및 메타데이터 작업입니다. |
| `CheckTables()` | 시작 시/최초 액세스 시 스키마를 검사하고 동기화하며, 필요에 따라 비동기 또는 동기로 실행합니다. |

</details>

<details>
<summary><b>쿼리 DSL</b> (8)</summary>

| 기능 | 주요 특징 |
| --- | --- |
| `IQueryable<T>` LINQ(v12.0) | `Entity<T>.Query`가 `IQueryable<T>`를 반환하여 `Where/OrderBy/Skip/Take/Count/First/ToList` 등 표준 LINQ 작업을 지원합니다. |
| `DAL.Select<T>()` 플루언트 쿼리(v12.1) | `DAL.Select<T>()`가 현재 연결에 바인딩된 `IQueryable<T>`를 반환하며, `WhereIf/Where/OrderBy/Skip/Take/Page/Count/FirstOrDefault/ToList` 전체 체인 호출을 지원합니다. |
| 탐색 속성(v12.1) | `HasOne`/`HasMany` Fluent API로 탐색 관계를 구성하고, `Include`로 경로를 구성해 일괄 로드하며, `SelectBuilder`는 탐색 관계를 통한 JOIN을 지원합니다. |
| `WhereIf` 인라인 조건 | `Query.WhereIf(condition, predicate)`와 `FindAllWhereIf`로 동적 필터링을 구현합니다. |
| `WhereExpression` | 체이닝 가능한 `&` / `\|` / `Contains` / `Between` / `Like` 표현식 조합입니다. |
| `Find` / `FindAll` / `Query` / `PageFindAll` | 쿼리 진입점이며, `PageFindAll`에는 페이징이 내장되어 있습니다. |
| `Pager` / `PageParameter` | 페이징, 정렬, `TotalCount` 통계 및 데이터 범위 확장을 담당합니다. |
| 윈도우 함수(v12.0) | `SelectBuilder.Window` + `WindowFunction`이 ROW_NUMBER/RANK/DENSE_RANK 및 집계 윈도우 함수를 지원합니다. |

</details>

<details>
<summary><b>다중 레벨 캐시 시스템</b> (6)</summary>

읽기가 많고 쓰기가 적은 워크로드를 위해 설계되었습니다:

| 캐시 | 주요 특징 |
| --- | --- |
| `Meta.Count` | 빠른 행 수 캐시입니다(1000 이상이면 시스템 인덱스를 사용하고, SQLite는 Max(identity)와 비동기 정밀 보정을 사용). |
| `EntityCache` (`Meta.Cache.Entities`) | 전체 테이블 캐시입니다(1000행 미만의 작은 테이블에 권장하며, 10000행 초과 시 금지). |
| `SingleCache` (`Meta.SingleCache[key]`) | 기본 키/대체 키 기준의 사전 수준 단일 객체 캐시로, 사용자처럼 빈번한 포인트 조회에 적합합니다. |
| 만료 정책 | 기본 60초입니다. 만료된 읽기는 오래된 데이터를 즉시 반환하고 비동기로 새로 고칩니다. 쓰기는 캐시를 즉시 무효화/갱신합니다. |
| 트랜잭션 인식 | 트랜잭션 내부에서는 수정이 누적되며, 커밋/롤백 후 캐시가 일괄적으로 지워지거나 되돌려집니다. |
| 2차 캐시(v12.0) | `CacheInvalidator.Provider`로 Redis를 주입하고, 버전 메커니즘을 통해 프로세스 간 엔티티 캐시 무효화를 조정합니다. |

</details>

<details>
<summary><b>리버스 엔지니어링 및 마이그레이션</b> (3)</summary>

| 기능 | 주요 특징 |
| --- | --- |
| 스키마 리버스 엔지니어링 | 엔티티에서 테이블 스키마와 인덱스를 역으로 생성/수정하며, 자동으로 일관성을 유지합니다. |
| 엔티티별 마이그레이션 정책 | 테이블 수준 `Migration` 구성(모델 `Table` 속성 / `[BindTable]` / `TableItem` 시작 시 재정의)으로 엔티티별 리버스 엔지니어링 전략(Off / ReadOnly / On / Full)을 제어합니다. |
| 모델 추출 및 데이터베이스 간 마이그레이션 | 모든 데이터베이스에서 모델 추출 + 데이터베이스 간 데이터 마이그레이션(DataMigration 동반 도구 사용)을 지원합니다. |

</details>

<details>
<summary><b>샤딩(ShardPolicy)</b> (3)</summary>

| 기능 | 주요 특징 |
| --- | --- |
| `ShardPolicy` 추상화 | `TimeShardPolicy` 예: 날짜별 테이블 이름 Table_yyyyMMdd. |
| 투명한 라우팅 | 비즈니스 코드는 여전히 Find / Insert / Update를 호출하며 실제 테이블 이름은 신경 쓰지 않습니다. |
| 사용자 정의 전략 | 기본 키 해시 / 시간 / 범위 전략을 사용자 정의할 수 있습니다. |

</details>

<details>
<summary><b>트랜잭션 및 캐시 일관성</b> (3)</summary>

| 시나리오 | 주요 특징 |
| --- | --- |
| 비트랜잭션 쓰기 | 쓰기 시 관련 캐시가 즉시 만료되고 비동기로 재구축되며, 읽기/쓰기 일관성을 위해 인메모리 객체를 직접 갱신하려고 시도합니다. |
| 커밋 | 트랜잭션 내부에서는 전체 테이블 캐시를 지우지 않고 적중된 객체만 로컬에서 변경하며, 커밋 후 일괄적으로 지우거나 새로 고칩니다. |
| 롤백 | 롤백 시 트랜잭션 내부의 변경 사항이 되돌려집니다. |

</details>

<details>
<summary><b>성능 최적화</b> (6)</summary>

| 기법 | 주요 특징 |
| --- | --- |
| 다중 레벨 캐싱 | 핫 쿼리가 데이터베이스에 도달하지 않도록 유지합니다. |
| 배치 쓰기 | 다중 필드 및 다중 레코드 작업을 병합합니다(내부 최적화). |
| 페이징 알고리즘 | 데이터베이스/버전별로 최적의 전략을 선택합니다. |
| 지연/비동기 | 행 수, 모델 검사 및 캐시 새로 고침을 모두 비동기로 실행할 수 있습니다. |
| 리플렉션 캐시 / 델리게이트 가속 | 엔티티 속성 접근자를 미리 준비합니다. |
| 인메모리 연관 | 메인 테이블을 먼저 캐시한 뒤 메모리에서 "의사 조인"을 수행합니다. |

</details>

<details>
<summary><b>데이터 범위 및 멀티테넌시</b> (5)</summary>

| 기능 | 주요 특징 |
| --- | --- |
| 데이터 범위 | "전체 / 부서 및 하위 부서 / 부서 / 본인만 / 사용자 정의"의 다섯 범위를 지원하는 행 수준 데이터 액세스 제어입니다. |
| 범위 연결 | `DataScopeContext` + `DataScopeInterceptor`가 쿼리 필터 적용, 삽입 시 기본 소유자 설정, 수정/삭제 시 검증을 자동으로 수행합니다. |
| 엔티티 옵트인 | 엔티티가 `IUserScope` / `IDepartmentScope` / `IDataScope`를 구현하며, 부서 관리자는 자동으로 표시 범위에 포함되고 자신의 데이터는 항상 액세스할 수 있습니다. |
| 멀티테넌시 | 엔티티가 `ITenantScope`를 구현하면 `TenantInterceptor`가 쿼리에 테넌트 필터를 자동 추가하고 쓰기 시 테넌트 식별자를 검증/채우며, EntityBuilder는 `TenantId` 컬럼이 있는 비즈니스 클래스에 `ITenantScope`를 자동으로 구현합니다. |
| 세부 정보 | 자세한 내용은 `Doc/数据权限.md` 및 `Doc/权限管理.md`를 참조하세요. |

</details>

<details>
<summary><b>다중 데이터베이스 구성</b> (4)</summary>

| 기능 | 주요 특징 |
| --- | --- |
| 기본 SQLite | 개발 환경에서는 SQLite가 기본입니다(무설정). |
| 연결 설정 | 배포 시 구성 파일 또는 환경 변수에서 `XCode_{ConnName}`을 설정합니다. |
| 프로바이더 식별자 | 연결 문자열에 프로바이더 식별자가 포함되어야 합니다(예: `provider=MySql;server=...;user=...;******;database=...;`). |
| 다중 연결 | 동일 프로세스에서 여러 데이터베이스 연결 이름(ConnName)을 함께 사용할 수 있습니다. |

</details>

### 사용 예제

#### 동적 조건과 페이징

```csharp
public static IList<UserInfo> Search(String name, Int32 age, Pager p)
{
    var exp = new WhereExpression();
    if (!name.IsNullOrEmpty()) exp &= UserInfo._.UserName == name;
    if (age > 0) exp &= UserInfo._.Age == age;
    return UserInfo.FindAll(exp, p);
}
```

#### 지정 필드 업데이트(캐시 우회, 일반적인 사용에는 권장하지 않음)

```csharp
UserInfo.Update(UserInfo._.UserName == "김철수" & UserInfo._.Age == 18, UserInfo._.Id == 1);
```

#### 사용자 정의 정렬과 컬럼 프로젝션

```csharp
var list = UserInfo.FindAll(UserInfo._.Age > 10, " Age DESC,Id ASC",
    string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 20);
```

#### 인덱스와 생성 메서드

모델에 인덱스 추가:

```xml
<Indexes>
  <Index Columns="UserName" Unique="True" />
  <Index Columns="UserName,Age" />
</Indexes>
```

다시 생성하면 `FindByUserName`, 조합 조건 헬퍼 메서드 / Search 템플릿이 자동으로 나타납니다.

엔티티 캐시 전략: `Meta.Session.Count < 1000`이면 인메모리 컬렉션 조회를 사용하고, 그렇지 않으면 데이터베이스 또는 단일 객체 캐시로 이동합니다.

```csharp
public static UserInfo FindByUserName(String userName)
{
    if (userName.IsNullOrEmpty()) return null;
    if (Meta.Session.Count < 1000) return Meta.Cache.Find(e => e.UserName.EqualIgnoreCase(userName));
    return Find(_.UserName == userName);
}
```

#### 샤딩 예제

```csharp
Meta.ShardPolicy = new TimeShardPolicy(nameof(UserInfo.Id), Meta.Factory)
{
    ConnPolicy = "{0}",           // 사용자 정의 가능한 데이터베이스 이름 정책
    TablePolicy = "{0}_{1:yyyyMMdd}", // 테이블 이름 템플릿
    Step = TimeSpan.FromDays(1)
};
```

Insert / Query 등 나머지 코드는 그대로 유지되며 해당 날짜 테이블로 자동 라우팅됩니다.

#### 원본 예제(유지)

<details>
<summary><b>원본 CRUD 및 복잡한 쿼리 예제</b></summary>

##### 생성, 읽기, 수정, 삭제

```csharp
var user = new UserInfo();
user.UserName = "홍길동";
user.Password = "123456";
user.Age = 18;
user.Insert();
Console.WriteLine("새 레코드가 삽입되었습니다. 사용자 ID: " + user.Id);

var u = UserInfo.FindById(1);
u.UserName = "홍길동";
u.Password = "123456";
u.Age = 19;
u.Update();

u = UserInfo.FindById(1);
Console.WriteLine($"사용자 ID={u.Id} 나이가 다음과 같이 업데이트되었습니다: {u.Age}");
```

##### 복잡한 쿼리

```csharp
var list = UserInfo.FindAll(UserInfo._.UserName == "홍길동" & UserInfo._.Age == 19, UserInfo._.UserName.Desc(), string.Join(",", UserInfo._.UserName, UserInfo._.Age), 0, 0);
```

</details>

### 벤치마크

| 기능 | 지표(일반적인 환경) | 비고 |
| ---- | ---------------- | ---- |
| 단일 머신 Insert | 최대 약 890k TPS | 배치 / 캐시 + SQLite / RAM 디스크 조합 테스트 |
| 단일 머신 Query | 최대 약 18억 QPS | 작은 테이블 + 엔티티 캐시 / 단일 객체 캐시 시나리오 |
| 테이블당 데이터량 | 운영 환경에서 수백억 건 | 페이징 + 샤딩 전략 |

> 실제 성능은 데이터베이스 종류 / 하드웨어 / 샤딩 전략 / 캐시 적중률에 따라 달라집니다.

### FAQ

- 첫 번째 Count는 왜 빠른가요? `Meta.Count`는 시스템 인덱스를 사용하며, SQLite는 먼저 identity 최댓값을 취한 뒤 비동기로 정밀화할 수 있습니다.
- 업데이트 후 캐시는 얼마나 빨리 일관성을 갖나요? 현재 프로세스의 쓰기는 즉시 반영되며, 프로세스 간에는 캐시 만료를 기다리거나 능동적 무효화 전략을 사용하세요.
- 자동 마이그레이션을 비활성화할 수 있나요? `Migration=Off`(전역 또는 테이블별)로 설정하거나, 필요에 따라 ReadOnly/On/Full을 사용하세요.
- 복잡한 다중 테이블 쿼리는요? 여러 단일 테이블 쿼리 + 인메모리 연관을 권장합니다(캐시 적중 시 더 빠르고 구조도 명확합니다).

## 생태계 지도

NewLife는 독립적으로 버전 관리되는 오픈소스 프로젝트의 계층형 생태계입니다. 아래 지도는 모든 NewLife 리포지토리에서 그대로 동일하게 공유되며, 각 프로젝트는 코드 리포지토리와 기본 NuGet 패키지로 연결됩니다.

| 프로젝트 | 한 줄 소개 | 다운로드 |
| --- | --- | --- |
| **기반** | | |
| [NewLife.Core](https://github.com/NewLifeX/X) | 로깅, 구성, 캐싱, 네트워킹, 직렬화, 스케줄링 및 APM 추적. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Core?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Core) |
| [NewLife.XCode](https://github.com/NewLifeX/NewLife.XCode) | 데이터 미들웨어: 테이블당 수백억 건 규모, 자동 분할 테이블, 읽기/쓰기 분리, 10여 종 데이터베이스 지원. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCode?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCode) |
| [NewLife.Net](https://github.com/NewLifeX/NewLife.Net) | 고성능 네트워크 라이브러리: 단일 머신에서 177만 pps / 14.5 Gbps(TCP 1KB, 2026-10), 400만 개 장기 TCP 연결. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Net?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Net) |
| [NewLife.Remoting](https://github.com/NewLifeX/NewLife.Remoting) | 클라이언트/서버 애플리케이션과 IoT 장치를 위한 프로토콜 통신 및 RPC 프레임워크. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Remoting?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Remoting) |
| [NewLife.Agent](https://github.com/NewLifeX/NewLife.Agent) | .NET 애플리케이션을 운영체제 데몬으로 설치: Windows 서비스 및 Linux systemd. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Agent?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Agent) |
| [NewLife.Templates](https://github.com/NewLifeX/NewLife.Zero) | 콘솔, 웹, API, 서비스, 데이터, IoT 프로젝트용 `dotnet new` 프로젝트 템플릿. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Templates?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Templates) |
| **데이터 및 메시징** | | |
| [NewLife.Redis](https://github.com/NewLifeX/NewLife.Redis) | 마이크로초 지연, 초당 수백만 작업, 내장 메시지 큐를 갖춘 Redis 클라이언트. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Redis?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Redis) |
| [NewLife.RocketMQ](https://github.com/NewLifeX/NewLife.RocketMQ) | Apache RocketMQ와 알리바바 클라우드 메시지 큐를 위한 순수 관리형 RocketMQ 클라이언트. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.RocketMQ?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.RocketMQ) |
| [NewLife.MQTT](https://github.com/NewLifeX/NewLife.MQTT) | 알리바바 클라우드 IoT 연결을 포함한 MQTT 클라이언트 및 서버. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MQTT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MQTT) |
| [NewLife.NovaDb](https://github.com/NewLifeX/NewLife.NovaDb) | C# 기반 하이브리드 데이터베이스: 임베디드 또는 네트워크 방식, 관계형, 시계열, 메시지 큐, KV를 하나로. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.NovaDb?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.NovaDb) |
| [NewLife.MySql](https://github.com/NewLifeX/NewLife.MySql) | NewLife 데이터 스택 위에 구축한 MySql 드라이버와 도우미 계층. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.MySql?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.MySql) |
| **웹, AI 및 분산** | | |
| [NewLife.Cube](https://github.com/NewLifeX/NewLife.Cube) | 빠른 관리 플랫폼 프레임워크: 권한, SSO, OAuth 서버 및 CRUD 스캐폴딩. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Cube?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Cube) |
| [Stardust](https://github.com/NewLifeX/Stardust) | 분산 서비스 플랫폼: 노드 관리, APM, 구성 센터, 레지스트리 및 릴리스 센터. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Stardust?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Stardust) |
| [AntJob](https://github.com/NewLifeX/AntJob) | 샤딩, 재시도, 백필을 지원하는 분산 작업 및 실시간 데이터 스케줄링 플랫폼. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AntJob?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AntJob) |
| [NewLife.AI](https://github.com/NewLifeX/NewLife.AI) | 46개 LLM 프로바이더를 위한 통합 클라이언트와 즉시 실행 가능한 웹 채팅 애플리케이션 ChatAI. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.AI?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.AI) |
| **산업 및 IoT** | | |
| [NewLife.IoT](https://github.com/NewLifeX/NewLife.IoT) | 프로토콜 계약과 장치 추상을 정의하는 IoT 표준 라이브러리. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IoT?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IoT) |
| [NewLife.Modbus](https://github.com/NewLifeX/NewLife.Modbus) | NewLife.IoT 기반으로 구현한 Modbus TCP/RTU/ASCII. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Modbus?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Modbus) |
| [NewLife.Siemens](https://github.com/NewLifeX/NewLife.Siemens) | NewLife.IoT 기반으로 구현한 지멘스 PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Siemens?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Siemens) |
| [NewLife.Omron](https://github.com/NewLifeX/NewLife.Omron) | NewLife.IoT 기반으로 구현한 오므론 PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Omron?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Omron) |
| [NewLife.Melsec](https://github.com/NewLifeX/NewLife.Melsec) | NewLife.IoT 기반으로 구현한 미쓰비시 Melsec PLC 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Melsec?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Melsec) |
| [NewLife.OPC](https://github.com/NewLifeX/NewLife.OPC) | 산업 데이터 수집용 OPC 클라이언트. | — |
| [NewLife.BACnet](https://github.com/NewLifeX/NewLife.BACnet) | BACnet 빌딩 자동화 프로토콜 스택. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.BACnet?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.BACnet) |
| [ZeroIoT](https://github.com/NewLifeX/ZeroIoT) | 게이트웨이와 장치 관리를 갖춘 경량 오픈소스 IoT 플랫폼. | — |
| **차량 텔레매틱스** | | |
| [NewLife.JT808](https://github.com/NewLifeX/NewLife.JT808) | JT/T 808 차량 위치 프로토콜: 70개 이상의 메시지 본문, 2011/2013/2019. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT808?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT808) |
| [NewLife.JT809](https://github.com/NewLifeX/NewLife.JT809) | 운송 관리 기관을 위한 JT/T 809 플랫폼 간 프로토콜. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT809?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT809) |
| [NewLife.JT1078](https://github.com/NewLifeX/NewLife.JT1078) | JT/T 1078 차량 내 비디오: RTP 파싱, FLV/fMP4 리먹싱 및 라이브 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.JT1078?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.JT1078) |
| **도메인 컴포넌트** | | |
| [NewLife.Office](https://github.com/NewLifeX/NewLife.Office) | Excel, Word, PPT, PDF 및 15개 이상의 문서 형식을 하나의 패키지로. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Office?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Office) |
| [NewLife.Audio](https://github.com/NewLifeX/NewLife.Audio) | 크로스 플랫폼 오디오: 코덱, 장치 I/O, DSP, 컨테이너 및 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Audio?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Audio) |
| [NewLife.Video](https://github.com/NewLifeX/NewLife.Video) | 비디오 코덱, 컨테이너 형식, 프레임 처리 및 스트리밍. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Video?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Video) |
| [NewLife.Map](https://github.com/NewLifeX/NewLife.Map) | 바이두, 고덕(AMap), 텐센트, 톈디투 지도에 대한 통합 인터페이스와 지오코딩 및 좌표 변환. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.Map?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.Map) |
| [NewLife.IP](https://github.com/NewLifeX/NewLife.IP) | 오프라인 메모리 매핑 데이터베이스와 온라인 API 폴백을 갖춘 IPv4 위치 정보. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.IP?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.IP) |
| **도구 및 제품** | | |
| [CrazyCoder](https://github.com/NewLifeX/XCoder) | 개발자 도구 모음: 네트워크, 시리얼 포트, 암호화, 정규식, Modbus 및 MQTT. | [![NuGet](https://img.shields.io/nuget/dt/NewLife.XCoder?style=flat-square&logo=nuget&label=downloads)](https://www.nuget.org/packages/NewLife.XCoder) |
| [GitCandy](https://github.com/NewLifeX/GitCandy) | 셀프 호스팅 Git 소스 코드 관리 시스템. | — |
| [LuckyClover](https://github.com/NewLifeX/LuckyClover) | 구버전 호스트에서 시작할 때 일치하는 .NET 런타임을 자동으로 설치합니다. | — |

> 배지는 각 프로젝트의 기본 패키지를 나타냅니다. 테마, 확장 및 위성 패키지는 해당 NuGet 페이지에서 확인하세요. 여기에 나열하지 않은 30여 개 패키지(FTP, LoRa, Office 확장, 각종 프로토콜 드라이버 등)는 [NewLifeX 조직](https://github.com/NewLifeX)과 [newlifex.com](https://newlifex.com)에서 확인할 수 있습니다. 이 표는 NewLife.Core README에서 관리합니다.

## 문서

- [XCode 문서 내비게이션](Doc/XCode文档导航.md): 모듈별로 정리된 전체 문서 색인
- [XCode 사용 매뉴얼](Doc/XCode使用手册.md): 엔드투엔드 사용 안내
- [아키텍처 설계](Doc/架构设计.md): 계층, 구성 요소 및 핵심 설계 결정
- [다국어 지역화](Doc/多语言本地化.md): 메시지 모델, 언어 테이블 및 런타임 재정의
- 온라인 문서: https://newlifex.com/xcode
- [ChangeLog.md](ChangeLog.md): 버전 이력

## 기여

### 빌드 및 테스트

```
dotnet restore
dotnet build -c Release
dotnet test XUnitTest.XCode -c Release
```

- `XUnitTest.XCode` 아래에 케이스를 추가하세요. 성능 관련 제안에는 벤치마크나 설명을 포함해야 합니다.
- 리포지토리 규칙(강력한 이름, 다중 TFM, XML 주석, 조건부 컴파일)을 따르세요.
- 공개 API 변경에는 마이그레이션 전략(Obsolete → 제거)이 필요합니다.

### 릴리스

`v` 접두사 태그(예: `v2026.1018`)를 푸시하면 `.github/workflows/publish.yml`이 트리거됩니다. 워크플로는 XCode, XCodeTool 및 프로바이더 패키지(`XCode.MySql`, `XCode.SQLite`, `XCode.SqlServer`, `XCode.Oracle`, `XCode.PostgreSQL`, `XCode.DaMeng`, `XCode.DB2`, `XCode.KingBase`, `XCode.HighGo`)를 패키징하여 NuGet.org와 GitHub Packages에 푸시합니다.

### 피드백

- 이슈 / PR을 환영합니다: 성능 최적화 / 새로운 데이터베이스 프로바이더 / 문서 개선.
- 제출 전에 `dotnet build`와 관련 단위 테스트가 통과하는지 확인하고, 코드 스타일과 커밋 메시지는 리포지토리 규칙을 따르세요.
- 버그와 제안은 [Issue](https://github.com/NewLifeX/NewLife.XCode/issues)로 보고해 주세요. Star와 PR을 환영합니다.

## 팀

<img src="https://newlifex.com/logo.png" width="48" align="right" alt="NewLife" />

NewLife 팀은 2002년에 창립되어 20여 년간 .NET 및 IoT 생태계에 집중해 왔으며, 약 100개의 오픈소스 프로젝트(이 중 34개가 이 지도의 핵심)를 유지하고 전력, 물류, 의료, 산업 제어 및 인터넷 산업에 소프트웨어와 하드웨어를 아우르는 통합 솔루션을 제공합니다. "학문에는 선후가 없으며, 이룬 사람이 스승이다"라는 좌우명을 따릅니다.

오픈소스 컴포넌트는 NuGet에서 <!--nuget-total:start-->6.48M<!--nuget-total:end-->회 다운로드되었습니다(생태계 전체 기준, 2026-10).

웹사이트: https://newlifex.com · 오픈소스 조직: https://github.com/NewLifeX

<details>
<summary>위챗 공식 계정: 智能大石头(스캔하여 팔로우)</summary>

![智能大石头](https://newlifex.com/stone.jpg)

</details>

## 라이선스

MIT, 완전 오픈소스입니다. 상업적 사용 / 수정 / 재배포가 허용됩니다(비공개 배포를 위해 "NewLife"를 기업 브랜드로 교체 가능). 강력한 이름 인증서 `newlife.snk`가 내장되어 있습니다.

> 이 프로젝트가 도움이 되었다면 Star를 눌러 주시고, 팀에 공유하거나 Issue/PR로 개선 제안을 보내 주세요.
