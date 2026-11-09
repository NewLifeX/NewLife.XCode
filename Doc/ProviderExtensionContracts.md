# Optional migration and exact XML contracts

The default `DbTable` conversion may map numeric SQL NULL to a CLR default value. An ETL consumer that needs SQL NULL must be able to supply its own page reader while retaining the ID/time/offset algorithms. Set `DbPackage.QueryCallback` to read a page using the provided source, builder, offset, limit and cancellation token. The option affects `Sync` only, never ordinary DAL queries. Callers own reader disposal, SQL parameter binding, page bounds and timeout. User-supplied extractor factories and derived extractors continue to own their query path.

`PackageExtensionTests` reproduces a SQLite NULL becoming zero with the default path, and verifies an opt-in reader preserves it for each built-in algorithm. It also checks custom-source ownership and the unchanged default.

For a model with `Name="MixedName"` and `TableName="mixedname"`, the existing compact XML can omit the physical spelling even with `NameFormats.None`. Use `table.WriteXml(writer, NameFormats.Exact)` or set `EntityBuilderOption.NameFormat` to `Exact` when exporting a complete model. This keeps case-only physical mappings, without changing existing XML defaults or enum numeric values. The five-argument `ModelHelper.WriteXml` overload exposes the same opt-in for individual model members; the original four-argument method remains available for binary callers.

These extensions have no database-specific behavior. Provider overrides for ordinary session reads stay downstream; a separate Provider PR can depend on this architecture PR. Acceptance and release decisions remain with the maintainers.
