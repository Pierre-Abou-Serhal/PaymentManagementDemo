using System.Security.Cryptography;
using PaymentManagement.Common;
using PaymentManagement.DAL;
using System.Xml.Linq;
using PaymentManagement.BLL.Iso20022;

namespace PaymentManagement.BLL;
public sealed partial class BLL
{
    public async Task CorrectInstitutionAssignment(Guid receiptId, InstitutionRequest request)
    {
        await using var db = _dapperAccess.Open();
        var batchId = await db.ExecuteScalarAsync<Guid?>(Procedure.GetBatchIdForReceipt, new { receiptId });
        if (!batchId.HasValue)
        {
            throw new WorkflowException(404, "Imported batch not found for receipt.");
        }

        await CorrectInstitution(batchId.Value, request);
    }

    private static byte[] Version(string value)
    {
        try
        {
            var bytes = Convert.FromHexString(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value);
            if (bytes.Length == 8)
            {
                return bytes;
            }
        }
        catch (FormatException)
        {
        }

        throw new WorkflowException(400, "Expected rowversion must be a 16-digit hexadecimal value.");
    }

    public async Task<IEnumerable<ReceiptSummary>> GetFileReceipts(int? institutionId, string? state, int skip, int take, string? search = null)
    {
        await using var db = _dapperAccess.Open();
        var pattern = string.IsNullOrWhiteSpace(search) ? null : "%" + search.Trim().Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%";
        var rows = await db.QueryAsync<ReceiptSummary>(Procedure.ListFileReceipts, new { institutionId, state, pattern, skip = Math.Max(0, skip), take = Math.Clamp(take, 1, 100) });
        foreach (var receipt in rows)
        {
            ReadGroupHeader(receipt);
        }

        return rows;
    }

    public async Task<ReceiptDetail?> GetFileReceipt(Guid id)
    {
        await using var db = _dapperAccess.Open();
        var receipt = await db.QuerySingleOrDefaultAsync<ReceiptDetail>(Procedure.GetFileReceipt, new { id });
        if (receipt is not null)
        {
            ReadGroupHeader(receipt);
        }

        return receipt;
    }

    private static void ReadGroupHeader(ReceiptInfo receipt)
    {
        receipt.GroupHeader = GroupHeaderReader.Read(receipt.SourceBytes);
        receipt.SourceBytes = [];
    }

    public async Task<(byte[] Bytes, string Filename)?> GetOriginalFile(Guid id)
    {
        await using var db = _dapperAccess.Open();
        var row = await db.QuerySingleOrDefaultAsync<OriginalRow>(Procedure.GetOriginalFile, new { id });
        return row is null ? null : (row.Bytes, row.Filename);
    }

    public async Task<BatchView?> GetBatch(Guid id)
    {
        await using var db = _dapperAccess.Open();
        var batch = await db.QuerySingleOrDefaultAsync<BatchInfo>(Procedure.GetBatch, new { id });
        if (batch is null)
        {
            return null;
        }

        var categories = await db.QueryAsync<BatchCategory>(Procedure.ListBatchCategories, new { id });
        var blockers = await Blockers(db, id);
        var currencyTotals = await db.QueryAsync<CurrencyTotal>(Procedure.GetCurrencyTotals, new { id });
        return new BatchView(batch, categories, currencyTotals, blockers);
    }

    public async Task<IEnumerable<PaymentGroup>> GetPaymentGroups(Guid id)
    {
        await using var db = _dapperAccess.Open();
        return await db.QueryAsync<PaymentGroup>(Procedure.ListPaymentGroups, new { id });
    }

    public async Task<IEnumerable<TransferSummary>> GetTransfers(Guid id, string? category, string? decision, Guid? groupId, int skip, int take)
    {
        await using var db = _dapperAccess.Open();
        return await db.QueryAsync<TransferSummary>(Procedure.ListTransfers, new { id, category, decision, groupId, skip = Math.Max(0, skip), take = Math.Clamp(take, 1, 100) });
    }

    public async Task<TransferDetail?> GetTransfer(Guid id)
    {
        await using var db = _dapperAccess.Open();
        var transfer = await db.QuerySingleOrDefaultAsync<TransferDetail>(Procedure.GetTransfer, new { id });
        if (transfer is null)
        {
            return null;
        }

        var source = XDocument.Parse(transfer.SourceXml);
        transfer.Remittances = source.Descendants().Where(element => element.Name.LocalName == "RmtInf").SelectMany(element => element.Elements()).Select(element => new Remittance(element.Name.LocalName, element.Value, element.ToString(SaveOptions.DisableFormatting))).ToArray();
        transfer.History = await db.QueryAsync<AuditEntry>(Procedure.ListTransferAudit, new { id });
        return transfer;
    }

