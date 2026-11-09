using System.Data;
using System.Security.Cryptography;
using System.Xml.Linq;
using Dapper;
using PaymentManagement.Api.Iso20022;
using PaymentManagement.Api.Persistence;

namespace PaymentManagement.Api.Payments;

public sealed record DecisionItem(Guid Id, string Version);
public sealed record DecisionRequest(List<DecisionItem> Items, string Decision, string? Reason);
public sealed record MappingRequest(string InputCode, string OutputCode);
public sealed record RuleRequest(string Currency, decimal Threshold);
public sealed record InstitutionRequest(int InstitutionId, string ExpectedBatchVersion);
public sealed record FinalizeRequest(Guid OperationId, string ExpectedBatchVersion);

public sealed class WorkflowService(PaymentStore store, IsoService iso)
{
    public async Task CorrectReceiptInstitution(Guid receiptId, InstitutionRequest request)
    {
        await using var db = store.Open();
        var batchId = await db.ExecuteScalarAsync<Guid?>("SELECT id FROM dbo.t_payment_batches WHERE receipt_id=@receiptId", new { receiptId });
        if (!batchId.HasValue) throw new WorkflowException(404, "Imported batch not found for receipt.");
        await CorrectInstitution(batchId.Value, request);
    }
    private static byte[] Version(string value)
    {
        try { var bytes = Convert.FromHexString(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value); if (bytes.Length == 8) return bytes; }
        catch (FormatException) { }
        throw new WorkflowException(400, "Expected rowversion must be a 16-digit hexadecimal value.");
    }
    public async Task<IEnumerable<dynamic>> Receipts(int? institutionId, string? state, int skip, int take, string? search = null)
    {
        await using var db = store.Open();
        var rows = await db.QueryAsync(@"SELECT r.id,r.original_filename AS originalFilename,ri.code AS intakeInstitution,ei.code AS effectiveInstitution,
 r.channel,r.received_utc AS receivedUtc,r.import_state AS importState,r.location_state AS locationState,
 r.error_type AS errorType,r.duplicate_of AS duplicateOf,b.id AS batchId,b.state AS batchState,
 r.detected_definition AS detectedDefinition,r.source_bytes AS sourceBytes,b.output_code AS outputCode,
 (SELECT COUNT(*) FROM dbo.t_transfers t WHERE t.batch_id=b.id) AS transferCount,
 (SELECT COUNT(*) FROM dbo.t_transfers t WHERE t.batch_id=b.id AND t.decision='Pending') AS pendingCount
 FROM dbo.t_file_receipts r JOIN dbo.t_institutions ri ON ri.id=r.intake_institution_id
 JOIN dbo.t_institutions ei ON ei.id=r.effective_institution_id
 LEFT JOIN dbo.t_payment_batches b ON b.receipt_id=r.id
 WHERE (@institutionId IS NULL OR r.effective_institution_id=@institutionId) AND (@state IS NULL OR r.import_state=@state OR b.state=@state)
 AND (@pattern IS NULL OR r.original_filename LIKE @pattern OR b.original_message_id LIKE @pattern)
 ORDER BY r.received_utc DESC,r.id DESC OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", new { institutionId, state,
            pattern = string.IsNullOrWhiteSpace(search) ? null : "%" + search.Trim().Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%",
            skip = Math.Max(0, skip), take = Math.Clamp(take, 1, 100) });
        return rows.Select(WithGroupHeader).ToArray();
    }
    public async Task<dynamic?> Receipt(Guid id)
    {
        await using var db = store.Open();
        var row = await db.QuerySingleOrDefaultAsync(@"SELECT r.id,r.original_filename AS originalFilename,r.original_path AS originalPath,r.current_path AS currentPath,
 ri.code AS intakeInstitution,ei.code AS effectiveInstitution,r.channel,r.received_utc AS receivedUtc,r.byte_length AS byteLength,
 CONVERT(varchar(64),r.checksum,2) AS checksum,r.import_state AS importState,r.location_state AS locationState,
 r.detected_definition AS detectedDefinition,r.source_bytes AS sourceBytes,r.error_type AS errorType,r.validation_json AS validationJson,r.duplicate_of AS duplicateOf,
 b.id AS batchId,e.id AS technicalDocumentId,e.publication_state AS technicalPublicationState,
 CONVERT(varchar(18),CONVERT(varbinary(8),r.rv),1) AS version
 FROM dbo.t_file_receipts r JOIN dbo.t_institutions ri ON ri.id=r.intake_institution_id
 JOIN dbo.t_institutions ei ON ei.id=r.effective_institution_id LEFT JOIN dbo.t_payment_batches b ON b.receipt_id=r.id
 LEFT JOIN dbo.t_outgoing_documents e ON e.receipt_id=r.id AND e.kind='ImportErrorJson' WHERE r.id=@id", new { id });
        return row is null ? null : WithGroupHeader(row);
    }
    private static dynamic WithGroupHeader(object row)
    {
        var result = ((IDictionary<string, object>)row).ToDictionary(x => x.Key, x => (object?)x.Value);
        result["groupHeader"] = GroupHeaderReader.Read((byte[])result["sourceBytes"]!);
        result.Remove("sourceBytes");
        return result;
    }
    public async Task<(byte[] Bytes, string Filename)?> Original(Guid id)
    {
        await using var db = store.Open();
        var row = await db.QuerySingleOrDefaultAsync<OriginalRow>("SELECT source_bytes AS Bytes,original_filename AS Filename FROM dbo.t_file_receipts WHERE id=@id", new { id });
        return row is null ? null : (row.Bytes, row.Filename);
    }
    public async Task<dynamic?> Batch(Guid id)
    {
        await using var db = store.Open();
        var batch = await db.QuerySingleOrDefaultAsync(@"SELECT b.id,b.receipt_id AS receiptId,i.code AS institution,b.input_code AS inputCode,
 b.output_code AS outputCode,b.mapping_revision AS mappingRevision,b.original_message_id AS originalMessageId,
 b.original_creation AS originalCreation,b.state,b.current_revision AS currentRevision,b.assignment_locked_utc AS assignmentLockedUtc,
 b.created_utc AS createdUtc,CONVERT(varchar(18),CONVERT(varbinary(8),b.rv),1) AS version,
 (SELECT COUNT(*) FROM dbo.t_transfers t WHERE t.batch_id=b.id) AS transferCount,
 (SELECT COUNT(*) FROM dbo.t_transfers t WHERE t.batch_id=b.id AND t.decision='Pending') AS pendingCount,
 (SELECT COUNT(*) FROM dbo.t_transfers t WHERE t.batch_id=b.id AND t.decision='Rejected' AND t.decision_source='System') AS systemRejectedCount,
 (SELECT COUNT(*) FROM dbo.t_transfers t WHERE t.batch_id=b.id AND t.decision='Rejected' AND t.decision_source='Operator') AS operatorRejectedCount
 FROM dbo.t_payment_batches b JOIN dbo.t_institutions i ON i.id=b.effective_institution_id WHERE b.id=@id", new { id });
        if (batch is null) return null;
        var categories = await db.QueryAsync(@"SELECT c.category,COUNT(t.id) AS transferCount,
 CASE WHEN c.category='INVALID' THEN 'NotRequired' WHEN COUNT(t.id)=0 THEN 'NotApplicable'
 WHEN c.processed_utc IS NULL THEN 'NotProcessed' ELSE 'Processed' END AS processingState,
 c.processed_utc AS processedUtc,c.processed_actor AS processedActor
 FROM dbo.t_batch_categories c LEFT JOIN dbo.t_transfers t ON t.batch_id=c.batch_id AND t.category=c.category
 WHERE c.batch_id=@id GROUP BY c.category,c.processed_utc,c.processed_actor
 ORDER BY CASE c.category WHEN 'LOCAL' THEN 1 WHEN 'ACH' THEN 2 WHEN 'RTGS' THEN 3 WHEN 'SWIFT' THEN 4 ELSE 5 END", new { id });
        var blockers = await Blockers(db, id);
        var currencyTotals = await db.QueryAsync(@"SELECT currency,CONVERT(varchar(50),SUM(amount)) AS amount
 FROM (SELECT instructed_currency AS currency,instructed_amount AS amount FROM dbo.t_transfers WHERE batch_id=@id AND instructed_amount IS NOT NULL
 UNION ALL SELECT equivalent_currency,equivalent_amount FROM dbo.t_transfers WHERE batch_id=@id AND equivalent_amount IS NOT NULL) amounts
 GROUP BY currency ORDER BY currency", new { id });
        return new { batch, categories, currencyTotals, blockers, canFinalize = blockers.Count == 0 };
    }
    public async Task<IEnumerable<dynamic>> Groups(Guid id)
    {
        await using var db = store.Open();
        return await db.QueryAsync(@"SELECT g.id,g.source_ordinal AS sourceOrdinal,g.payment_info_id AS paymentInfoId,g.debtor_name AS debtorName,
 g.debtor_account_id AS debtorAccount,g.debtor_account_kind AS debtorAccountKind,g.debtor_account_currency AS debtorCurrency,
 g.debtor_agent AS debtorAgent,g.requested_execution_date AS executionDate,COUNT(t.id) AS transferCount
 FROM dbo.t_payment_groups g LEFT JOIN dbo.t_transfers t ON t.group_id=g.id WHERE g.batch_id=@id
 GROUP BY g.id,g.source_ordinal,g.payment_info_id,g.debtor_name,g.debtor_account_id,g.debtor_account_kind,g.debtor_account_currency,g.debtor_agent,g.requested_execution_date
 ORDER BY g.source_ordinal", new { id });
    }
    public async Task<IEnumerable<dynamic>> Transfers(Guid id, string? category, string? decision, Guid? groupId, int skip, int take)
    {
        await using var db = store.Open();
        return await db.QueryAsync(@"SELECT t.id,t.group_id AS groupId,g.source_ordinal AS groupOrdinal,t.source_ordinal AS sourceOrdinal,
 g.payment_info_id AS paymentInfoId,g.debtor_account_id AS debtorAccount,t.instruction_id AS instructionId,t.end_to_end_id AS endToEndId,
 t.creditor_name AS creditorName,t.creditor_account_id AS creditorAccount,t.creditor_account_kind AS creditorAccountKind,
 CONVERT(varchar(50),COALESCE(t.instructed_amount,t.equivalent_amount)) AS amount,
 COALESCE(t.instructed_currency,t.equivalent_currency) AS currency,t.transfer_currency AS transferCurrency,
 CASE WHEN t.instructed_amount IS NOT NULL THEN 'Instructed' ELSE 'Equivalent' END AS amountBasis,
 t.category,t.decision,t.decision_source AS decisionSource,t.decision_reason AS decisionReason,
 t.routing_code AS routingCode,t.routing_explanation AS routingExplanation,CONVERT(varchar(18),CONVERT(varbinary(8),t.rv),1) AS version
 FROM dbo.t_transfers t JOIN dbo.t_payment_groups g ON g.id=t.group_id WHERE t.batch_id=@id
 AND (@category IS NULL OR t.category=@category) AND (@decision IS NULL OR t.decision=@decision)
 AND (@groupId IS NULL OR t.group_id=@groupId)
 ORDER BY g.source_ordinal,t.source_ordinal OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
            new { id, category, decision, groupId, skip = Math.Max(0, skip), take = Math.Clamp(take, 1, 100) });
    }
    public async Task<dynamic?> Transfer(Guid id)
    {
        await using var db = store.Open();
        var row = await db.QuerySingleOrDefaultAsync(@"SELECT t.id,t.batch_id AS batchId,t.group_id AS groupId,g.source_ordinal AS groupOrdinal,
 t.source_ordinal AS sourceOrdinal,g.payment_info_id AS paymentInfoId,g.debtor_name AS debtorName,g.debtor_account_id AS debtorAccount,
 t.instruction_id AS instructionId,t.end_to_end_id AS endToEndId,t.creditor_name AS creditorName,
 t.creditor_account_id AS creditorAccount,t.creditor_account_kind AS creditorAccountKind,t.creditor_agent AS creditorAgent,
 CONVERT(varchar(50),t.instructed_amount) AS instructedAmount,t.instructed_currency AS instructedCurrency,
 CONVERT(varchar(50),t.equivalent_amount) AS equivalentAmount,t.equivalent_currency AS equivalentCurrency,
 t.transfer_currency AS transferCurrency,t.account_currency AS accountCurrency,CONVERT(varchar(50),t.routing_amount) AS routingAmount,
 t.iban_country AS ibanCountry,t.iban_bank_code AS ibanBankCode,t.rule_revision AS ruleRevision,t.routing_code AS routingCode,
 t.routing_explanation AS routingExplanation,t.category,t.decision,t.decision_source AS decisionSource,
 t.decision_reason AS decisionReason,t.decision_actor AS decisionActor,t.decision_utc AS decisionUtc,
 t.source_xml AS sourceXml,CONVERT(varchar(18),CONVERT(varbinary(8),t.rv),1) AS version
 FROM dbo.t_transfers t JOIN dbo.t_payment_groups g ON g.id=t.group_id WHERE t.id=@id", new { id });
        if (row is null) return null;
        var result = ((IDictionary<string, object>)row).ToDictionary(x => x.Key, x => (object?)x.Value);
        var source = XDocument.Parse((string)result["sourceXml"]!);
        result["remittances"] = source.Descendants().Where(x => x.Name.LocalName == "RmtInf")
            .SelectMany(x => x.Elements().Select(child => new { kind = child.Name.LocalName, value = child.Value, xml = child.ToString(SaveOptions.DisableFormatting) })).ToArray();
        result["history"] = await db.QueryAsync(@"SELECT occurred_utc AS occurredUtc,actor,action,details_json AS detailsJson
 FROM dbo.t_workflow_audit WHERE transfer_id=@id ORDER BY id", new { id });
        return result;
    }
    public async Task Process(Guid batchId, string category)
    {
        category = category.ToUpperInvariant();
        if (category is not ("LOCAL" or "ACH" or "RTGS" or "SWIFT")) throw new WorkflowException(400, "Only execution categories can be marked processed.");
        await using var db = store.Open(); await db.OpenAsync();
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var batch = await LockBatch(db, tx, batchId);
        if (batch.State != "Editable") throw new WorkflowException(409, "Reopen the batch before processing.");
        var count = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.t_transfers WHERE batch_id=@batchId AND category=@category", new { batchId, category }, tx);
        if (count == 0) throw new WorkflowException(409, "Empty categories are NotApplicable.");
        var changed = await db.ExecuteAsync(@"UPDATE dbo.t_batch_categories SET processed_utc=SYSUTCDATETIME(),processed_actor=@actor
 WHERE batch_id=@batchId AND category=@category AND processed_utc IS NULL", new { batchId, category, actor = store.Operator }, tx);
        if (changed == 1)
        {
            await db.ExecuteAsync("UPDATE dbo.t_payment_batches SET assignment_locked_utc=COALESCE(assignment_locked_utc,SYSUTCDATETIME()) WHERE id=@batchId", new { batchId }, tx);
            await PaymentStore.Audit(db, tx, store.Operator, "CategoryProcessed", batch.ReceiptId, batchId, null, null, new { category });
        }
        tx.Commit();
    }
    public async Task Decide(Guid batchId, DecisionRequest request)
    {
        if (request.Decision is not ("Approved" or "Rejected")) throw new WorkflowException(400, "Decision must be Approved or Rejected.");
        var reason = request.Reason?.Trim();
        if (request.Decision == "Rejected" && string.IsNullOrWhiteSpace(reason)) throw new WorkflowException(400, "Rejection requires an explanation.");
        if (request.Items.Count == 0 || request.Items.Count > 100 || request.Items.Select(x => x.Id).Distinct().Count() != request.Items.Count)
            throw new WorkflowException(400, "Select 1–100 distinct transfer IDs.");
        await using var db = store.Open(); await db.OpenAsync();
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var batch = await LockBatch(db, tx, batchId);
        if (batch.State != "Editable") throw new WorkflowException(409, "Reopen the batch before changing decisions.");
        var rows = (await db.QueryAsync<DecisionRow>(@"SELECT t.id,t.batch_id AS BatchId,t.category,t.decision,t.decision_reason AS Reason,
 t.rv AS Version,c.processed_utc AS ProcessedUtc FROM dbo.t_transfers t
 JOIN dbo.t_batch_categories c ON c.batch_id=t.batch_id AND c.category=t.category WHERE t.id IN @ids",
            new { ids = request.Items.Select(x => x.Id).ToArray() }, tx)).ToDictionary(x => x.Id);
        var failures = new List<object>();
        foreach (var item in request.Items)
        {
            rows.TryGetValue(item.Id, out var row);
            if (row is null || row.BatchId != batchId) failures.Add(new { item.Id, error = "Transfer does not belong to batch." });
            else if (row.Category == "INVALID") failures.Add(new { item.Id, error = "Invalid-category decisions are system-controlled." });
            else if (row.ProcessedUtc is null) failures.Add(new { item.Id, error = "Category is not processed." });
            else if (!row.Version.SequenceEqual(Version(item.Version))) failures.Add(new { item.Id, error = "Stale version." });
        }
        if (failures.Count > 0) throw new WorkflowException(409, "No decisions changed; selection contains ineligible transfers.", failures);
        foreach (var item in request.Items)
        {
            var row = rows[item.Id];
            await db.ExecuteAsync(@"UPDATE dbo.t_transfers SET decision=@decision,decision_source='Operator',decision_reason=@reason,
 decision_actor=@actor,decision_utc=SYSUTCDATETIME() WHERE id=@id AND rv=@version",
                new { id = item.Id, version = row.Version, decision = request.Decision, reason = request.Decision == "Rejected" ? reason : null, actor = store.Operator }, tx);
            await PaymentStore.Audit(db, tx, store.Operator, "DecisionChanged", batch.ReceiptId, batchId, item.Id, null,
                new { from = row.Decision, previousReason = row.Reason, to = request.Decision, newReason = request.Decision == "Rejected" ? reason : null });
        }
        await db.ExecuteAsync("UPDATE dbo.t_payment_batches SET state=state WHERE id=@batchId", new { batchId }, tx);
        tx.Commit();
    }
    public async Task CorrectInstitution(Guid batchId, InstitutionRequest request)
    {
        var expected = Version(request.ExpectedBatchVersion);
        await using var db = store.Open(); await db.OpenAsync();
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var batch = await LockBatch(db, tx, batchId);
        if (!batch.Version.SequenceEqual(expected)) throw new WorkflowException(409, "Batch changed; refresh before correcting institution.");
        if (batch.State != "Editable" || batch.AssignmentLockedUtc is not null) throw new WorkflowException(409, "Institution assignment is locked.");
        var target = await db.QuerySingleOrDefaultAsync<InstitutionRow>("SELECT id,code FROM dbo.t_institutions WHERE id=@id AND is_active=1", new { id = request.InstitutionId }, tx);
        if (target is null) throw new WorkflowException(404, "Target institution not found.");
        if (target.Id == batch.InstitutionId) { tx.Commit(); return; }
        var receipt = await db.QuerySingleAsync<ReceiptForCorrection>("SELECT checksum FROM dbo.t_file_receipts WHERE id=@id", new { id = batch.ReceiptId }, tx);
        var duplicate = await db.ExecuteScalarAsync<Guid?>(@"SELECT TOP(1) id FROM dbo.t_file_receipts WITH(UPDLOCK,HOLDLOCK)
 WHERE effective_institution_id=@targetId AND canonical_checksum=@checksum", new { targetId = target.Id, receipt.Checksum }, tx);
        if (duplicate.HasValue) throw new WorkflowException(409, "The target institution already has identical source content.", new { duplicateOf = duplicate });
        var mapping = await db.QuerySingleOrDefaultAsync<MappingRow>("SELECT output_code AS OutputCode,revision FROM dbo.t_response_mappings WHERE institution_id=@institutionId AND input_code=@inputCode",
            new { institutionId = target.Id, inputCode = batch.InputCode }, tx);
        await db.ExecuteAsync("UPDATE dbo.t_file_receipts SET effective_institution_id=@targetId WHERE id=@receiptId", new { targetId = target.Id, receiptId = batch.ReceiptId }, tx);
        await db.ExecuteAsync(@"UPDATE dbo.t_payment_batches SET effective_institution_id=@targetId,output_code=@output,mapping_revision=@revision WHERE id=@batchId",
            new { targetId = target.Id, output = mapping?.OutputCode, revision = mapping?.Revision, batchId }, tx);
        await PaymentStore.Audit(db, tx, store.Operator, "InstitutionCorrected", batch.ReceiptId, batchId, null, null,
            new { fromInstitutionId = batch.InstitutionId, toInstitutionId = target.Id, mapping?.OutputCode, mapping?.Revision });
        tx.Commit();
    }
    public async Task<Guid> Finalize(Guid batchId, FinalizeRequest request)
    {
        if (request.OperationId == Guid.Empty) throw new WorkflowException(400, "Operation ID is required.");
        var expected = Version(request.ExpectedBatchVersion);
        await using var db = store.Open(); await db.OpenAsync();
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var existing = await db.ExecuteScalarAsync<Guid?>("SELECT id FROM dbo.t_outgoing_documents WHERE operation_id=@operationId", new { operationId = request.OperationId }, tx);
        if (existing.HasValue) { tx.Commit(); return existing.Value; }
        var batch = await LockBatch(db, tx, batchId);
        if (!batch.Version.SequenceEqual(expected)) throw new WorkflowException(409, "Batch changed; refresh before final submission.");
        if (batch.State != "Editable") throw new WorkflowException(409, "Batch is already finalized.");
        var blockers = await Blockers(db, batchId, tx);
        if (blockers.Count > 0) throw new WorkflowException(409, "Batch is not ready for final submission.", blockers);
        var rows = (await db.QueryAsync<ReportRow>(@"SELECT g.id AS GroupId,g.source_ordinal AS GroupOrdinal,g.payment_info_id AS PaymentInfoId,
 t.source_ordinal AS TransferOrdinal,t.instruction_id AS InstructionId,t.end_to_end_id AS EndToEndId,t.decision,t.decision_reason AS Reason
 FROM dbo.t_payment_groups g JOIN dbo.t_transfers t ON t.group_id=g.id WHERE g.batch_id=@batchId ORDER BY g.source_ordinal,t.source_ordinal",
            new { batchId }, tx)).ToList();
        var groups = rows.GroupBy(x => new { x.GroupId, x.GroupOrdinal, x.PaymentInfoId })
            .Select(x => new ReportGroup(x.Key.PaymentInfoId, x.Select(t => new ReportTransfer(t.InstructionId, t.EndToEndId, t.Decision, t.Reason)).ToList())).ToList();
        var revision = batch.CurrentRevision + 1;
        var docId = Guid.NewGuid();
        var messageId = $"PMD{docId:N}";
        byte[] bytes;
        try { bytes = iso.Write(batch.OutputCode!, messageId, batch.OriginalMessageId ?? "UNKNOWN", batch.InputCode, batch.OriginalCreation, groups, DateTime.UtcNow); }
        catch (IsoException e) { throw new WorkflowException(422, "Generated report failed XSD validation.", e.Errors); }
        await db.ExecuteAsync(@"INSERT dbo.t_outgoing_documents(id,kind,receipt_id,batch_id,revision,operation_id,recipient_institution_id,iso_definition,message_id,filename,mime_type,content_bytes,content_sha256,generated_utc,publication_state)
 VALUES(@docId,'Pain002',@receiptId,@batchId,@revision,@operationId,@institutionId,@outputCode,@messageId,@filename,'application/xml',@bytes,@hash,SYSUTCDATETIME(),'Pending')",
            new { docId, receiptId = batch.ReceiptId, batchId, revision, operationId = request.OperationId,
                institutionId = batch.InstitutionId, outputCode = batch.OutputCode, messageId,
                filename = $"PAIN002_{batchId:N}_R{revision}_{docId:N}.xml", bytes, hash = SHA256.HashData(bytes) }, tx);
        await db.ExecuteAsync("UPDATE dbo.t_payment_batches SET state='Finalized',current_revision=@revision WHERE id=@batchId", new { batchId, revision }, tx);
        await PaymentStore.Audit(db, tx, store.Operator, "Finalized", batch.ReceiptId, batchId, null, docId, new { revision, messageId });
        tx.Commit();
        return docId;
    }
    public async Task Reopen(Guid batchId)
    {
        await using var db = store.Open(); await db.OpenAsync();
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var batch = await LockBatch(db, tx, batchId);
        if (batch.State != "Finalized") throw new WorkflowException(409, "Only finalized batches can be reopened.");
        var publishing = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.t_outgoing_documents WHERE batch_id=@batchId AND publication_state='Publishing'", new { batchId }, tx);
        if (publishing > 0) throw new WorkflowException(409, "Publication is in progress; retry after it completes.");
        await db.ExecuteAsync("UPDATE dbo.t_outgoing_documents SET publication_state='SupersededUnpublished' WHERE batch_id=@batchId AND publication_state IN ('Pending','Failed')", new { batchId }, tx);
        await db.ExecuteAsync("UPDATE dbo.t_payment_batches SET state='Editable' WHERE id=@batchId", new { batchId }, tx);
        await PaymentStore.Audit(db, tx, store.Operator, "Reopened", batch.ReceiptId, batchId, null, null, new { batch.CurrentRevision });
        tx.Commit();
    }
    public async Task<IEnumerable<dynamic>> Reports(Guid batchId)
    {
        await using var db = store.Open();
        return await db.QueryAsync(@"SELECT d.id,d.revision,d.iso_definition AS isoDefinition,d.message_id AS messageId,
 i.code AS recipient,d.filename,d.generated_utc AS generatedUtc,d.publication_state AS publicationState,
 d.published_utc AS publishedUtc,d.retry_count AS retryCount,d.last_error AS lastError
 FROM dbo.t_outgoing_documents d JOIN dbo.t_institutions i ON i.id=d.recipient_institution_id
 WHERE d.batch_id=@batchId ORDER BY d.revision DESC", new { batchId });
    }
    public async Task<ReportDocument?> Document(Guid id)
    {
        await using var db = store.Open();
        var document = await db.QuerySingleOrDefaultAsync<ReportDocument>(@"SELECT d.id,d.kind,d.receipt_id AS ReceiptId,d.batch_id AS BatchId,
 d.filename,d.mime_type AS MimeType,d.content_bytes AS Bytes,d.content_sha256 AS Hash,d.publication_state AS PublicationState,
 i.code AS InstitutionCode FROM dbo.t_outgoing_documents d JOIN dbo.t_institutions i ON i.id=d.recipient_institution_id WHERE d.id=@id", new { id });
        if (document is not null)
            await db.ExecuteAsync(@"INSERT dbo.t_workflow_audit(actor,action,receipt_id,batch_id,document_id)
 VALUES(@actor,'Downloaded',@receiptId,@batchId,@id)", new { id, actor = store.Operator, receiptId = document.ReceiptId, batchId = document.BatchId });
        return document;
    }
    public async Task RetryPublication(Guid id)
    {
        await using var db = store.Open();
        var changed = await db.ExecuteAsync(@"UPDATE dbo.t_outgoing_documents SET publication_state='Pending',last_error=NULL
 WHERE id=@id AND publication_state='Failed'", new { id });
        if (changed == 0) throw new WorkflowException(409, "Only failed publications can be retried.");
    }
    public async Task<IEnumerable<dynamic>> Audit(Guid batchId)
    {
        await using var db = store.Open();
        return await db.QueryAsync(@"SELECT id,occurred_utc AS occurredUtc,actor,action,receipt_id AS receiptId,
 transfer_id AS transferId,document_id AS documentId,details_json AS detailsJson
 FROM dbo.t_workflow_audit WHERE batch_id=@batchId ORDER BY id", new { batchId });
    }
    private static async Task<List<string>> Blockers(Microsoft.Data.SqlClient.SqlConnection db, Guid id, Microsoft.Data.SqlClient.SqlTransaction? tx = null)
    {
        var blockers = new List<string>();
        var state = await db.QuerySingleOrDefaultAsync<BatchReadiness>("SELECT state,output_code AS OutputCode FROM dbo.t_payment_batches WHERE id=@id", new { id }, tx);
        if (state is null) return ["Batch does not exist."];
        if (state.State != "Editable") blockers.Add("Batch is not editable.");
        if (state.OutputCode is null) blockers.Add("Response version mapping is missing.");
        var pending = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.t_transfers WHERE batch_id=@id AND decision='Pending'", new { id }, tx);
        if (pending > 0) blockers.Add($"{pending} transfers are pending.");
        var unprocessed = await db.QueryAsync<string>(@"SELECT c.category FROM dbo.t_batch_categories c WHERE c.batch_id=@id
 AND c.category<>'INVALID' AND c.processed_utc IS NULL AND EXISTS(SELECT 1 FROM dbo.t_transfers t WHERE t.batch_id=c.batch_id AND t.category=c.category)", new { id }, tx);
        blockers.AddRange(unprocessed.Select(x => $"{x} category is not processed."));
        return blockers;
    }
    private static async Task<BatchLock> LockBatch(Microsoft.Data.SqlClient.SqlConnection db, Microsoft.Data.SqlClient.SqlTransaction tx, Guid id) =>
        await db.QuerySingleOrDefaultAsync<BatchLock>(@"SELECT id,receipt_id AS ReceiptId,effective_institution_id AS InstitutionId,input_code AS InputCode,
 output_code AS OutputCode,original_message_id AS OriginalMessageId,original_creation AS OriginalCreation,
 state,current_revision AS CurrentRevision,assignment_locked_utc AS AssignmentLockedUtc,rv AS Version
 FROM dbo.t_payment_batches WITH(UPDLOCK,HOLDLOCK) WHERE id=@id", new { id }, tx)
        ?? throw new WorkflowException(404, "Batch not found.");
    private sealed class OriginalRow { public byte[] Bytes { get; set; } = []; public string Filename { get; set; } = ""; }
    private sealed class ReceiptForCorrection { public byte[] Checksum { get; set; } = []; }
    private sealed class BatchReadiness { public string State { get; set; } = ""; public string? OutputCode { get; set; } }
    private sealed class BatchLock
    {
        public Guid Id { get; set; } public Guid ReceiptId { get; set; } public int InstitutionId { get; set; }
        public string InputCode { get; set; } = ""; public string? OutputCode { get; set; }
        public string? OriginalMessageId { get; set; } public string? OriginalCreation { get; set; }
        public string State { get; set; } = ""; public int CurrentRevision { get; set; }
        public DateTime? AssignmentLockedUtc { get; set; } public byte[] Version { get; set; } = [];
    }
    private sealed class DecisionRow
    {
        public Guid Id { get; set; } public Guid BatchId { get; set; } public string Category { get; set; } = "";
        public string Decision { get; set; } = ""; public string? Reason { get; set; }
        public byte[] Version { get; set; } = []; public DateTime? ProcessedUtc { get; set; }
    }
    private sealed class ReportRow
    {
        public Guid GroupId { get; set; } public int GroupOrdinal { get; set; } public string? PaymentInfoId { get; set; }
        public int TransferOrdinal { get; set; } public string? InstructionId { get; set; } public string? EndToEndId { get; set; }
        public string Decision { get; set; } = ""; public string? Reason { get; set; }
    }
}
public sealed class ReportDocument
{
    public Guid Id { get; set; } public string Kind { get; set; } = ""; public Guid ReceiptId { get; set; }
    public Guid? BatchId { get; set; } public string Filename { get; set; } = ""; public string MimeType { get; set; } = "";
    public byte[] Bytes { get; set; } = []; public byte[] Hash { get; set; } = [];
    public string PublicationState { get; set; } = ""; public string InstitutionCode { get; set; } = "";
}
