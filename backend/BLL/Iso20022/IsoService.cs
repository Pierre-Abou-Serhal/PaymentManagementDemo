using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;

namespace PaymentManagement.BLL.Iso20022;

public sealed record IsoError(string Message, int Line = 0, int Position = 0);
public sealed class IsoException(string kind, IEnumerable<IsoError> errors, string? definition = null) : Exception(string.Join("; ", errors.Select(x => x.Message)))
{
    public string Kind { get; } = kind;
    public IReadOnlyList<IsoError> Errors { get; } = errors.ToArray();
    public string? Definition { get; } = definition;
}
public sealed record ImportedGroup(int Ordinal, string? PaymentInfoId, string? DebtorName,
    string? DebtorAccount, string? DebtorAccountKind, string? DebtorCurrency, string? DebtorAgent,
    string? ExecutionDate, string SourceXml, List<ImportedTransfer> Transfers);
public sealed record ImportedTransfer(int Ordinal, string? InstructionId, string? EndToEndId,
    string? CreditorName, string? CreditorAccount, string? CreditorAccountKind, string? CreditorAgent,
    decimal? InstructedAmount, string? InstructedCurrency, decimal? EquivalentAmount,
    string? EquivalentCurrency, string? TransferCurrency, string? AccountCurrency,
    decimal? RoutingAmount, string SourceXml, RoutingResult Routing);
public sealed record ImportedFile(string Definition, string? MessageId, string? Creation,
    List<ImportedGroup> Groups);
public sealed record RoutingResult(string Category, string? Code, string Explanation,
    string? Country, string? BankCode, int? RuleRevision);
public sealed record ReportTransfer(string? InstructionId, string? EndToEndId, string Decision, string? Reason);
public sealed record ReportGroup(string? PaymentInfoId, List<ReportTransfer> Transfers);

