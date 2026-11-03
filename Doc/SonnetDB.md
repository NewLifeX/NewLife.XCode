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
| Empty views and model export | Declared column metadata without inferring types from data; XML retains physical identifier spelling |
| Nullable values and cross-database migration | Reader values preserve NULL, including numeric NULL; custom extraction callbacks keep their own source DAL |

SQL names preserve their declared spelling. Ordinary references are case insensitive and quoted references are exact; embedded quote characters are escaped. The provider preserves physical model names when writing XML.

## Boundaries

- A UInt64 mapped to the provider's signed INT remains limited to Int64.MaxValue. A different database column type requires an explicit application mapping.
- New DateTime models use DATETIME2. Existing DATETIME columns retain the legacy contract; do not silently bind submillisecond values as DbType.DateTime.
- Unsupported transaction isolation levels are rejected. ReadCommitted does not imply dirty reads or arbitrary locking hints.
- Changes to primary-key or identity definitions require an explicit migration. The adapter does not silently rewrite complex schema migrations.
- Embedded DropDatabase does not delete a directory through SQL. Coordinate offline backup and restore before upgrading storage format.
- HTTP/2 uses native Frame where the driver supports it, plus HTTP/2 REST for applicable lifecycle/transaction operations. This is not a claim that every request uses binary Frame.

Run the complete `SonnetDBTests`, `SonnetDBMigrationTests` and `SonnetDBRemoteTests` files against the matching packages and real Server. The migration regressions cover the default ID, time and paging extractors, external extractor callbacks, numeric NULL, cancellation and other databases' default behavior. Set `SONNETDB_TEST_CONNECTION` and `SONNETDB_TEST_HTTP2_CONNECTION` for remote tests; the source tests otherwise explicitly skip remote acceptance. Release acceptance must inspect executed/skipped counts and verify the loaded package and Server identities.

Upstream merge, XCode NuGet publication and public-package consumer acceptance are separate delivery steps. Source/candidate validation does not establish those publication results.
