using PaymentManagement.BLL.Iso20022;
using System.Text.Json;
using System.Xml.Linq;
using System.Text;

var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../backend/PaymentManagement"));
var samples = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../demo/samples"));
var iso = new IsoService(Path.Combine(root, "Iso20022"));
var rules = new Dictionary<string, (decimal, int)> { ["EUR"] = (10000, 1), ["USD"] = (10000, 1), ["GBP"] = (10000, 1), ["LBP"] = (1000000000, 1) };
ImportedFile Read(string name) => iso.Read(File.ReadAllBytes(Path.Combine(samples, name)), rules, "0019");
void Check(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); Console.WriteLine("PASS: " + name); }

var mixed = Read("mixed-demo.xml");
var headerView = GroupHeaderReader.Read(File.ReadAllBytes(Path.Combine(samples,"mixed-demo.xml")));
Check(headerView.Available && headerView.MessageId == "MIXED-DEMO" && headerView.NumberOfTransactions == "7" &&
    headerView.InitiatingParty == "Synthetic Demo Institution" && headerView.ControlSum is null,
    "source GrpHdr summary retains declared values without inventing a control sum");
var richHeader = "<?xml version=\"1.0\" encoding=\"utf-16\"?><Document xmlns=\"urn:iso:std:iso:20022:tech:xsd:pain.001.001.13\"><CstmrCdtTrfInitn><GrpHdr><MsgId>HEADER-TEST</MsgId><Authstn><Prtry>First</Prtry></Authstn><Authstn><Prtry>Second</Prtry></Authstn><NbOfTxs>2</NbOfTxs><CtrlSum>1234.50</CtrlSum><InitgPty><Nm>Équipe Démo</Nm><Id><OrgId><Othr><Id>PARTY-1</Id></Othr></OrgId></Id></InitgPty></GrpHdr></CstmrCdtTrfInitn></Document>";
var detailedHeader=GroupHeaderReader.Read(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(richHeader)).ToArray());
Check(detailedHeader.ControlSum == "1234.50" && detailedHeader.InitiatingParty == "Équipe Démo" &&
    detailedHeader.Fields!.Any(f=>f.Path=="GrpHdr/Authstn[1]/Prtry"&&f.Value=="First") &&
    detailedHeader.Fields!.Any(f=>f.Path=="GrpHdr/Authstn[2]/Prtry"&&f.Value=="Second") &&
    detailedHeader.Fields!.Any(f=>f.Path=="GrpHdr/InitgPty/Id/OrgId/Othr/Id"&&f.Value=="PARTY-1"),
    "GrpHdr details preserve encoding, repeated fields, nested identifiers and exact decimal text");
Check(!GroupHeaderReader.Read(Encoding.UTF8.GetBytes("<!DOCTYPE Document [<!ENTITY x SYSTEM 'file:///unavailable'>]><Document><X><GrpHdr><MsgId>&x;</MsgId></GrpHdr></X></Document>")).Available,
    "GrpHdr display reader prohibits DTD and external entities");
Check(!GroupHeaderReader.Read(Encoding.UTF8.GetBytes("<Document><Broken")).Available,
    "unreadable headers do not break the queue projection");
var categories = mixed.Groups.SelectMany(g => g.Transfers).Select(t => t.Routing.Category).ToArray();
Check(categories.Length == 7, "seven transfers retained across two groups");
Check(categories.Order().SequenceEqual(new[] { "LOCAL", "ACH", "RTGS", "SWIFT", "INVALID", "INVALID", "INVALID" }.Order()), "five categories and Invalid policy");
Check(Read("one-hundred.xml").Groups.Sum(g => g.Transfers.Count) == 100, "one hundred transfers and five groups");
Check(Read("all-invalid.xml").Groups.SelectMany(g => g.Transfers).All(t => t.Routing.Category == "INVALID"), "all Invalid import");
foreach (var name in new[] { "malformed.xml", "xsd-invalid.xml" })
{
    try { Read(name); throw new Exception("FAILED: accepted " + name); }
    catch (IsoException e) { Check(e.Errors.Count > 0, name + " reported " + e.Kind); }
}
var groups = mixed.Groups.Select(g => new ReportGroup(g.PaymentInfoId,
    g.Transfers.Select(t => new ReportTransfer(t.InstructionId, t.EndToEndId,
        t.Routing.Category == "INVALID" ? "Rejected" : t.Ordinal % 2 == 0 ? "Approved" : "Rejected",
        t.Routing.Category == "INVALID" ? t.Routing.Explanation : "Operator demo rejection")).ToList())).ToList();
