using PaymentManagement.BLL.Iso20022;
using PaymentManagement.BLL.Workers;
using PaymentManagement.Common;
using PaymentManagement.DAL;
using PaymentManagement.Filters;
using PaymentManagement.Middleware;
using PaymentService = PaymentManagement.BLL.BLL;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();
builder.Services.Configure<GlobalSettings>(builder.Configuration.GetSection(GlobalSettings.Section));
var connectionString = builder.Configuration.GetConnectionString("PaymentManagement")
    ?? throw new InvalidOperationException("ConnectionStrings:PaymentManagement is missing.");
builder.Services.AddTransient(_ => new DapperAccess(connectionString));
builder.Services.AddSingleton(new IsoService(Path.Combine(builder.Environment.ContentRootPath, "Iso20022")));
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<PaymentActionFilter>();
builder.Services.AddScoped<CorrelationLog>();

if (builder.Configuration.GetValue("Demo:RunWorkers", true))
{
    builder.Services.AddHostedService<FolderPoller>();
    builder.Services.AddHostedService<OutboxPublisher>();
}
builder.Services.AddCors(options => options.AddPolicy("Development",
    policy => policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()
        .WithExposedHeaders("Content-Disposition")));
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options => options.SuppressMapClientErrors = true);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Development");
app.UseMiddleware<WorkflowExceptionMiddleware>();
app.MapControllers();
app.Run();
