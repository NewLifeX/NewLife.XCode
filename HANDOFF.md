# Provider extension contracts — 2026-11-06

This branch starts at upstream `512d8a434`. It responds to the architecture review in NewLifeX/NewLife.XCode#103; that closed PR remains unchanged.

- Generic, opt-in `DbPackage.QueryCallback` configures only exact built-in extractors. Custom extractor callbacks and derived extractors retain their source and `Fetch`. A missing callback preserves the original DAL query path.
- `NameFormats.Exact` and an additive `ModelHelper.WriteXml` overload retain case-only physical name mappings. Existing enum values, method signatures and default XML behavior are preserved.
- No database type checks, DbSession/DAL changes, provider dependency changes or publication belong in this architecture PR.
- SQLite-only regressions provide the minimal NULL migration and XML spelling reproductions. Validation and final commit identity are recorded in the PR; downstream Provider work depends on this branch being accepted and published with the provider.
- Validation: 10/10 SQLite/XML regressions, no failures/skips; all six XCode targets build with 859 existing-repository warnings and 0 errors; English runtime-message check passes. The upstream source has mixed LF/CRLF despite the global editorconfig: preserve original per-file bytes and use `git -c core.whitespace=cr-at-eol diff --check`, rather than mixing an EOL rewrite into the architecture change. A formatter probe and the initial zero-test discovery are retained in external validation evidence and are not test PASS claims.
