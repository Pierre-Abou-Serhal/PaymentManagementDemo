# Test report — 2026-11-08

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
