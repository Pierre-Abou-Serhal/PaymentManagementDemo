# Payment Management Demo

ASP.NET Core 10 API and folder workers, Angular 22 operator UI, and SQL Server tables for the pain.001 → pain.002 demonstration described in the BRD. The application records an operator's external payment outcomes; it does not execute payments.

The work queue shows original `GrpHdr` details immediately for received files: message ID, initiating party, source creation timestamp, declared transaction count, and control sum when supplied. **Details** opens a drawer with every header field (including repeated/nested values) and the header XML. The same information appears on receipt and batch screens. Existing receipts are supported from their retained source bytes; no extra SQL script or reimport is needed.

## Files to apply manually

1. In SQL Server Management Studio, connect to your SQL Server instance (default: `.\SQLEXPRESS`) using Windows Authentication.
2. Run [`sql/001_schema.sql`](sql/001_schema.sql) in a new query window. It creates `PaymentManagementDemo` if absent and then creates the `dbo.t_*` tables, constraints, and indexes.
3. Run [`sql/002_data.sql`](sql/002_data.sql). It inserts ALFA and AZADEYA, four invented demo thresholds, the 30-definition ISO inventory, and their response mappings. It does not seed payment outcomes.

The scripts use `GO`, so run them in SSMS or `sqlcmd`, in order. Both can be reapplied without recreating existing rows. No stored procedures are needed by this implementation; if added later, use the requested `sp_*` convention. The application does **not** apply either script automatically or modify the target database during setup.

## Local run

Requirements: .NET 10 SDK, Node.js 22.22.3+ or 24.15.0+ (or 26+), npm, and a SQL Server instance accessible to the Windows identity running the API. The default connection uses Windows Authentication on the local `.\SQLEXPRESS` instance and targets `PaymentManagementDemo`. If the SQL instance or data location differs, set `ConnectionStrings__PaymentManagement` and `Demo__Root` in your environment. Keep credentials out of the tracked settings files. The API identity needs read/write on the database and on the demo root. To keep demo data inside the working tree, use the ignored `.local-data` directory as the root.

From the repository root, in separate PowerShell terminals:

```powershell
dotnet run --project backend/PaymentManagement.Api.csproj --no-launch-profile
```

```powershell
cd frontend
npm ci
npm start
```

Open `http://localhost:4200`. The Angular development proxy forwards `/api` and `/health` to the API at `http://localhost:5000`. The API listens on `127.0.0.1:5000` by default. The queue shows database/storage health and the demo operator identity. The `--no-launch-profile` option matters because the generated launch profiles use other ports. After backend source changes, stop and rerun the API; `npm start` refreshes frontend changes automatically.

The queue refreshes every 15 seconds and supports institution/status filters and server-side search by filename or original message ID. Batch review uses **Transfers**, **Reports**, and **Activity** tabs. File and transfer drawers support Escape, keyboard focus containment, and focus return. Wide tables scroll inside their cards on narrow screens.

The backend creates `ALFA` and `AZADEYA` folders under `C:\PaymentManagement`, each with `IN`, `OUT`, `ARCHIVED`, and `ERROR`. The `Demo:Root` setting can relocate this tree. The API uses one configured local operator identity (`Demo:OperatorName`, initially `LocalDemoOperator`) for audit. This is a trusted local demo identity, not an authentication system; keep the API and UI restricted to internal operators.

## Walk through a file

1. Use **Upload initiation file** with ALFA and [`demo/samples/mixed-demo.xml`](demo/samples/mixed-demo.xml), or copy that XML to `C:\PaymentManagement\ALFA\IN`. Polling waits for two stable observations before claiming a file.
2. Open the receipt from the queue. A successful original moves to `ARCHIVED`; the batch retains its original bytes and individual source XML. The mixed fixture has two payment groups, seven transfers, all four execution categories, and three automatic `Invalid` rejections.
3. Open the batch. Mark each nonempty execution category **Processed** to attest that its payments were handled externally. Select transfers and approve or reject them; rejections need a reason. `Invalid` transfers are already rejected by System and cannot be changed.
4. When every transfer has a definitive outcome, choose **Submit final report**. The submission card changes to **Report submitted**. Choose **View reports**, download its pain.002 from **Report history**, and find the identical document in ALFA's `OUT` folder. The response contains an individual result for every transfer.
5. Reopen the batch, change an eligible decision, and submit again. Revision 2 gets a new report identity and OUT filename; revision 1 remains downloadable with unchanged bytes.
6. Upload the same bytes again with ALFA to see a linked duplicate receipt and an `IMPORT_{ReceiptId}.error.json` report. Upload [`demo/samples/malformed.xml`](demo/samples/malformed.xml) to see the original in `ERROR`, structured validation details, and a technical JSON report in `OUT`. [`demo/samples/all-invalid.xml`](demo/samples/all-invalid.xml) can be explicitly finalized without any category Process action. [`demo/samples/one-hundred.xml`](demo/samples/one-hundred.xml) exercises five groups and 100 transfers.

