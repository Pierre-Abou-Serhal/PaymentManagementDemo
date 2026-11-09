# Test report — 2026-11-08

## .NET 8 retarget

- The full solution built with .NET SDK 8.0.427 in Release with zero warnings. All 155 ISO checks passed.
- The ASP.NET Core 8 API started and Swagger listed all 24 payment actions. The HTTP smoke test passed against an isolated SQL Server Express database at compatibility level 130.

## BaseApiProject and BaseFrontendProject alignment — 2026-11-09

- The .NET 10 Release build passed with zero warnings, all 155 ISO checks passed, and the Angular production build succeeded.
- Swagger documented 24 named payment actions. The HTTP smoke test passed against SQL Server Express at compatibility level 130 in a temporary database. It covered correlation and validation envelopes, settings reads, multipart intake, GrpHdr, stale bulk decisions, idempotent finalization, audit history, and XML and JSON attachments.
- Headless Edge loaded the work queue and settings with data from the temporary API and database.
- The tests used ASP.NET Core structured logging. Building with `SGBL.NLogUtil` requires the team's NuGet source and `-p:UseTeamLogging=true`.

## .NET 8 and stored procedure update — 2026-11-09

- Built the controller, BAL, DAL, Common, and ISO check projects with SDK **8.0.427**. The Release build passed without warnings, and all **155** ISO checks passed.
- Applied the schema, seed data, and `003_stored_procedures.sql` in a temporary SQL Server 2025 Express database at compatibility level **130**. The procedure script applied twice successfully. All 60 procedure names matched the DAL calls; the backend had no inline table SQL. A native SQL Server 2016 engine was not available.
- The HTTP smoke test passed for import, automatic rejection, atomic stale bulk conflict, finalization, revision history, duplicates, and report download. Additional checks passed for folder polling and archive, GrpHdr search, institution reassignment and locking, rule and mapping revision updates, exact source and report bytes, audit, validation errors, and publication failure/retry.
- Windows x64 framework-dependent publish contained `PaymentManagement.Api.exe`, the manifest, and all 30 schemas. The Windows Service was not installed. The existing `PaymentManagementDemo` database was not migrated during verification.

## Interface and GrpHdr update — 2026-11-09

The new header checks passed for all 15 initiation definitions, plus UTF-16/non-ASCII content, exact control-sum text, repeated authorization fields, nested party identifiers, malformed XML, and blocked DTD/external entities. The queue/receipt projection reads retained source bytes, removes the source blob from JSON, and adds the header view without a schema migration. Existing report/adaptor checks still pass. The frontend production build and backend Release build pass.

The updated interface was checked in the in-app browser using a temporary API with polling/publication workers disabled and the existing local database. Checks covered visible queue header summaries, full-field expansion, message-ID search, Escape dismissal, transfer-drawer focus, correct equivalent-amount currency labels, submitted/report history, upload controls, and institution/threshold settings. The queue and header drawer were also inspected at a narrow viewport. These browser checks did not submit payments, alter outcomes/settings, or generate reports in the user's database.

NuGet's optional package-vulnerability lookup was unavailable during the test run; the local build and executable checks completed successfully.

## Original implementation checks

`dotnet run --project tests/IsoChecks/IsoChecks.csproj` passed. Each row below had an XSD-validated pain.001 fixture imported, with original groups/transfers retained; its configured counterpart produced XSD-validated full reports for **all Approved**, **all Rejected**, and **Mixed** outcomes. Every pain.002 definition also passed those three scenarios independently with one `TxSts` result per input transfer.

| pain.001 input | Tested pain.002 counterpart(s) | Result |
|---|---|---|
| pain.001.001.01 | pain.002.001.01 | Pass |
| pain.001.002.01 | pain.002.001.01 | Pass |
| pain.001.003.01 | pain.002.001.01 | Pass |
| pain.001.001.02 | pain.002.001.02 | Pass |
| pain.001.001.03 | pain.002.001.03 | Pass |
| pain.001.001.04 | pain.002.001.04 | Pass |
| pain.001.001.05 | pain.002.001.05 | Pass |
| pain.001.001.06 | pain.002.001.06 | Pass |
| pain.001.001.07 | pain.002.001.07, pain.002.001.08 | Pass |
| pain.001.001.08 | pain.002.001.09 | Pass |
| pain.001.001.09 | pain.002.001.10 | Pass |
| pain.001.001.10 | pain.002.001.11 | Pass |
| pain.001.001.11 | pain.002.001.12, pain.002.001.13 | Pass |
| pain.001.001.12 | pain.002.001.14 | Pass |
| pain.001.001.13 | pain.002.001.15 | Pass |

Additional direct checks passed for Matrix A's five categories on a seven-transfer, two-group fixture; an all-Invalid input; malformed and XSD-invalid files; and a 100-transfer, five-group input. `dotnet build` succeeded without warnings. Angular production build succeeded with Node 24.22.1.

Both SQL scripts were syntax checked, applied to a fresh temporary database on SQL Server 2025 Express, and then exercised with compatibility level 130. `demo/smoke.ps1` passed against the API and that database: archive, grouping/classification, pending finalization block, category processing, atomic stale bulk conflict, finalization, seven individual XML results, reopen/new revision, immutable earlier report, duplicate linkage, and all-Invalid explicit finalization. Additional manual API checks during development covered malformed/XSD error JSON and moves, OUT publication and exact-byte download, institution correction before/after processing, Invalid edit rejection, and same bytes in a different institution.

The test host is **not** a SQL Server 2016 engine. Compatibility level 130 exercises its query behavior but cannot establish full engine compatibility. A Windows Service/IIS deployment and fault-injection checks for interrupted archive/publication operations were not performed on this host. These are remaining acceptance checks for the target environment; they are not reported as passed.