    public async Task ProcessCategory(Guid batchId, string category)
    {
        category = category.ToUpperInvariant();
        if (category is not ("LOCAL" or "ACH" or "RTGS" or "SWIFT"))
        {
            throw new WorkflowException(400, "Only execution categories can be marked processed.");
        }

        await using var db = _dapperAccess.Open();
        await db.BeginSerializableAsync();
        var batch = await LockBatch(db, batchId);
        if (batch.State != "Editable")
        {
            throw new WorkflowException(409, "Reopen the batch before processing.");
        }

        var count = await db.ExecuteScalarAsync<int>(Procedure.CountCategoryTransfers, new { batchId, category });
        if (count == 0)
        {
            throw new WorkflowException(409, "Empty categories are NotApplicable.");
        }

        var changed = await db.ExecuteScalarAsync<int>(Procedure.MarkCategoryProcessed, new { batchId, category, actor = Operator });
        if (changed == 1)
        {
            await db.ExecuteAsync(Procedure.LockBatchAssignment, new { batchId });
            await WriteAudit(db, Operator, "CategoryProcessed", batch.ReceiptId, batchId, null, null, new { category });
        }

        await db.CommitAsync();
    }

    public async Task SaveTransferDecisions(Guid batchId, DecisionRequest request)
    {
        if (request.Decision is not ("Approved" or "Rejected"))
        {
            throw new WorkflowException(400, "Decision must be Approved or Rejected.");
        }

        var reason = request.Reason?.Trim();
        if (request.Decision == "Rejected" && reason?.Length > 2000)
        {
            throw new WorkflowException(400, "Rejection reason cannot exceed 2,000 characters.");
        }

        if (request.Decision == "Rejected" && string.IsNullOrWhiteSpace(reason))
        {
            throw new WorkflowException(400, "Rejection requires an explanation.");
        }

        if (request.Items.Count == 0 || request.Items.Count > 100 || request.Items.Any(item => item is null) || request.Items.Select(item => item.Id).Distinct().Count() != request.Items.Count)
        {
            throw new WorkflowException(400, "Select 1–100 distinct transfer IDs.");
        }

        await using var db = _dapperAccess.Open();
        await db.BeginSerializableAsync();
        var batch = await LockBatch(db, batchId);
        if (batch.State != "Editable")
        {
            throw new WorkflowException(409, "Reopen the batch before changing decisions.");
        }

        var rows = (await db.GetDecisionTransfersAsync<DecisionRow>(request.Items.Select(x => x.Id))).ToDictionary(x => x.Id);
        var failures = new List<object>();
        foreach (var item in request.Items)
        {
            rows.TryGetValue(item.Id, out var row);
            if (row is null || row.BatchId != batchId)
            {
                failures.Add(new { item.Id, error = "Transfer does not belong to batch." });
            }
            else if (row.Category == "INVALID")
            {
                failures.Add(new { item.Id, error = "Invalid-category decisions are system-controlled." });
            }
            else if (row.ProcessedUtc is null)
            {
                failures.Add(new { item.Id, error = "Category is not processed." });
            }
            else if (!row.Version.SequenceEqual(Version(item.Version)))
            {
                failures.Add(new { item.Id, error = "Stale version." });
            }
        }

        if (failures.Count > 0)
        {
            throw new WorkflowException(409, "No decisions changed; selection contains ineligible transfers.", failures);
        }

        foreach (var item in request.Items)
        {
            var row = rows[item.Id];
            await db.ExecuteAsync(Procedure.UpdateTransferDecision, new { id = item.Id, version = row.Version, decision = request.Decision, reason = request.Decision == "Rejected" ? reason : null, actor = Operator });
            await WriteAudit(db, Operator, "DecisionChanged", batch.ReceiptId, batchId, item.Id, null, new { from = row.Decision, previousReason = row.Reason, to = request.Decision, newReason = request.Decision == "Rejected" ? reason : null });
        }

        await db.ExecuteAsync(Procedure.BumpBatchVersion, new { batchId });
        await db.CommitAsync();
    }

