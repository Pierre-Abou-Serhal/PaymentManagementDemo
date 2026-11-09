using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using PaymentManagement.BLL.Workers;
using PaymentManagement.Common;
using PaymentManagement.DAL;
using System.Text.Json;
using PaymentManagement.BLL.Iso20022;

namespace PaymentManagement.BLL;
public sealed partial class BLL
{
    public async Task<Guid> UploadFile(int institutionId, string filename, Stream source, CancellationToken cancellationToken)
    {
        var original = Path.GetFileName(filename);
        if (!original.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            throw new WorkflowException(400, "File must have .xml extension.");
        }

        await using var db = _dapperAccess.Open();
        var institution = await db.QuerySingleOrDefaultAsync<InstitutionRow>(Procedure.GetActiveInstitution, new { institutionId }) ?? throw new WorkflowException(404, "Institution not found.");
        var id = Guid.NewGuid();
        var inbound = Folder(institution.Code, "IN");
        Directory.CreateDirectory(inbound);
        var staged = Path.Combine(inbound, $"{id:N}__{original}.uploading");
        var claimed = Path.Combine(inbound, $"{id:N}__{original}.processing.{id:N}");
        await using (var stream = File.Create(staged))
        {
            await source.CopyToAsync(stream, cancellationToken);
        }

        File.Move(staged, claimed);
        if (!FolderPoller.TryClaim(id))
        {
            throw new WorkflowException(409, "Upload is already processing.");
        }

        try
        {
            return await ImportClaimed(id, institutionId, original, claimed, "Upload");
        }
        finally
        {
            FolderPoller.Release(id);
        }
    }

