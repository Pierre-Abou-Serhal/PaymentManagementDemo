using Microsoft.AspNetCore.Mvc;
using PaymentManagement.Common;
using PaymentService = PaymentManagement.BLL.BLL;

namespace PaymentManagement.Controllers;
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api")]
public sealed class LegacyPaymentController(PaymentService bal) : ControllerBase
{
    [HttpGet("/health")]
    public async Task<IActionResult> Health()
    {
        if (!await bal.DatabaseAvailable())
        {
            return StatusCode(503, new { database = "unavailable", storage = "unknown", operatorIdentity = bal.Operator });
        }

        var institutions = await bal.GetActiveInstitutions();
        var missing = institutions.SelectMany(i => new[] { "IN", "OUT", "ARCHIVED", "ERROR" }.Select(purpose => bal.Folder(i.Code, purpose))).Where(path => !Directory.Exists(path)).ToArray();
        return StatusCode(missing.Length == 0 ? 200 : 503, new { database = "available", storage = missing.Length == 0 ? "available" : "missing folders", missingFolders = missing, operatorIdentity = bal.Operator });
    }

    [HttpGet("institutions")]
    public async Task<IActionResult> Institutions() => Ok(await bal.GetAllInstitutions());
    [HttpGet("iso-definitions")]
    public IActionResult Definitions() => Ok(bal.GetIsoDefinitions());
    [HttpGet("routing-rules")]
    public async Task<IActionResult> Rules() => Ok(await bal.GetRoutingRules());
    [HttpPut("routing-rules")]
    public async Task<IActionResult> SetRule(RuleRequest request)
    {
        await bal.SaveRoutingRule(request.Currency, request.Threshold);
        return NoContent();
    }

    [HttpGet("institutions/{id:int}/mappings")]
    public async Task<IActionResult> Mappings(int id) => Ok(await bal.GetResponseMappings(id));
    [HttpPut("institutions/{id:int}/mappings")]
    public async Task<IActionResult> SetMapping(int id, MappingRequest request)
    {
        await bal.SaveResponseMapping(id, request.InputCode, request.OutputCode);
        return NoContent();
    }

    [HttpPost("file-receipts/upload")]
    public async Task<IActionResult> Upload(CancellationToken cancellationToken)
    {
        if (!Request.HasFormContentType)
        {
            throw new WorkflowException(400, "Use multipart form data.");
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        if (!int.TryParse(form["institutionId"], out var institutionId))
        {
            throw new WorkflowException(400, "Select an institution.");
        }

        var file = form.Files.GetFile("file") ?? throw new WorkflowException(400, "Choose an XML file.");
        await using var source = file.OpenReadStream();
        return Ok(new { receiptId = await bal.UploadFile(institutionId, file.FileName, source, cancellationToken) });
    }

    [HttpGet("file-receipts")]
    public async Task<IActionResult> Receipts(int? institutionId, string? state, int? skip, int? take, string? search) => Ok(await bal.GetFileReceipts(institutionId, state, skip ?? 0, take ?? 50, search));
    [HttpGet("file-receipts/{id:guid}")]
    public async Task<IActionResult> Receipt(Guid id) => await bal.GetFileReceipt(id)is { } receipt ? Ok(receipt) : NotFound();
    [HttpGet("file-receipts/{id:guid}/validation-errors")]
    public async Task<IActionResult> ValidationErrors(Guid id) => await bal.GetFileReceipt(id)is { } receipt ? Ok(new { receipt.ErrorType, receipt.ValidationJson, receipt.DuplicateOf }) : NotFound();
    [HttpGet("file-receipts/{id:guid}/original")]
    public async Task<IActionResult> Original(Guid id) => await bal.GetOriginalFile(id)is { } file ? File(file.Bytes, "application/xml", file.Filename) : NotFound();
    [HttpPost("file-receipts/{id:guid}/institution")]
    public async Task<IActionResult> CorrectInstitution(Guid id, InstitutionRequest request)
    {
        await bal.CorrectInstitutionAssignment(id, request);
        return NoContent();
    }

    [HttpGet("batches/{id:guid}")]
    public async Task<IActionResult> Batch(Guid id) => await bal.GetBatch(id)is { } batch ? Ok(batch) : NotFound();
    [HttpGet("batches/{id:guid}/payment-groups")]
    public async Task<IActionResult> Groups(Guid id) => Ok(await bal.GetPaymentGroups(id));
    [HttpGet("batches/{id:guid}/transfers")]
    public async Task<IActionResult> Transfers(Guid id, string? category, string? decision, Guid? groupId, int? skip, int? take) => Ok(await bal.GetTransfers(id, category, decision, groupId, skip ?? 0, take ?? 50));
    [HttpGet("transfers/{id:guid}")]
    public async Task<IActionResult> Transfer(Guid id) => await bal.GetTransfer(id)is { } transfer ? Ok(transfer) : NotFound();
    [HttpPost("batches/{id:guid}/categories/{category}/process")]
    public async Task<IActionResult> ProcessCategory(Guid id, string category)
    {
        await bal.ProcessCategory(id, category);
        return NoContent();
    }

    [HttpPost("batches/{id:guid}/transfer-decisions")]
    public async Task<IActionResult> Decide(Guid id, DecisionRequest request)
    {
        await bal.SaveTransferDecisions(id, request);
        return NoContent();
    }

    [HttpPost("batches/{id:guid}/finalize")]
    public async Task<IActionResult> FinalizeBatch(Guid id, FinalizeRequest request) => Ok(new { reportId = await bal.FinalizeBatch(id, request) });
    [HttpPost("batches/{id:guid}/reopen")]
    public async Task<IActionResult> Reopen(Guid id)
    {
        await bal.ReopenBatch(id);
        return NoContent();
    }

    [HttpGet("batches/{id:guid}/reports")]
    public async Task<IActionResult> Reports(Guid id) => Ok(await bal.GetReports(id));
    [HttpGet("batches/{id:guid}/audit")]
    public async Task<IActionResult> Audit(Guid id) => Ok(await bal.GetAudit(id));
    [HttpGet("reports/{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id) => await bal.GetReportDocument(id)is { } doc ? File(doc.Bytes, doc.MimeType, doc.Filename) : NotFound();
    [HttpPost("reports/{id:guid}/retry-publication")]
    public async Task<IActionResult> RetryPublication(Guid id)
    {
        await bal.RetryPublication(id);
        return NoContent();
    }
}