    public async Task CorrectInstitution(Guid batchId, InstitutionRequest request)
    {
        var expected = Version(request.ExpectedBatchVersion);
        await using var db = _dapperAccess.Open();
        await db.BeginSerializableAsync();
        var batch = await LockBatch(db, batchId);
        if (!batch.Version.SequenceEqual(expected))
        {
            throw new WorkflowException(409, "Batch changed; refresh before correcting institution.");
        }

        if (batch.State != "Editable" || batch.AssignmentLockedUtc is not null)
        {
            throw new WorkflowException(409, "Institution assignment is locked.");
        }

        var target = await db.QuerySingleOrDefaultAsync<InstitutionRow>(Procedure.GetActiveInstitution, new { institutionId = request.InstitutionId });
        if (target is null)
        {
            throw new WorkflowException(404, "Target institution not found.");
        }

        if (target.Id == batch.InstitutionId)
        {
            await db.CommitAsync();
            return;
        }

        var receipt = await db.QuerySingleAsync<ReceiptForCorrection>(Procedure.GetReceiptChecksum, new { id = batch.ReceiptId });
        var duplicate = await db.ExecuteScalarAsync<Guid?>(Procedure.FindCanonicalReceipt, new { institutionId = target.Id, hash = receipt.Checksum });
        if (duplicate.HasValue)
        {
            throw new WorkflowException(409, "The target institution already has identical source content.", new { duplicateOf = duplicate });
        }

        var mapping = await db.QuerySingleOrDefaultAsync<MappingRow>(Procedure.GetResponseMapping, new { institutionId = target.Id, input = batch.InputCode });
        await db.ExecuteAsync(Procedure.AssignReceipt, new { targetId = target.Id, receiptId = batch.ReceiptId });
        await db.ExecuteAsync(Procedure.AssignBatch, new { targetId = target.Id, output = mapping?.OutputCode, revision = mapping?.Revision, batchId });
        await WriteAudit(db, Operator, "InstitutionCorrected", batch.ReceiptId, batchId, null, null, new { fromInstitutionId = batch.InstitutionId, toInstitutionId = target.Id, mapping?.OutputCode, mapping?.Revision });
        await db.CommitAsync();
    }

    public async Task<Guid> FinalizeBatch(Guid batchId, FinalizeRequest request)
    {
        if (request.OperationId == Guid.Empty)
        {
            throw new WorkflowException(400, "Operation ID is required.");
        }

        var expected = Version(request.ExpectedBatchVersion);
        await using var db = _dapperAccess.Open();
        await db.BeginSerializableAsync();
        var existing = await db.ExecuteScalarAsync<Guid?>(Procedure.FindReportOperation, new { operationId = request.OperationId });
        if (existing.HasValue)
        {
            await db.CommitAsync();
            return existing.Value;
        }

        var batch = await LockBatch(db, batchId);
        if (!batch.Version.SequenceEqual(expected))
        {
            throw new WorkflowException(409, "Batch changed; refresh before final submission.");
        }

        if (batch.State != "Editable")
        {
            throw new WorkflowException(409, "Batch is already finalized.");
        }

        var blockers = await Blockers(db, batchId);
        if (blockers.Count > 0)
        {
            throw new WorkflowException(409, "Batch is not ready for final submission.", blockers);
        }

        var rows = (await db.QueryAsync<ReportRow>(Procedure.ListReportTransfers, new { batchId })).ToList();
        var groups = rows.GroupBy(x => new { x.GroupId, x.GroupOrdinal, x.PaymentInfoId }).Select(x => new ReportGroup(x.Key.PaymentInfoId, x.Select(t => new ReportTransfer(t.InstructionId, t.EndToEndId, t.Decision, t.Reason)).ToList())).ToList();
        var revision = batch.CurrentRevision + 1;
        var docId = Guid.NewGuid();
        var messageId = $"PMD{docId:N}";
        byte[] bytes;
        try
        {
            bytes = _isoService.Write(batch.OutputCode!, messageId, batch.OriginalMessageId ?? "UNKNOWN", batch.InputCode, batch.OriginalCreation, groups, DateTime.UtcNow);
        }
        catch (IsoException e)
        {
            throw new WorkflowException(422, "Generated report failed XSD validation.", e.Errors);
        }

        await db.ExecuteAsync(Procedure.InsertStatusReport, new { docId, receiptId = batch.ReceiptId, batchId, revision, operationId = request.OperationId, institutionId = batch.InstitutionId, outputCode = batch.OutputCode, messageId, filename = $"PAIN002_{batchId:N}_R{revision}_{docId:N}.xml", bytes, hash = SHA256.HashData(bytes) });
        await db.ExecuteAsync(Procedure.FinalizeBatch, new { batchId, revision });
        await WriteAudit(db, Operator, "Finalized", batch.ReceiptId, batchId, null, docId, new { revision, messageId });
        await db.CommitAsync();
        return docId;
    }