Settings allow operators to change institution response mappings and demo routing thresholds. An imported batch pins the selected mapping and routing rule revision. Institution correction is available only before any execution category has been processed. Category processing is irreversible. The UI uses relative API URLs and paginates transfer rows.

## Automated checks

```powershell
dotnet run --project tests/IsoChecks/IsoChecks.csproj
dotnet build backend/PaymentManagement.Api.csproj
cd frontend; npm run build
```

After the API and SQL scripts are running, PowerShell 7 can exercise the real HTTP/SQL/folder workflow:

```powershell
./demo/smoke.ps1
```

The smoke script generates unique message IDs, so it can be run repeatedly. It creates and finalizes sample batches in the **currently configured database**. Run it only in a demo database. The [test report](tests/TEST_REPORT.md) records the per-version matrix and integration checks.

For an additional UI/API preview instance, `--Demo:RunWorkers false` disables its polling and publication workers. The normal application defaults to workers enabled. Do not run two worker-enabled backends against the same demo folders/database.

## Windows Service and IIS

Publish the API on Windows:

```powershell
dotnet publish backend/PaymentManagement.Api.csproj -c Release -r win-x64 --self-contained false -o .\publish\backend
./deploy/windows-service.ps1 -Action Install -Executable (Resolve-Path .\publish\backend\PaymentManagement.Api.exe)
```

Set the service's **Log On** identity to a Windows account with SQL access to `PaymentManagementDemo` and modify access to the configured folder tree; allow it to create the root if it does not exist. Then run `./deploy/windows-service.ps1 -Action Start`. The same script supports `Stop` and `Uninstall`. `Demo:Root`, connection string, port, and operator identity can be overridden through the service environment or `appsettings.json` beside the published executable. The service is the sole polling/publishing backend; do not host another API instance under IIS.

Build the frontend with `npm ci` and `npm run build`, then point an IIS static site at `frontend/dist/frontend/browser`. Install IIS **URL Rewrite** and **Application Request Routing**, enable ARR proxy, and retain the included `web.config` in the deployed directory. It rewrites `/api/*` and `/health` to the local service at `127.0.0.1:5000` and falls back to `index.html` for Angular routes. If the service port changes, update both `web.config` and the service URL. Put access controls/TLS at the IIS boundary for any nonlocal use.

## ISO scope and limits

The bundled [`manifest`](backend/Iso20022/manifest.json) lists 15 pain.001 and 15 pain.002 definitions, actual namespaces, XSD hashes, source URLs, and permitted counterpart choices. The implementation validates inbound and generated XML against their respective bundled XSDs, retains original bytes and per-group/transfer source XML, and generates full reports from the pinned output definition. ISO catalogue IDs were reconciled with the [current catalogue](https://www.iso20022.org/iso-20022-message-definitions?search=pain) and [archive](https://www.iso20022.org/catalogue-messages/iso-20022-messages-archive?search=pain). The official XSD downloads returned HTTP 403 in this environment; the bundled schema bytes came from the public mirror named in the manifest. Recent schemas were cross-checked against another mirror. See [third-party notices](THIRD_PARTY_NOTICES.md) for schema provenance and applicable terms. Obtain and compare official XSD copies before production use.

The scripts were applied and the workflow tested on the available SQL Server 2025 Express instance with the temporary test database set to compatibility level **130**. No SQL Server 2016 engine was available here; a live 2016 run remains the final deployment check. The app is a local/internal demonstration and has no bank integration, institution portal, or real authentication.
