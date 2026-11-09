using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using PaymentManagement.Common;
using PaymentManagement.DAL;

namespace PaymentManagement.BLL;
public sealed partial class BLL
{
    internal async Task PublishAvailable(CancellationToken token)
    {
        await using var db = _dapperAccess.Open();
        await db.OpenAsync(token);
        var id = await db.ExecuteScalarAsync<Guid?>(Procedure.ClaimOutgoingDocument, cancellationToken: token);
        if (!id.HasValue)
        {
            return;
        }

        var doc = await db.QuerySingleAsync<ReportDocument>(Procedure.GetOutgoingDocument, new { id });
        try
        {
            var outbound = Folder(doc.InstitutionCode, "OUT");
            Directory.CreateDirectory(outbound);
            var final = Path.Combine(outbound, doc.Filename);
            if (File.Exists(final))
            {
                if (!SHA256.HashData(await File.ReadAllBytesAsync(final, token)).SequenceEqual(doc.Hash))
                {
                    throw new IOException("Existing OUT filename has different content; refusing overwrite.");
                }
            }
            else
            {
                var temp = final + ".tmp." + doc.Id.ToString("N");
                await File.WriteAllBytesAsync(temp, doc.Bytes, token);
                File.Move(temp, final);
            }

            await db.ExecuteAsync(Procedure.MarkDocumentPublished, new { id });
            await WriteAudit(db, "System", "PublishedToOut", doc.ReceiptId, doc.BatchId, null, id, null);
            _logger.LogInformation("Published document {DocumentId} to {Path}", id, final);
        }
        catch (Exception e)
        {
            await db.ExecuteAsync(Procedure.MarkPublicationFailed, new { id, error = e.Message.Length > 1900 ? e.Message[..1900] : e.Message });
            _logger.LogError(e, "Could not publish document {DocumentId}", id);
        }
    }
}