public sealed class IsoService(string cataloguePath)
{
    public const string InputCode = "pain.001.001.13";
    public const string OutputCode = "pain.002.001.15";
    private const string Prefix = "urn:iso:std:iso:20022:tech:xsd:";
    private string SchemaPath(string code) => Path.Combine(cataloguePath, "Schemas", code + ".xsd");
    public JsonElement Definitions()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(cataloguePath, "manifest.json")));
        return manifest.RootElement.GetProperty("definitions").Clone();
    }

    public bool AllowedCounterpart(string input, string output)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(cataloguePath, "manifest.json")));
        var definitions = manifest.RootElement.GetProperty("definitions").EnumerateArray().ToArray();
        var source = definitions.FirstOrDefault(x => x.GetProperty("code").GetString() == input);
        var target = definitions.FirstOrDefault(x => x.GetProperty("code").GetString() == output);
        return source.ValueKind == JsonValueKind.Object && target.ValueKind == JsonValueKind.Object &&
            source.GetProperty("status").GetString() == "Verified" && target.GetProperty("status").GetString() == "Verified" &&
            source.TryGetProperty("counterparts", out var choices) && choices.EnumerateArray().Any(x => x.GetString() == output);
    }

    public ImportedFile Read(byte[] bytes, IReadOnlyDictionary<string, (decimal Threshold, int Revision)> rules, string ownBank)
    {
        XDocument document;
        try
        {
            using var stream = new MemoryStream(bytes);
            using var reader = XmlReader.Create(stream, SecureReader());
            document = XDocument.Load(reader, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
        catch (XmlException ex) { throw new IsoException("MalformedXml", [new(ex.Message, ex.LineNumber, ex.LinePosition)]); }
        var ns = document.Root?.Name.NamespaceName ?? "";
        var code = ns.Split(':').LastOrDefault() ?? "";
        var suffix = code.StartsWith("pain.001.001.", StringComparison.Ordinal) ? code[13..] : "";
        var legacy = code is "pain.001.001.01" or "pain.001.002.01" or "pain.001.003.01";
        if (document.Root?.Name.LocalName != "Document" ||
            !(legacy || (int.TryParse(suffix, out var parsedVersion) && parsedVersion is >= 2 and <= 13)) ||
            !File.Exists(SchemaPath(code)))
            throw new IsoException("UnsupportedDefinition", [new("The initiation definition is not registered as an executable reader.")], code);
        var bodyName = legacy || code == "pain.001.001.02" ? code : "CstmrCdtTrfInitn";
        if (document.Root.Element(XName.Get(bodyName, ns)) is null)
            throw new IsoException("UnsupportedDefinition", [new("The initiation body does not match its registered definition.")], code);
        try { Validate(bytes, code, ns); }
        catch (IsoException e) { throw new IsoException(e.Kind, e.Errors, code); }
        XNamespace n = ns;
        var body = document.Root.Element(n + bodyName)!;
        var header = body.Element(n + "GrpHdr");
        var groups = new List<ImportedGroup>();
        var groupOrdinal = 0;
        foreach (var group in body.Elements(n + "PmtInf"))
        {
            groupOrdinal++;
            var transfers = new List<ImportedTransfer>();
            var ordinal = 0;
            var debtorAcct = group.Element(n + "DbtrAcct");
            foreach (var tx in group.Elements(n + (legacy ? "PmtTx" : "CdtTrfTxInf")))
            {
                ordinal++;
                var account = tx.Element(n + "CdtrAcct");
                var accountId = account?.Element(n + "Id") ?? account;
                var iban = Value(accountId, n + "IBAN");
                var other = Value(accountId?.Element(n + "Othr"), n + "Id") ??
                    Value(accountId, n + "BBAN") ?? Value(accountId, n + "UPIC") ??
                    Value(accountId?.Element(n + "PrtryAcct"), n + "Id") ??
                    Value(accountId?.Element(n + "DmstAcct"), n + "Id");
                var instructed = tx.Element(n + "Amt")?.Element(n + "InstdAmt");
                var equivalent = tx.Element(n + "Amt")?.Element(n + "EqvtAmt");
                var eqAmount = equivalent?.Element(n + "Amt");
                var instAmount = ParseAmount(instructed);
                var eqValue = ParseAmount(eqAmount);
                var instCurrency = instructed?.Attribute("Ccy")?.Value;
                var eqCurrency = eqAmount?.Attribute("Ccy")?.Value;
                var transferCurrency = instCurrency ?? Value(equivalent, n + "CcyOfTrf");
                var routingAmount = instAmount ?? (eqCurrency == transferCurrency ? eqValue : null);
                var routing = Route(iban, transferCurrency, routingAmount, rules, ownBank);
                transfers.Add(new(ordinal, Value(tx.Element(n + "PmtId"), n + "InstrId"),
                    Value(tx.Element(n + "PmtId"), n + "EndToEndId"),
                    Value(tx.Element(n + "Cdtr"), n + "Nm"), iban ?? other,
                    accountId?.Elements().FirstOrDefault()?.Name.LocalName,
                    Value(tx.Element(n + "CdtrAgt")?.Element(n + "FinInstnId"), n + "BICFI") ??
                    Value(tx.Element(n + "CdtrAgt")?.Element(n + "FinInstnId"), n + "BIC"),
                    instAmount, instCurrency, eqValue, eqCurrency, transferCurrency,
                    Value(account, n + "Ccy"), routingAmount, tx.ToString(SaveOptions.DisableFormatting), routing));
            }
            if (Value(group, n + "NbOfTxs") is { } groupCount &&
                (!long.TryParse(groupCount, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedGroupCount) || expectedGroupCount != transfers.Count))
                throw new IsoException("SemanticValidation", [new($"Payment group {groupOrdinal} NbOfTxs does not match its transaction count.")], code);
            var debtorId = debtorAcct?.Element(n + "Id") ?? debtorAcct;
            var debtorIban = Value(debtorId, n + "IBAN");
            var debtorOther = Value(debtorId?.Element(n + "Othr"), n + "Id") ??
                Value(debtorId, n + "BBAN") ?? Value(debtorId, n + "UPIC") ??
                Value(debtorId?.Element(n + "PrtryAcct"), n + "Id") ??
                Value(debtorId?.Element(n + "DmstAcct"), n + "Id");
            groups.Add(new(groupOrdinal, Value(group, n + "PmtInfId"), Value(group.Element(n + "Dbtr"), n + "Nm"),
                debtorIban ?? debtorOther, debtorId?.Elements().FirstOrDefault()?.Name.LocalName,
                Value(debtorAcct, n + "Ccy"), Value(group.Element(n + "DbtrAgt")?.Element(n + "FinInstnId"), n + "BICFI") ??
                Value(group.Element(n + "DbtrAgt")?.Element(n + "FinInstnId"), n + "BIC"),
                group.Element(n + "ReqdExctnDt") is { } date ? (date.HasElements ? date.Elements().First().Value : date.Value) : null,
                group.ToString(SaveOptions.DisableFormatting), transfers));
        }
        if (groups.Sum(x => x.Transfers.Count) == 0)
            throw new IsoException("SemanticValidation", [new("The initiation contains no transfers.")]);
        if (Value(header, n + "NbOfTxs") is { } fileCount &&
            (!long.TryParse(fileCount, NumberStyles.None, CultureInfo.InvariantCulture, out var expectedFileCount) || expectedFileCount != groups.Sum(x => x.Transfers.Count)))
            throw new IsoException("SemanticValidation", [new("Group header NbOfTxs does not match the imported transaction count.")], code);
        return new(code, Value(header, n + "MsgId") ?? Value(header, n + "GrpId"), Value(header, n + "CreDtTm"), groups);
    }

    public byte[] Write(string outputCode, string messageId, string originalMessageId, string inputCode,
        string? originalCreation, IReadOnlyList<ReportGroup> groups, DateTime generatedUtc)
    {
        var suffix = outputCode.StartsWith("pain.002.001.", StringComparison.Ordinal) ? outputCode[13..] : "";
        if (!int.TryParse(suffix, out var version) || version is < 1 or > 15 || !File.Exists(SchemaPath(outputCode)))
            throw new IsoException("UnsupportedDefinition", [new("No verified report writer exists for " + outputCode)]);
        if (version <= 2) return WriteEarly(outputCode, messageId, originalMessageId, inputCode, originalCreation, groups, generatedUtc, version);
        XNamespace n = Prefix + outputCode;
        XElement El(string name, object value) => new(n + name, value);
        string Status(IEnumerable<ReportTransfer> txs)
        {
            var distinct = txs.Select(x => x.Decision).Distinct().ToArray();
            return distinct.Length == 1 ? (distinct[0] == "Approved" ? "ACSC" : "RJCT") : "PART";
        }
        var all = groups.SelectMany(x => x.Transfers).ToArray();
        var original = El("OrgnlGrpInfAndSts", new object[] {
            El("OrgnlMsgId", originalMessageId), El("OrgnlMsgNmId", inputCode),
            originalCreation is null ? null! : El("OrgnlCreDtTm", originalCreation),
            El("OrgnlNbOfTxs", all.Length), El("GrpSts", Status(all)) }.Where(x => x is not null));
        var groupNodes = groups.Select(g => El("OrgnlPmtInfAndSts", new object[] {
            El("OrgnlPmtInfId", g.PaymentInfoId ?? throw new IsoException("MissingGroupReference", [new("The selected report definition requires a source payment information ID.")])), El("OrgnlNbOfTxs", g.Transfers.Count),
            El("PmtInfSts", Status(g.Transfers)),
            g.Transfers.Select(t => El("TxInfAndSts", new object[] {
                t.InstructionId is null ? null! : El("OrgnlInstrId", t.InstructionId),
                t.EndToEndId is null ? null! : El("OrgnlEndToEndId", t.EndToEndId),
                El("TxSts", t.Decision == "Approved" ? "ACSC" : "RJCT"),
                t.Decision == "Rejected" ? El("StsRsnInf", SplitNarrative(t.Reason ?? "Rejected").Select(x => El("AddtlInf", x))) : null!
            }.Where(x => x is not null))) }));
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null),
            El("Document", El("CstmrPmtStsRpt", new object[] {
                El("GrpHdr", new object[] { El("MsgId", messageId), El("CreDtTm", generatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)) }),
                original, groupNodes })));
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true })) doc.Save(writer);
        var bytes = stream.ToArray();
        Validate(bytes, outputCode, n.NamespaceName);
        return bytes;
    }

    private byte[] WriteEarly(string outputCode, string messageId, string originalMessageId, string inputCode,
        string? originalCreation, IReadOnlyList<ReportGroup> groups, DateTime generatedUtc, int version)
    {
        XNamespace n = version == 1 ? "urn:iso:std:iso:20022:xsd:" + outputCode : Prefix + outputCode;
        XElement El(string name, object value) => new(n + name, value);
        var all = groups.SelectMany(x => x.Transfers).ToArray();
        string Status(IEnumerable<ReportTransfer> transfers)
        {
            var states = transfers.Select(x => x.Decision).Distinct().ToArray();
            return states.Length == 1 ? (states[0] == "Approved" ? "ACSC" : "RJCT") : "PART";
        }
        object body;
        if (version == 1)
        {
            body = new object[] {
                El("GnlInf", new object[] { El("PmtInitnStsId", messageId),
                    El("CreDtTm", generatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)),
                    El("InitgPty", El("Nm", "PaymentManagementDemo")) }),
                El("OrgnlGrpRefInfAndSts", new object[] { El("GrpId", originalMessageId),
                    El("OrgnlMsgTp", inputCode), El("GrpSts", Status(all)) }),
                groups.Select(g => El("OrgnlPmtInf", g.Transfers.Select(t => {
                    if (t.EndToEndId is null) throw new IsoException("MissingReference", [new("pain.002.001.01 requires the original EndToEndId.")]);
                    if (t.Decision == "Rejected" && (t.Reason ?? "Rejected").Length > 105)
                        throw new IsoException("NarrativeTooLong", [new("pain.002.001.01 allows at most 105 characters of rejection narrative per transfer.")]);
                    return El("OrgnlTxRefInfAndSts", new object[] {
                        El("PmtId", new object[] { t.InstructionId is null ? null! : El("InstrId", t.InstructionId), El("EndToEndId", t.EndToEndId) }.Where(x => x is not null)),
                        El("TxSts", t.Decision == "Approved" ? "ACSC" : "RJCT"),
                        t.Decision == "Rejected" ? El("AddtlInf", t.Reason ?? "Rejected") : null!
                    }.Where(x => x is not null)); })))
            };
        }
        else
        {
            body = new object[] {
                El("GrpHdr", new object[] { El("MsgId", messageId),
                    El("CreDtTm", generatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)) }),
                El("OrgnlGrpInfAndSts", new object[] { El("OrgnlMsgId", originalMessageId), El("OrgnlMsgNmId", inputCode),
                    originalCreation is null ? null! : El("OrgnlCreDtTm", originalCreation),
                    El("OrgnlNbOfTxs", all.Length), El("GrpSts", Status(all)) }.Where(x => x is not null)),
                groups.SelectMany(g => g.Transfers.Select(t => El("TxInfAndSts", new object[] {
                    g.PaymentInfoId is null ? null! : El("OrgnlPmtInfId", g.PaymentInfoId),
                    t.InstructionId is null ? null! : El("OrgnlInstrId", t.InstructionId),
                    t.EndToEndId is null ? null! : El("OrgnlEndToEndId", t.EndToEndId),
                    El("TxSts", t.Decision == "Approved" ? "ACSC" : "RJCT"),
                    t.Decision == "Rejected" ? El("StsRsnInf", SplitNarrative(t.Reason ?? "Rejected").Select(x => El("AddtlStsRsnInf", x))) : null!
                }.Where(x => x is not null))))
            };
        }
        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), El("Document", El(outputCode, body)));
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true })) doc.Save(writer);
        var bytes = stream.ToArray();
        Validate(bytes, outputCode, n.NamespaceName);
        return bytes;
    }

    private static IEnumerable<string> SplitNarrative(string value)
    {
        for (var i = 0; i < value.Length; i += 105) yield return value.Substring(i, Math.Min(105, value.Length - i));
    }
    private static XmlReaderSettings SecureReader() => new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 20_000_000 };
    private void Validate(byte[] bytes, string code, string namespaceUri)
    {
        var settings = SecureReader();
        settings.ValidationType = ValidationType.Schema;
        settings.Schemas.Add(namespaceUri, SchemaPath(code));
        settings.ValidationFlags = XmlSchemaValidationFlags.ReportValidationWarnings;
        var errors = new List<IsoError>();
        settings.ValidationEventHandler += (_, e) => errors.Add(new(e.Message, e.Exception?.LineNumber ?? 0, e.Exception?.LinePosition ?? 0));
        try
        {
            using var stream = new MemoryStream(bytes);
            using var reader = XmlReader.Create(stream, settings);
            while (reader.Read()) { }
        }
        catch (XmlException e) { errors.Add(new(e.Message, e.LineNumber, e.LinePosition)); }
        if (errors.Count > 0) throw new IsoException("SchemaValidation", errors);
    }
    private static string? Value(XElement? parent, XName name) => parent?.Element(name)?.Value;
    private static decimal? ParseAmount(XElement? element) => decimal.TryParse(element?.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : null;
    private static RoutingResult Route(string? rawIban, string? currency, decimal? amount,
        IReadOnlyDictionary<string, (decimal Threshold, int Revision)> rules, string ownBank)
    {
        var iban = new string((rawIban ?? "").Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        var failures = new List<string>();
        if (rawIban is null) failures.Add("MISSING_BENEFICIARY_IBAN");
        else if (!ValidIban(iban)) failures.Add("UNUSABLE_BENEFICIARY_IBAN");
        if (currency is null || !rules.ContainsKey(currency)) failures.Add("NO_CURRENCY_THRESHOLD");
        var country = iban.Length >= 2 ? iban[..2] : null;
        var bank = country == "LB" && iban.Length >= 8 ? iban.Substring(4, 4) : null;
        if (failures.Count == 0 && country == "LB" && bank != ownBank && amount is null)
            failures.Add("ROUTING_AMOUNT_UNAVAILABLE");
        if (failures.Count > 0)
            return new("INVALID", string.Join(";", failures), string.Join("; ", failures.Select(FailureText)), country, bank,
                currency is not null && rules.TryGetValue(currency, out var rule) ? rule.Revision : null);
        var selected = rules[currency!];
        var category = country != "LB" ? "SWIFT" : bank == ownBank ? "LOCAL" : amount < selected.Threshold ? "ACH" : "RTGS";
        return new(category, null, category switch {
            "SWIFT" => "Non-Lebanese beneficiary IBAN.", "LOCAL" => "Lebanese own-bank beneficiary (0019).",
            "ACH" => $"Other Lebanese bank; {currency} amount below {selected.Threshold}.",
            _ => $"Other Lebanese bank; {currency} amount at or above {selected.Threshold}." }, country, bank, selected.Revision);
    }
    private static string FailureText(string code) => code switch {
        "MISSING_BENEFICIARY_IBAN" => "Beneficiary IBAN is missing.",
        "UNUSABLE_BENEFICIARY_IBAN" => "Beneficiary IBAN is unusable or has an invalid checksum.",
        "NO_CURRENCY_THRESHOLD" => "No demo routing threshold is configured for the transfer currency.",
        _ => "Transfer-currency routing amount is unavailable." };
    private static bool ValidIban(string iban)
    {
        if (iban.Length is < 15 or > 34 || !char.IsLetter(iban[0]) || !char.IsLetter(iban[1]) ||
            !char.IsDigit(iban[2]) || !char.IsDigit(iban[3]) || iban.Any(c => !char.IsAsciiLetterOrDigit(c))) return false;
        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var ch in rearranged)
        {
            var digits = char.IsDigit(ch) ? ch.ToString() : (ch - 'A' + 10).ToString(CultureInfo.InvariantCulture);
            foreach (var digit in digits) remainder = (remainder * 10 + digit - '0') % 97;
        }
        return remainder == 1;
    }
}
