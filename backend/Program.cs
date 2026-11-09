using System.Security.Cryptography;
using System.Text.Json;
using PaymentManagement.Api.Delivery;
using PaymentManagement.Api.Imports;
using PaymentManagement.Api.Iso20022;
using PaymentManagement.Api.Payments;
using PaymentManagement.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();
builder.Services.AddSingleton<IsoService>();
builder.Services.AddSingleton<PaymentStore>();
builder.Services.AddSingleton<WorkflowService>();
if (builder.Configuration.GetValue<bool>("Demo:RunWorkers", true))
{
    builder.Services.AddHostedService<FolderPoller>();
    builder.Services.AddHostedService<OutboxPublisher>();
}
builder.Services.AddCors(options => options.AddPolicy("Development", policy => policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));
var app = builder.Build();
app.UseCors("Development");
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (WorkflowException e)
    {
        context.Response.StatusCode = e.Status;
        await context.Response.WriteAsJsonAsync(new { error = e.Message, details = e.Details });
    }
    catch (IsoException e)
    {
        context.Response.StatusCode = 422;
        await context.Response.WriteAsJsonAsync(new { error = e.Kind, details = e.Errors });
    }
});
app.MapGet("/health", async (PaymentStore store) =>
{
    if (!await store.DatabaseAvailable())
        return Results.Json(new { database = "unavailable", storage = "unknown", operatorIdentity = store.Operator }, statusCode: 503);
    var institutions = await store.ActiveInstitutions();
    var missing = institutions.SelectMany(i => new[] { "IN", "OUT", "ARCHIVED", "ERROR" }
        .Select(kind => store.Folder(i.Code, kind))).Where(path => !Directory.Exists(path)).ToArray();
    return Results.Json(new { database = "available", storage = missing.Length == 0 ? "available" : "missing folders",
        missingFolders = missing, operatorIdentity = store.Operator }, statusCode: missing.Length == 0 ? 200 : 503);
});
app.MapGet("/api/institutions", (PaymentStore store) => store.Institutions());
app.MapGet("/api/iso-definitions", (IWebHostEnvironment env) =>
{
    using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(env.ContentRootPath, "Iso20022", "manifest.json")));
    return Results.Json(json.RootElement.GetProperty("definitions").Clone());
});
app.MapGet("/api/routing-rules", (PaymentStore store) => store.Rules());
app.MapPut("/api/routing-rules", async (RuleRequest request, PaymentStore store) => { await store.SetRule(request.Currency, request.Threshold); return Results.NoContent(); });
app.MapGet("/api/institutions/{id:int}/mappings", (int id, PaymentStore store) => store.Mappings(id));
app.MapPut("/api/institutions/{id:int}/mappings", async (int id, MappingRequest request, PaymentStore store) => { await store.SetMapping(id, request.InputCode, request.OutputCode); return Results.NoContent(); });
app.MapPost("/api/file-receipts/upload", async (HttpRequest request, PaymentStore store) =>
{
    if (!request.HasFormContentType) throw new WorkflowException(400, "Use multipart form data.");
    var form = await request.ReadFormAsync();
    if (!int.TryParse(form["institutionId"], out var institutionId)) throw new WorkflowException(400, "Select an institution.");
    var file = form.Files.GetFile("file") ?? throw new WorkflowException(400, "Choose an XML file.");
    var institution = (await store.ActiveInstitutions()).SingleOrDefault(x => x.Id == institutionId)
        ?? throw new WorkflowException(404, "Institution not found.");
    var original = Path.GetFileName(file.FileName);
    if (!original.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) throw new WorkflowException(400, "File must have .xml extension.");
    var id = Guid.NewGuid();
    var inbound = store.Folder(institution.Code, "IN");
    Directory.CreateDirectory(inbound);
    var staged = Path.Combine(inbound, $"{id:N}__{original}.uploading");
    var claimed = Path.Combine(inbound, $"{id:N}__{original}.processing.{id:N}");
    await using (var stream = File.Create(staged)) await file.CopyToAsync(stream);
    File.Move(staged, claimed);
    if (!FolderPoller.TryClaim(id)) throw new WorkflowException(409, "Upload is already processing.");
    try { await store.ImportClaimed(id, institutionId, original, claimed, "Upload"); }
    finally { FolderPoller.Release(id); }
    return Results.Ok(new { receiptId = id });
});
app.MapGet("/api/file-receipts", (int? institutionId, string? state, int? skip, int? take, string? search, WorkflowService workflow) =>
    workflow.Receipts(institutionId, state, skip ?? 0, take ?? 50, search));