var bytes = iso.Write("pain.002.001.15", "PMD-TEST-1", mixed.MessageId!, mixed.Definition, mixed.Creation, groups, DateTime.UtcNow);
Check(bytes.Length > 0, "mixed pain.002.001.15 passes XSD validation");
var allInvalid = Read("all-invalid.xml");
var rejected = allInvalid.Groups.Select(g => new ReportGroup(g.PaymentInfoId,
    g.Transfers.Select(t => new ReportTransfer(t.InstructionId,t.EndToEndId,"Rejected",t.Routing.Explanation)).ToList())).ToList();
Check(iso.Write("pain.002.001.15", "PMD-TEST-2",allInvalid.MessageId!,allInvalid.Definition,allInvalid.Creation,rejected,DateTime.UtcNow).Length > 0, "all Invalid pain.002 passes XSD validation");
for (var version = 2; version <= 13; version++)
{
    var code = $"pain.001.001.{version:00}";
    var input = Read($"version-{code}.xml");
    Check(input.Definition == code && input.Groups.Sum(g => g.Transfers.Count) == 7, code + " reader and XSD");
    Check(GroupHeaderReader.Read(File.ReadAllBytes(Path.Combine(samples,$"version-{code}.xml"))) is {Available:true,Definition:var headerCode} && headerCode==code,
        code + " GrpHdr display");
}
foreach (var code in new[] { "pain.001.001.01", "pain.001.002.01", "pain.001.003.01" })
{
    var input = Read($"version-{code}.xml");
    Check(input.Definition == code && input.Groups.Sum(g => g.Transfers.Count) == 2, code + " legacy reader and XSD");
    Check(GroupHeaderReader.Read(File.ReadAllBytes(Path.Combine(samples,$"version-{code}.xml"))) is {Available:true,MessageId:not null},
        code + " legacy GrpHdr display");
}
for (var version = 1; version <= 15; version++)
{
    var output = $"pain.002.001.{version:00}";
    var input = version <= 2 ? Read($"version-pain.001.001.{version:00}.xml") : mixed;
    foreach (var scenario in new[] { "Approved", "Rejected", "Mixed" })
    {
        var index = 0;
        var reportGroups = input.Groups.Select(g => new ReportGroup(g.PaymentInfoId,
            g.Transfers.Select(t => new ReportTransfer(t.InstructionId,t.EndToEndId,
                scenario == "Mixed" ? (++index % 2 == 0 ? "Approved" : "Rejected") : scenario,
                "Synthetic rejection")).ToList())).ToList();
        var result = iso.Write(output, "PMD-ALL-OUTPUTS", input.MessageId!, input.Definition, input.Creation, reportGroups, DateTime.UtcNow);
        Check(XDocument.Load(new MemoryStream(result)).Descendants().Count(x => x.Name.LocalName == "TxSts") == input.Groups.Sum(g => g.Transfers.Count),
            output + " " + scenario + " XSD and full transfer results");
    }
}
using (var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Iso20022", "manifest.json"))))
foreach (var definition in inventory.RootElement.GetProperty("definitions").EnumerateArray().Where(x => x.GetProperty("family").GetString() == "pain.001"))
{
    var inputCode = definition.GetProperty("code").GetString()!;
    var input = Read($"version-{inputCode}.xml");
    foreach (var counterpart in definition.GetProperty("counterparts").EnumerateArray())
    {
        var outputCode = counterpart.GetString()!;
        Check(iso.AllowedCounterpart(inputCode, outputCode), inputCode + " configured counterpart is registered");
        foreach (var scenario in new[] { "Approved", "Rejected", "Mixed" })
        {
            var ordinal = 0;
            var reportGroups = input.Groups.Select(g => new ReportGroup(g.PaymentInfoId,
                g.Transfers.Select(t => new ReportTransfer(t.InstructionId, t.EndToEndId,
                    scenario == "Mixed" ? (++ordinal % 2 == 0 ? "Approved" : "Rejected") : scenario,
                    "Synthetic rejection")).ToList())).ToList();
            var result = iso.Write(outputCode, "PMD-PAIR-TEST", input.MessageId!, input.Definition, input.Creation, reportGroups, DateTime.UtcNow);
            var statusCount = XDocument.Load(new MemoryStream(result)).Descendants().Count(x => x.Name.LocalName == "TxSts");
            Check(statusCount == input.Groups.Sum(g => g.Transfers.Count), inputCode + " → " + outputCode + " " + scenario + " full report");
        }
    }
}
