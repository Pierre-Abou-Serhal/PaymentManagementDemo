using System.Security.Cryptography;
using Dapper;
using PaymentManagement.Api.Payments;
using PaymentManagement.Api.Persistence;

namespace PaymentManagement.Api.Delivery;

public sealed class OutboxPublisher(PaymentStore store, IConfiguration config, ILogger<OutboxPublisher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await PublishAvailable(stoppingToken); }
            catch (Exception e) { logger.LogError(e, "Outbox publisher failed; retrying"); }
            await Task.Delay(TimeSpan.FromSeconds(config.GetValue<int?>("Demo:PublishSeconds") ?? 3), stoppingToken);
        }
    }
    private async Task PublishAvailable(CancellationToken token)
    {
        await using var db = store.Open(); await db.OpenAsync(token);
        var id = await db.ExecuteScalarAsync<Guid?>(@";WITH candidate AS (
 SELECT TOP(1) * FROM dbo.t_outgoing_documents WITH(UPDLOCK,READPAST)
 WHERE (publication_state IN ('Pending','Failed') AND (next_attempt_utc IS NULL OR next_attempt_utc<=SYSUTCDATETIME()))
 OR (publication_state='Publishing' AND publication_started_utc<DATEADD(minute,-2,SYSUTCDATETIME()))
 ORDER BY generated_utc,id)
 UPDATE candidate SET publication_state='Publishing',publication_started_utc=SYSUTCDATETIME()
 OUTPUT inserted.id");
        if (!id.HasValue) return;
        var doc = await db.QuerySingleAsync<ReportDocument>(@"SELECT d.id,d.kind,d.receipt_id AS ReceiptId,d.batch_id AS BatchId,
 d.filename,d.mime_type AS MimeType,d.content_bytes AS Bytes,d.content_sha256 AS Hash,
 d.publication_state AS PublicationState,i.code AS InstitutionCode
 FROM dbo.t_outgoing_documents d JOIN dbo.t_institutions i ON i.id=d.recipient_institution_id WHERE d.id=@id", new { id });
        try
        {
            var outbound = store.Folder(doc.InstitutionCode, "OUT");
            Directory.CreateDirectory(outbound);
            var final = Path.Combine(outbound, doc.Filename);
            if (File.Exists(final))
            {
                if (!SHA256.HashData(await File.ReadAllBytesAsync(final, token)).SequenceEqual(doc.Hash))
                    throw new IOException("Existing OUT filename has different content; refusing overwrite.");
            }
            else
            {
                var temp = final + ".tmp." + doc.Id.ToString("N");
                await File.WriteAllBytesAsync(temp, doc.Bytes, token);
                File.Move(temp, final);
            }
            await db.ExecuteAsync(@"UPDATE dbo.t_outgoing_documents SET publication_state='PublishedToOut',published_utc=SYSUTCDATETIME(),
 last_error=NULL,next_attempt_utc=NULL WHERE id=@id", new { id });
            await db.ExecuteAsync(@"INSERT dbo.t_workflow_audit(actor,action,receipt_id,batch_id,document_id)
 VALUES('System','PublishedToOut',@receiptId,@batchId,@id)", new { id, receiptId = doc.ReceiptId, batchId = doc.BatchId });
            logger.LogInformation("Published document {DocumentId} to {Path}", id, final);
        }
        catch (Exception e)
        {
            await db.ExecuteAsync(@"UPDATE dbo.t_outgoing_documents SET publication_state='Failed',retry_count=retry_count+1,
 last_error=@error,next_attempt_utc=DATEADD(second,30,SYSUTCDATETIME()) WHERE id=@id",
                new { id, error = e.Message.Length > 1900 ? e.Message[..1900] : e.Message });
            logger.LogError(e, "Could not publish document {DocumentId}", id);
        }
    }
}
