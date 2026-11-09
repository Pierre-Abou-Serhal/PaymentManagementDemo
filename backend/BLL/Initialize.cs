using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentManagement.BLL.Iso20022;
using PaymentManagement.Common;
using PaymentManagement.DAL;

namespace PaymentManagement.BLL;

public sealed partial class BLL
{
    private readonly DapperAccess _dapperAccess;
    private readonly IsoService _isoService;
    private readonly IOptionsMonitor<GlobalSettings> _globalSettings;
    private readonly ILogger<BLL> _logger;

    public BLL(DapperAccess dal, IsoService iso, IOptionsMonitor<GlobalSettings> settings, ILogger<BLL> logger)
    {
        _dapperAccess = dal;
        _isoService = iso;
        _globalSettings = settings;
        _logger = logger;
    }

    private GlobalSettings Settings => _globalSettings.CurrentValue;
    public string Root => Settings.Root ?? Path.Combine(AppContext.BaseDirectory, "PAYMENT_MANAGEMENT");
    public string Operator => Settings.OperatorName;
    private string OwnBank => Settings.OwnBankCode;
    internal int PollSeconds => Settings.PollSeconds;
    internal int PublishSeconds => Settings.PublishSeconds;
    public string Folder(string code, string purpose) => Path.Combine(Root, code, purpose);
    public JsonElement GetIsoDefinitions() => _isoService.Definitions();
}