    public async Task<bool> DatabaseAvailable()
    {
        try
        {
            await using var db = _dapperAccess.Open();
            await db.OpenAsync();
            return await db.ExecuteScalarAsync<int>(Procedure.DatabaseAvailable) == 1;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<InstitutionSummary>> GetAllInstitutions()
    {
        await using var db = _dapperAccess.Open();
        return await db.QueryAsync<InstitutionSummary>(Procedure.ListInstitutions);
    }

    public async Task<IEnumerable<RoutingRule>> GetRoutingRules()
    {
        await using var db = _dapperAccess.Open();
        return await db.QueryAsync<RoutingRule>(Procedure.ListRoutingRules);
    }

    public async Task<IEnumerable<ResponseMapping>> GetResponseMappings(int institutionId)
    {
        await using var db = _dapperAccess.Open();
        return await db.QueryAsync<ResponseMapping>(Procedure.ListResponseMappings, new { institutionId });
    }

    public async Task SaveResponseMapping(int institutionId, string input, string output)
    {
        if (!_isoService.AllowedCounterpart(input, output))
        {
            throw new WorkflowException(400, "The selected input-to-output mapping is not in the verified catalogue inventory.");
        }

        await using var db = _dapperAccess.Open();
        await db.BeginSerializableAsync();
        var institution = await db.QuerySingleOrDefaultAsync<InstitutionRow>(Procedure.GetActiveInstitution, new { institutionId });
        if (institution is null)
        {
            throw new WorkflowException(404, "Institution not found.");
        }

        await db.ExecuteAsync(Procedure.UpsertResponseMapping, new { institutionId, input, output });
        await db.CommitAsync();
    }

    public async Task SaveRoutingRule(string currency, decimal threshold)
    {
        currency = currency.ToUpperInvariant();
        if (currency.Length != 3 || threshold <= 0)
        {
            throw new WorkflowException(400, "Currency must have three letters and threshold must be positive.");
        }

        await using var db = _dapperAccess.Open();
        await db.BeginSerializableAsync();
        await db.ExecuteAsync(Procedure.UpsertRoutingRule, new { currency, threshold });
        await db.CommitAsync();
    }

    public async Task<List<InstitutionRow>> GetActiveInstitutions()
    {
        await using var db = _dapperAccess.Open();
        return (await db.QueryAsync<InstitutionRow>(Procedure.ListActiveInstitutions)).ToList();
    }

    public async Task<Guid> ImportClaimed(Guid id, int institutionId, string originalFilename, string claimedPath, string channel)
    {
        await using var db = _dapperAccess.Open();
        await db.OpenAsync();
        var existing = await db.QuerySingleOrDefaultAsync<ReceiptState>(Procedure.GetReceiptMoveState, new { id });
        if (existing is not null)
        {
            await FinishMove(db, id, claimedPath, existing.ImportState == "Imported", existing.IntakeInstitutionId);
            return id;
        }

        var bytes = await File.ReadAllBytesAsync(claimedPath);
        var hash = SHA256.HashData(bytes);
        var institution = await db.QuerySingleOrDefaultAsync<InstitutionRow>(Procedure.GetActiveInstitution, new { institutionId });
        if (institution is null)
        {
            throw new WorkflowException(404, "Institution is not registered.");
        }

        ImportedFile? imported = null;
        IsoException? isoError = null;
        try
        {
            if (bytes.Length > Settings.MaxSourceBytes)
            {
                throw new IsoException("TechnicalSizeLimit", [new("Source exceeds configured technical size limit.")]);
            }

            var rules = (await db.QueryAsync<RuleRow>(Procedure.GetRoutingThresholds)).ToDictionary(x => x.Currency, x => (x.Threshold, x.Revision));
            imported = _isoService.Read(bytes, rules, OwnBank);
        }
        catch (IsoException e)
        {
            isoError = e;
        }

        await db.BeginSerializableAsync();
        var duplicate = await db.ExecuteScalarAsync<Guid?>(Procedure.FindCanonicalReceipt, new { institutionId, hash });
        var outcome = duplicate.HasValue ? "Duplicate" : isoError is not null ? "ImportFailed" : "Imported";
        var errorType = duplicate.HasValue ? "DuplicateContent" : isoError?.Kind;
        var errors = duplicate.HasValue ? new[]
        {
            new IsoError("Exact source bytes were already imported for this institution.")
        }

        : isoError?.Errors;
        string? validationJson = errors is null ? null : JsonSerializer.Serialize(errors);
        var marker = claimedPath.LastIndexOf(".processing.", StringComparison.OrdinalIgnoreCase);
        var originalPath = marker < 0 ? claimedPath : claimedPath[..marker];
        await db.ExecuteAsync(Procedure.InsertFileReceipt, new { id, institutionId, channel, originalFilename, originalPath, claimedPath, bytes, length = (long)bytes.Length, hash, canonical = outcome == "Duplicate" ? null : hash, outcome, location = outcome == "Imported" ? "ArchivePending" : "ErrorMovePending", definition = imported?.Definition ?? isoError?.Definition, errorType, validationJson, duplicate });
        if (outcome == "Imported")
        {
            var batchId = Guid.NewGuid();
            var mapping = await db.QuerySingleOrDefaultAsync<MappingRow>(Procedure.GetResponseMapping, new { institutionId, input = imported!.Definition });
            await db.ExecuteAsync(Procedure.InsertBatch, new { batchId, id, institutionId, input = imported!.Definition, output = mapping?.OutputCode, revision = mapping?.Revision, messageId = imported.MessageId, creation = imported.Creation });
            await db.ExecuteAsync(Procedure.InsertBatchCategories, new { batchId });
            foreach (var group in imported.Groups)
            {
                var groupId = Guid.NewGuid();
                await db.ExecuteAsync(Procedure.InsertPaymentGroup, new { groupId, batchId, ordinal = group.Ordinal, group.PaymentInfoId, group.DebtorName, group.DebtorAccount, group.DebtorAccountKind, group.DebtorCurrency, group.DebtorAgent, group.ExecutionDate, group.SourceXml });
                foreach (var transfer in group.Transfers)
                {
                    var transferId = Guid.NewGuid();
                    var invalid = transfer.Routing.Category == "INVALID";
                    await db.ExecuteAsync(Procedure.InsertTransfer, new { transferId, batchId, groupId, ordinal = transfer.Ordinal, transfer.InstructionId, transfer.EndToEndId, transfer.CreditorName, transfer.CreditorAccount, transfer.CreditorAccountKind, transfer.CreditorAgent, transfer.InstructedAmount, transfer.InstructedCurrency, transfer.EquivalentAmount, transfer.EquivalentCurrency, transfer.TransferCurrency, transfer.AccountCurrency, transfer.RoutingAmount, transfer.Routing.Country, transfer.Routing.BankCode, ruleRevision = transfer.Routing.RuleRevision, routingCode = transfer.Routing.Code, routingExplanation = transfer.Routing.Explanation, category = transfer.Routing.Category, decision = invalid ? "Rejected" : "Pending", source = invalid ? "System" : null, reason = invalid ? transfer.Routing.Explanation : null, actor = invalid ? "System" : null, decided = invalid ? (DateTime? )DateTime.UtcNow : null, transfer.SourceXml });
                    if (invalid)
                    {
                        await WriteAudit(db, "System", "AutomaticRejection", id, batchId, transferId, null, new { transfer.Routing.Code, transfer.Routing.Explanation });
                    }
                }
            }

            await WriteAudit(db, "System", "Imported", id, batchId, null, null, new { imported.Definition, transferCount = imported.Groups.Sum(g => g.Transfers.Count) });
        }
        else
        {
            var report = JsonSerializer.SerializeToUtf8Bytes(new { receiptId = id, institutionCode = institution.Code, originalFilename, detectedDefinition = imported?.Definition ?? isoError?.Definition, errorUtc = DateTime.UtcNow, errorType, errors, duplicateOfReceiptId = duplicate });
            var docId = Guid.NewGuid();
            await db.ExecuteAsync(Procedure.InsertImportErrorDocument, new { docId, id, institutionId, filename = $"IMPORT_{id:N}.error.json", report, contentHash = SHA256.HashData(report) });
            await WriteAudit(db, "System", outcome, id, null, null, docId, new { errorType, duplicate });
        }

        await db.CommitAsync();
        await FinishMove(db, id, claimedPath, outcome == "Imported", institutionId);
        _logger.LogInformation("Receipt {ReceiptId} {Outcome} for institution {Institution}", id, outcome, institution.Code);
        return id;
    }

    private async Task FinishMove(DalSession db, Guid id, string claimedPath, bool success, int institutionId)
    {
        var code = await db.ExecuteScalarAsync<string>(Procedure.GetInstitutionCode, new { institutionId });
        if (code is null)
        {
            return;
        }

        var targetDir = Folder(code, success ? "ARCHIVED" : "ERROR");
        Directory.CreateDirectory(targetDir);
        var filename = Path.GetFileName(claimedPath);
        var marker = filename.LastIndexOf(".processing.", StringComparison.OrdinalIgnoreCase);
        if (marker >= 0)
        {
            filename = filename[..marker];
        }

        var target = Path.Combine(targetDir, $"{id:N}_{filename}");
        try
        {
            if (File.Exists(claimedPath) && !File.Exists(target))
            {
                File.Move(claimedPath, target);
            }

            if (!File.Exists(target))
            {
                throw new IOException("Neither claimed nor destination source file exists.");
            }

            await db.ExecuteAsync(Procedure.UpdateReceiptLocation, new { id, target, state = success ? "Archived" : "Error" });
        }
        catch (IOException e)
        {
            _logger.LogError(e, "Could not move receipt {ReceiptId} source", id);
        }
    }

    public async Task RecoverMoves()
    {
        await using var db = _dapperAccess.Open();
        await db.OpenAsync();
        var pending = await db.QueryAsync<PendingMove>(Procedure.ListPendingReceiptMoves);
        foreach (var row in pending)
        {
            if (row.CurrentPath is not null)
            {
                await FinishMove(db, row.Id, row.CurrentPath, row.ImportState == "Imported", row.InstitutionId);
            }
        }
    }

    internal static Task WriteAudit(DalSession db, string actor, string action, Guid? receiptId, Guid? batchId, Guid? transferId, Guid? documentId, object? details) => db.ExecuteAsync(Procedure.InsertAuditEvent, new { actor, action, receiptId, batchId, transferId, documentId, json = details is null ? null : JsonSerializer.Serialize(details) });
}
