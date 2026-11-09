using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Data.SqlClient;
using PaymentManagement.Api.Iso20022;

namespace PaymentManagement.Api.Persistence;

public sealed class PaymentStore(IConfiguration config, IsoService iso, ILogger<PaymentStore> logger)
{
    private string ConnectionString => config.GetConnectionString("PaymentManagement") ?? throw new InvalidOperationException("ConnectionStrings:PaymentManagement is missing.");
    public string Root => config["Demo:Root"] ?? Path.Combine(AppContext.BaseDirectory, "PAYMENT_MANAGEMENT");
    public string Operator => config["Demo:OperatorName"] ?? "LocalDemoOperator";
    public string OwnBank => config["Demo:OwnBankCode"] ?? "0019";
    public SqlConnection Open() => new(ConnectionString);
    private static string[] Categories = ["LOCAL", "ACH", "RTGS", "SWIFT", "INVALID"];
    public async Task<bool> DatabaseAvailable()
    {
        try { await using var db = Open(); await db.OpenAsync(); return await db.ExecuteScalarAsync<int>("SELECT 1") == 1; }
        catch { return false; }
    }
    public async Task<IEnumerable<dynamic>> Institutions()
    {
        await using var db = Open();
        return await db.QueryAsync("SELECT id,code,display_name AS displayName,is_active AS isActive FROM dbo.t_institutions ORDER BY code");
    }
    public async Task<IEnumerable<dynamic>> Definitions()
    {
        await using var db = Open();
        return await db.QueryAsync("SELECT code,family,namespace_uri AS namespaceUri,schema_file AS schemaFile,CONVERT(varchar(64),schema_sha256,2) AS sha256,source_url AS sourceUrl,is_tested AS isTested FROM dbo.t_iso_definitions ORDER BY code");
    }
    public async Task<IEnumerable<dynamic>> Rules()
    {
        await using var db = Open();
        return await db.QueryAsync("SELECT currency,CONVERT(varchar(50),threshold) AS threshold,revision,CONVERT(varchar(50),rv,1) AS version FROM dbo.t_demo_routing_rules ORDER BY currency");
    }
    public async Task<IEnumerable<dynamic>> Mappings(int institutionId)
    {
        await using var db = Open();
        return await db.QueryAsync("SELECT input_code AS inputCode,output_code AS outputCode,revision FROM dbo.t_response_mappings WHERE institution_id=@institutionId ORDER BY input_code", new { institutionId });
    }
    public async Task SetMapping(int institutionId, string input, string output)
    {
        if (!iso.AllowedCounterpart(input, output)) throw new WorkflowException(400, "The selected input-to-output mapping is not in the verified catalogue inventory.");
        await using var db = Open(); await db.OpenAsync();
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var institution = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.t_institutions WHERE id=@institutionId AND is_active=1", new { institutionId }, tx);
        if (institution == 0) throw new WorkflowException(404, "Institution not found.");
        await db.ExecuteAsync(@"IF EXISTS(SELECT 1 FROM dbo.t_response_mappings WITH(UPDLOCK,HOLDLOCK) WHERE institution_id=@institutionId AND input_code=@input)
 UPDATE dbo.t_response_mappings SET output_code=@output,revision=revision+1 WHERE institution_id=@institutionId AND input_code=@input
 ELSE INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES(@institutionId,@input,@output)", new { institutionId, input, output }, tx);
        tx.Commit();
    }
    public async Task SetRule(string currency, decimal threshold)
    {
        currency = currency.ToUpperInvariant();
        if (currency.Length != 3 || threshold <= 0) throw new WorkflowException(400, "Currency must have three letters and threshold must be positive.");
        await using var db = Open(); await db.OpenAsync();
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        await db.ExecuteAsync(@"IF EXISTS(SELECT 1 FROM dbo.t_demo_routing_rules WITH(UPDLOCK,HOLDLOCK) WHERE currency=@currency)
 UPDATE dbo.t_demo_routing_rules SET threshold=@threshold,revision=revision+1 WHERE currency=@currency
 ELSE INSERT dbo.t_demo_routing_rules(currency,threshold) VALUES(@currency,@threshold)", new { currency, threshold }, tx);
        tx.Commit();
    }
    public async Task<List<InstitutionRow>> ActiveInstitutions()
    {
        await using var db = Open();
        return (await db.QueryAsync<InstitutionRow>("SELECT id,code FROM dbo.t_institutions WHERE is_active=1")).ToList();
    }
    public string Folder(string code, string kind) => Path.Combine(Root, code, kind);
    public async Task<Guid> ImportClaimed(Guid id, int institutionId, string originalFilename, string claimedPath, string channel)
    {
        await using var db = Open(); await db.OpenAsync();
        var existing = await db.QuerySingleOrDefaultAsync<ReceiptState>("SELECT import_state AS ImportState,location_state AS LocationState,intake_institution_id AS IntakeInstitutionId FROM dbo.t_file_receipts WHERE id=@id", new { id });
        if (existing is not null)
        {
            await FinishMove(db, id, claimedPath, existing.ImportState == "Imported", existing.IntakeInstitutionId);
            return id;
        }
        var bytes = await File.ReadAllBytesAsync(claimedPath);
        var hash = SHA256.HashData(bytes);
        var institution = await db.QuerySingleOrDefaultAsync<InstitutionRow>("SELECT id,code FROM dbo.t_institutions WHERE id=@institutionId AND is_active=1", new { institutionId });
        if (institution is null) throw new WorkflowException(404, "Institution is not registered.");
        ImportedFile? imported = null;
        IsoException? isoError = null;
        try
        {
            if (bytes.Length > (config.GetValue<int?>("Demo:MaxSourceBytes") ?? 20_000_000))
                throw new IsoException("TechnicalSizeLimit", [new("Source exceeds configured technical size limit.")]);
            var rules = (await db.QueryAsync<RuleRow>("SELECT currency,threshold,revision FROM dbo.t_demo_routing_rules"))
                .ToDictionary(x => x.Currency, x => (x.Threshold, x.Revision));
            imported = iso.Read(bytes, rules, OwnBank);
        }
        catch (IsoException e) { isoError = e; }
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var duplicate = await db.ExecuteScalarAsync<Guid?>(@"SELECT TOP(1) id FROM dbo.t_file_receipts WITH(UPDLOCK,HOLDLOCK)
 WHERE effective_institution_id=@institutionId AND canonical_checksum=@hash", new { institutionId, hash }, tx);
        var outcome = duplicate.HasValue ? "Duplicate" : isoError is not null ? "ImportFailed" : "Imported";
        var errorType = duplicate.HasValue ? "DuplicateContent" : isoError?.Kind;
        var errors = duplicate.HasValue ? new[] { new IsoError("Exact source bytes were already imported for this institution.") } : isoError?.Errors;
        string? validationJson = errors is null ? null : JsonSerializer.Serialize(errors);
        var marker = claimedPath.LastIndexOf(".processing.", StringComparison.OrdinalIgnoreCase);
        var originalPath = marker < 0 ? claimedPath : claimedPath[..marker];
        await db.ExecuteAsync(@"INSERT dbo.t_file_receipts(id,intake_institution_id,effective_institution_id,channel,original_filename,original_path,current_path,received_utc,source_bytes,byte_length,checksum,canonical_checksum,import_state,location_state,detected_definition,error_type,validation_json,duplicate_of)
 VALUES(@id,@institutionId,@institutionId,@channel,@originalFilename,@originalPath,@claimedPath,SYSUTCDATETIME(),@bytes,@length,@hash,@canonical,@outcome,@location,@definition,@errorType,@validationJson,@duplicate)",
            new { id, institutionId, channel, originalFilename, originalPath, claimedPath, bytes, length = (long)bytes.Length, hash,
                canonical = outcome == "Duplicate" ? null : hash, outcome,
                location = outcome == "Imported" ? "ArchivePending" : "ErrorMovePending",
                definition = imported?.Definition ?? isoError?.Definition, errorType, validationJson, duplicate }, tx);
        if (outcome == "Imported")
        {
            var batchId = Guid.NewGuid();
            var mapping = await db.QuerySingleOrDefaultAsync<MappingRow>(@"SELECT output_code AS OutputCode,revision FROM dbo.t_response_mappings
 WHERE institution_id=@institutionId AND input_code=@input", new { institutionId, input = imported!.Definition }, tx);
            await db.ExecuteAsync(@"INSERT dbo.t_payment_batches(id,receipt_id,effective_institution_id,input_code,output_code,mapping_revision,original_message_id,original_creation,state,created_utc)
 VALUES(@batchId,@id,@institutionId,@input,@output,@revision,@messageId,@creation,'Editable',SYSUTCDATETIME())",
                new { batchId, id, institutionId, input = imported!.Definition, output = mapping?.OutputCode, revision = mapping?.Revision,
                    messageId = imported.MessageId, creation = imported.Creation }, tx);
            foreach (var category in Categories)
                await db.ExecuteAsync("INSERT dbo.t_batch_categories(batch_id,category) VALUES(@batchId,@category)", new { batchId, category }, tx);
            foreach (var group in imported.Groups)
            {
                var groupId = Guid.NewGuid();
                await db.ExecuteAsync(@"INSERT dbo.t_payment_groups(id,batch_id,source_ordinal,payment_info_id,debtor_name,debtor_account_id,debtor_account_kind,debtor_account_currency,debtor_agent,requested_execution_date,source_xml)
 VALUES(@groupId,@batchId,@ordinal,@paymentInfoId,@debtorName,@debtorAccount,@debtorAccountKind,@debtorCurrency,@debtorAgent,@executionDate,@sourceXml)",
                    new { groupId, batchId, ordinal = group.Ordinal, group.PaymentInfoId, group.DebtorName, group.DebtorAccount,
                        group.DebtorAccountKind, group.DebtorCurrency, group.DebtorAgent, group.ExecutionDate, group.SourceXml }, tx);
                foreach (var transfer in group.Transfers)
                {
                    var transferId = Guid.NewGuid();
                    var invalid = transfer.Routing.Category == "INVALID";
                    await db.ExecuteAsync(@"INSERT dbo.t_transfers(id,batch_id,group_id,source_ordinal,instruction_id,end_to_end_id,creditor_name,creditor_account_id,creditor_account_kind,creditor_agent,instructed_amount,instructed_currency,equivalent_amount,equivalent_currency,transfer_currency,account_currency,routing_amount,iban_country,iban_bank_code,rule_revision,routing_code,routing_explanation,category,decision,decision_source,decision_reason,decision_actor,decision_utc,source_xml)
 VALUES(@transferId,@batchId,@groupId,@ordinal,@instructionId,@endToEndId,@creditorName,@creditorAccount,@creditorAccountKind,@creditorAgent,@instructedAmount,@instructedCurrency,@equivalentAmount,@equivalentCurrency,@transferCurrency,@accountCurrency,@routingAmount,@country,@bankCode,@ruleRevision,@routingCode,@routingExplanation,@category,@decision,@source,@reason,@actor,@decided,@sourceXml)",
                        new { transferId, batchId, groupId, ordinal = transfer.Ordinal, transfer.InstructionId, transfer.EndToEndId,
                            transfer.CreditorName, transfer.CreditorAccount, transfer.CreditorAccountKind, transfer.CreditorAgent,
                            transfer.InstructedAmount, transfer.InstructedCurrency, transfer.EquivalentAmount, transfer.EquivalentCurrency,
                            transfer.TransferCurrency, transfer.AccountCurrency, transfer.RoutingAmount, transfer.Routing.Country,
                            transfer.Routing.BankCode, ruleRevision = transfer.Routing.RuleRevision, routingCode = transfer.Routing.Code,
                            routingExplanation = transfer.Routing.Explanation, category = transfer.Routing.Category,
                            decision = invalid ? "Rejected" : "Pending", source = invalid ? "System" : null,
                            reason = invalid ? transfer.Routing.Explanation : null, actor = invalid ? "System" : null,
                            decided = invalid ? (DateTime?)DateTime.UtcNow : null, transfer.SourceXml }, tx);
                    if (invalid) await Audit(db, tx, "System", "AutomaticRejection", id, batchId, transferId, null,
                        new { transfer.Routing.Code, transfer.Routing.Explanation });
                }
            }
            await Audit(db, tx, "System", "Imported", id, batchId, null, null, new { imported.Definition, transferCount = imported.Groups.Sum(g => g.Transfers.Count) });
        }
        else
        {
            var report = JsonSerializer.SerializeToUtf8Bytes(new {
                receiptId = id, institutionCode = institution.Code, originalFilename,
                detectedDefinition = imported?.Definition ?? isoError?.Definition, errorUtc = DateTime.UtcNow, errorType,
                errors, duplicateOfReceiptId = duplicate });
            var docId = Guid.NewGuid();
            await db.ExecuteAsync(@"INSERT dbo.t_outgoing_documents(id,kind,receipt_id,recipient_institution_id,filename,mime_type,content_bytes,content_sha256,generated_utc,publication_state)
 VALUES(@docId,'ImportErrorJson',@id,@institutionId,@filename,'application/json',@report,@contentHash,SYSUTCDATETIME(),'Pending')",
                new { docId, id, institutionId, filename = $"IMPORT_{id:N}.error.json", report, contentHash = SHA256.HashData(report) }, tx);
            await Audit(db, tx, "System", outcome, id, null, null, docId, new { errorType, duplicate });
        }
        tx.Commit();
        await FinishMove(db, id, claimedPath, outcome == "Imported", institutionId);
        logger.LogInformation("Receipt {ReceiptId} {Outcome} for institution {Institution}", id, outcome, institution.Code);
        return id;
    }
    private async Task FinishMove(SqlConnection db, Guid id, string claimedPath, bool success, int institutionId)
    {
        var code = await db.ExecuteScalarAsync<string>("SELECT code FROM dbo.t_institutions WHERE id=@institutionId", new { institutionId });
        if (code is null) return;
        var targetDir = Folder(code, success ? "ARCHIVED" : "ERROR");
        Directory.CreateDirectory(targetDir);
        var filename = Path.GetFileName(claimedPath);
        var marker = filename.LastIndexOf(".processing.", StringComparison.OrdinalIgnoreCase);
        if (marker >= 0) filename = filename[..marker];
        var target = Path.Combine(targetDir, $"{id:N}_{filename}");
        try
        {
            if (File.Exists(claimedPath) && !File.Exists(target)) File.Move(claimedPath, target);
            if (!File.Exists(target)) throw new IOException("Neither claimed nor destination source file exists.");
            await db.ExecuteAsync("UPDATE dbo.t_file_receipts SET current_path=@target,location_state=@state WHERE id=@id", new { id, target, state = success ? "Archived" : "Error" });
        }
        catch (IOException e) { logger.LogError(e, "Could not move receipt {ReceiptId} source", id); }
    }
    public async Task RecoverMoves()
    {
        await using var db = Open(); await db.OpenAsync();
        var pending = await db.QueryAsync<PendingMove>(@"SELECT id,current_path AS CurrentPath,intake_institution_id AS InstitutionId,
 import_state AS ImportState FROM dbo.t_file_receipts WHERE location_state IN ('ArchivePending','ErrorMovePending')");
        foreach (var row in pending)
            if (row.CurrentPath is not null)
                await FinishMove(db, row.Id, row.CurrentPath, row.ImportState == "Imported", row.InstitutionId);
    }
    internal static Task Audit(SqlConnection db, SqlTransaction tx, string actor, string action, Guid? receiptId,
        Guid? batchId, Guid? transferId, Guid? documentId, object? details) =>
        db.ExecuteAsync(@"INSERT dbo.t_workflow_audit(actor,action,receipt_id,batch_id,transfer_id,document_id,details_json)
 VALUES(@actor,@action,@receiptId,@batchId,@transferId,@documentId,@json)",
            new { actor, action, receiptId, batchId, transferId, documentId, json = details is null ? null : JsonSerializer.Serialize(details) }, tx);
}
public sealed class WorkflowException(int status, string message, object? details = null) : Exception(message)
{
    public int Status { get; } = status;
    public object? Details { get; } = details;
}
public sealed class InstitutionRow { public int Id { get; set; } public string Code { get; set; } = ""; }
public sealed class RuleRow { public string Currency { get; set; } = ""; public decimal Threshold { get; set; } public int Revision { get; set; } }
public sealed class MappingRow { public string OutputCode { get; set; } = ""; public int Revision { get; set; } }
public sealed class ReceiptState { public string ImportState { get; set; } = ""; public string LocationState { get; set; } = ""; public int IntakeInstitutionId { get; set; } }
public sealed class PendingMove { public Guid Id { get; set; } public string? CurrentPath { get; set; } public int InstitutionId { get; set; } public string ImportState { get; set; } = ""; }
