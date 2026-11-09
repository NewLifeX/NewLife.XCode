# SonnetDB 5.0

XCode uses SonnetDB's native ADO.NET provider for embedded directories and remote Server connections. Install `XCode.SonnetDB` with the matching NewLife.XCode release. The SonnetDB dependency is **5.0.0** and requires .NET 10.

```csharp
DAL.AddConnStr("demo", "Data Source=./demo-db", null, "SonnetDB");
var dal = DAL.Create("demo");
```

For a Server use `Data Source=sonnetdb+http://localhost:5080/demo;Token=YOUR_TOKEN;Protocol=rest`, or the Server's HTTP/2 endpoint with `Protocol=frame-http2`. Use matching 5.0 Core/Data/Protocol and Server artifacts; do not combine the 5.0 provider with an older Server.

## Supported contracts

| XCode path | SonnetDB behavior |
| --- | --- |
| Entity CRUD, async identity and batch Insert/InsertIgnore/Upsert | Native relational SQL and RETURNING; generated identity is located by declaration metadata |
| BatchReplace | Native REPLACE deletes conflicting rows and inserts the new row; omitted fields use defaults, identity/triggers/FKs follow the engine contract |
| Decimal parameters, literals and ADD/ALTER | Exact decimal values; explicit precision and scale persist and reject inexact or overflowing writes |
| DateTime | New models use DATETIME2 and DbType.DateTime2 for 100ns ticks; Local inputs retain CLR wall-clock ticks in parameters and literals; explicit legacy DATETIME is a separate millisecond contract |
| DateTimeOffset and TimeSpan | Native DATETIMEOFFSET and DURATION; DateTimeOffset returns complete UTC ticks with offset zero |
| String Length and descriptions | STRING(n), length evolution and COMMENT ON TABLE/COLUMN; reverse metadata reads saved length, precision and REMARKS |
| Pagination | Nonnegative Int64 LIMIT/OFFSET positions, including positions larger than Int32.MaxValue |
| Bounded delete | Native DELETE LIMIT preserves the maximum direct row count for single/composite/no-primary-key tables |
| Transactions | ReadCommitted and Serializable; commit and rollback use the engine's native transactions |
| Empty views and model export | Declared column metadata without inferring types from data; NameFormats.Exact explicitly retains physical identifier spelling |
| Nullable values and cross-database migration | Session overrides preserve numeric NULL; SonnetDBPackage explicitly enables exact NULL page reads; custom extraction callbacks keep their own source DAL |

Reverse metadata also recognizes legacy `DATETIME` and `TIME` as DateTime/TimeSpan aliases. They do not replace the preferred types for new models or change the legacy precision contract. A compatible native temporal `CAST` default survives metadata/model replay without another `CAST` wrapper, so unchanged temporal defaults do not generate `ALTER COLUMN`; ordinary literals still receive the required native cast, and string-default comparison remains ordinal.

SQL names preserve their declared spelling. Ordinary references are case insensitive and quoted references are exact; embedded quote characters are escaped. For exact XML, use `table.WriteXml(writer, NameFormats.Exact)` or `EntityBuilderOption.NameFormat = NameFormats.Exact` on complete model export. This is an explicit option; the ordinary compact XML contract stays unchanged.

For a migration involving SonnetDB, opt in through the provider data package:

```csharp
var package = new SonnetDBPackage { Dal = source, IgnoreError = false };
package.Sync(table, target.ConnName, syncSchema: false, cancellationToken: token);
```

This keeps the existing ID/time/paging algorithms and reads SQL NULL on an independent source connection using that source's actual factory. It does not enlist an existing source-session transaction or provide a consistent database snapshot across pages; use an offline or otherwise stable source for that requirement. Each page is bounded by the extractor batch and source command timeout (30 seconds by default), with the Sync token checked before reading and between rows. External extractor factories continue to own their source/Fetch. Regular DbPackage and ordinary reads for other databases retain their previous defaults.

## Boundaries

- A UInt64 mapped to the provider's signed INT remains limited to Int64.MaxValue. A different database column type requires an explicit application mapping.
- New DateTime models use DATETIME2. Existing DATETIME columns retain the legacy contract; do not silently bind submillisecond values as DbType.DateTime.
- Unsupported transaction isolation levels are rejected. ReadCommitted does not imply dirty reads or arbitrary locking hints.
- Changes to primary-key or identity definitions require an explicit migration. The adapter does not silently rewrite complex schema migrations.
- Embedded DropDatabase does not delete a directory through SQL. Coordinate offline backup and restore before upgrading storage format.
- HTTP/2 uses native Frame where the driver supports it, plus HTTP/2 REST for applicable lifecycle/transaction operations. This is not a claim that every request uses binary Frame.

Run the complete `SonnetDBTests`, `SonnetDBMigrationTests` and `SonnetDBRemoteTests` files against the matching packages and real Server. The migration regressions cover the default ID, time and paging extractors, external extractor callbacks, numeric NULL, cancellation and other databases' default behavior. Set `SONNETDB_TEST_CONNECTION` and `SONNETDB_TEST_HTTP2_CONNECTION` for remote tests; the source tests otherwise explicitly skip remote acceptance. Release acceptance must inspect executed/skipped counts and verify the loaded package and Server identities.

Upstream merge, XCode NuGet publication and public-package consumer acceptance are separate delivery steps. Source/candidate validation does not establish those publication results.

This change depends on the generic, opt-in contracts in [PR #104](https://github.com/NewLifeX/NewLife.XCode/pull/104), as requested in the closed [PR #103](https://github.com/NewLifeX/NewLife.XCode/pull/103). Ordinary reader behavior is implemented through SonnetDBSession.OnFill/QueryAsync overrides. No database-specific branch is added to DbSession, DAL, ModelHelper or DbPackage. Existing publicly released XCode packages remain separate from this source candidate until the maintainer releases matching Core/Provider packages.
