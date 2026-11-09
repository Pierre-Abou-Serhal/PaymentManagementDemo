# Changes

## 2026-11-09

- Targeted .NET 8 and split the backend into a controller host, BLL, Common, and DAL solution based on BaseApiProject.
- Moved runtime SQL into `dbo.sp_*` procedures with a separate, reapplicable deployment script.
- Added original GrpHdr summaries to the work queue and complete header details/XML to queue drawers, receipts, and batches. Existing source files supply the data without a schema change.
- Redesigned navigation, queue filters/search, file reception, category processing, transfer review, report history, audit, and settings with responsive layouts and keyboard-accessible drawers.
- Kept finalized batches visibly submitted and retained the existing irreversible processing and immutable report workflow.
- Corrected equivalent-amount labels to show the supplied amount currency separately from the transfer currency, and formatted displayed amounts without converting decimal strings to floating point.