app.MapGet("/api/file-receipts/{id:guid}", async (Guid id, WorkflowService workflow) =>
    await workflow.Receipt(id) is { } value ? Results.Ok(value) : Results.NotFound());
app.MapGet("/api/file-receipts/{id:guid}/validation-errors", async (Guid id, WorkflowService workflow) =>
    await workflow.Receipt(id) is { } value ? Results.Ok(new { value.errorType, value.validationJson, value.duplicateOf }) : Results.NotFound());
app.MapGet("/api/file-receipts/{id:guid}/original", async (Guid id, WorkflowService workflow) =>
    await workflow.Original(id) is { } file ? Results.File(file.Bytes, "application/xml", file.Filename) : Results.NotFound());
app.MapPost("/api/file-receipts/{id:guid}/institution", async (Guid id, InstitutionRequest request, WorkflowService workflow) =>
{ await workflow.CorrectReceiptInstitution(id, request); return Results.NoContent(); });
app.MapGet("/api/batches/{id:guid}", async (Guid id, WorkflowService workflow) =>
    await workflow.Batch(id) is { } value ? Results.Ok(value) : Results.NotFound());
app.MapGet("/api/batches/{id:guid}/payment-groups", (Guid id, WorkflowService workflow) => workflow.Groups(id));
app.MapGet("/api/batches/{id:guid}/transfers", (Guid id, string? category, string? decision, Guid? groupId, int? skip, int? take, WorkflowService workflow) =>
    workflow.Transfers(id, category, decision, groupId, skip ?? 0, take ?? 50));
app.MapGet("/api/transfers/{id:guid}", async (Guid id, WorkflowService workflow) =>
    await workflow.Transfer(id) is { } value ? Results.Ok(value) : Results.NotFound());
app.MapPost("/api/batches/{id:guid}/categories/{category}/process", async (Guid id, string category, WorkflowService workflow) =>
{ await workflow.Process(id, category); return Results.NoContent(); });
app.MapPost("/api/batches/{id:guid}/transfer-decisions", async (Guid id, DecisionRequest request, WorkflowService workflow) =>
{ await workflow.Decide(id, request); return Results.NoContent(); });
app.MapPost("/api/batches/{id:guid}/finalize", async (Guid id, FinalizeRequest request, WorkflowService workflow) =>
    Results.Ok(new { reportId = await workflow.Finalize(id, request) }));
app.MapPost("/api/batches/{id:guid}/reopen", async (Guid id, WorkflowService workflow) =>
{ await workflow.Reopen(id); return Results.NoContent(); });
app.MapGet("/api/batches/{id:guid}/reports", (Guid id, WorkflowService workflow) => workflow.Reports(id));
app.MapGet("/api/batches/{id:guid}/audit", (Guid id, WorkflowService workflow) => workflow.Audit(id));
app.MapGet("/api/reports/{id:guid}/download", async (Guid id, WorkflowService workflow) =>
    await workflow.Document(id) is { } doc ? Results.File(doc.Bytes, doc.MimeType, doc.Filename) : Results.NotFound());
app.MapPost("/api/reports/{id:guid}/retry-publication", async (Guid id, WorkflowService workflow) =>
{ await workflow.RetryPublication(id); return Results.NoContent(); });
app.Run();