    public async Task ReopenBatch(Guid batchId)
    {
        await using var db = _dapperAccess.Open();
        await db.BeginSerializableAsync();
        var batch = await LockBatch(db, batchId);
        if (batch.State != "Finalized")
        {
            throw new WorkflowException(409, "Only finalized batches can be reopened.");
        }

        var publishing = await db.ExecuteScalarAsync<int>(Procedure.CountPublishingReports, new { batchId });
        if (publishing > 0)
        {
            throw new WorkflowException(409, "Publication is in progress; retry after it completes.");
        }

        await db.ExecuteAsync(Procedure.SupersedeUnpublishedReports, new { batchId });
        await db.ExecuteAsync(Procedure.ReopenBatch, new { batchId });
        await WriteAudit(db, Operator, "Reopened", batch.ReceiptId, batchId, null, null, new { batch.CurrentRevision });
        await db.CommitAsync();
    }

    public async Task<IEnumerable<ReportSummary>> GetReports(Guid batchId)
    {
        await using var db = _dapperAccess.Open();
        return await db.QueryAsync<ReportSummary>(Procedure.ListBatchReports, new { batchId });
    }

    public async Task<ReportDocument?> GetReportDocument(Guid id)
    {
        await using var db = _dapperAccess.Open();
        var document = await db.QuerySingleOrDefaultAsync<ReportDocument>(Procedure.GetOutgoingDocument, new { id });
        if (document is not null)
        {
            await WriteAudit(db, Operator, "Downloaded", document.ReceiptId, document.BatchId, null, id, null);
        }

        return document;
    }

    public async Task RetryPublication(Guid id)
    {
        await using var db = _dapperAccess.Open();
        var changed = await db.ExecuteScalarAsync<int>(Procedure.RetryPublication, new { id });
        if (changed == 0)
        {
            throw new WorkflowException(409, "Only failed publications can be retried.");
        }
    }

    public async Task<IEnumerable<BatchAuditEntry>> GetAudit(Guid batchId)
    {
        await using var db = _dapperAccess.Open();
        return await db.QueryAsync<BatchAuditEntry>(Procedure.ListBatchAudit, new { batchId });
    }

    private static async Task<List<string>> Blockers(DalSession db, Guid id)
    {
        var blockers = new List<string>();
        var state = await db.QuerySingleOrDefaultAsync<BatchReadiness>(Procedure.GetBatchReadiness, new { id });
        if (state is null)
        {
            return["Batch does not exist."];
        }

        if (state.State != "Editable")
        {
            blockers.Add("Batch is not editable.");
        }

        if (state.OutputCode is null)
        {
            blockers.Add("Response version mapping is missing.");
        }

        var pending = await db.ExecuteScalarAsync<int>(Procedure.CountPendingTransfers, new { id });
        if (pending > 0)
        {
            blockers.Add($"{pending} transfers are pending.");
        }

        var unprocessed = await db.QueryAsync<string>(Procedure.ListUnprocessedCategories, new { id });
        blockers.AddRange(unprocessed.Select(x => $"{x} category is not processed."));
        return blockers;
    }

    private static async Task<BatchLock> LockBatch(DalSession db, Guid id) => await db.QuerySingleOrDefaultAsync<BatchLock>(Procedure.LockBatch, new { id }) ?? throw new WorkflowException(404, "Batch not found.");
    private sealed class OriginalRow
    {
        public byte[] Bytes { get; set; } = [];
        public string Filename { get; set; } = "";
    }

    private sealed class ReceiptForCorrection
    {
        public byte[] Checksum { get; set; } = [];
    }

    private sealed class BatchReadiness
    {
        public string State { get; set; } = "";
        public string? OutputCode { get; set; }
    }

    private sealed class BatchLock
    {
        public Guid Id { get; set; }
        public Guid ReceiptId { get; set; }
        public int InstitutionId { get; set; }
        public string InputCode { get; set; } = "";
        public string? OutputCode { get; set; }
        public string? OriginalMessageId { get; set; }
        public string? OriginalCreation { get; set; }
        public string State { get; set; } = "";
        public int CurrentRevision { get; set; }
        public DateTime? AssignmentLockedUtc { get; set; }
        public byte[] Version { get; set; } = [];
    }

    private sealed class DecisionRow
    {
        public Guid Id { get; set; }
        public Guid BatchId { get; set; }
        public string Category { get; set; } = "";
        public string Decision { get; set; } = "";
        public string? Reason { get; set; }
        public byte[] Version { get; set; } = [];
        public DateTime? ProcessedUtc { get; set; }
    }

    private sealed class ReportRow
    {
        public Guid GroupId { get; set; }
        public int GroupOrdinal { get; set; }
        public string? PaymentInfoId { get; set; }
        public int TransferOrdinal { get; set; }
        public string? InstructionId { get; set; }
        public string? EndToEndId { get; set; }
        public string Decision { get; set; } = "";
        public string? Reason { get; set; }
    }
}
