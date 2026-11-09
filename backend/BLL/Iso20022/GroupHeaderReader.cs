using System.Xml;
using System.Xml.Linq;
using PaymentManagement.Common;

namespace PaymentManagement.BLL.Iso20022;

/// <summary>Projects the original header for display without changing or revalidating the receipt.</summary>
public static class GroupHeaderReader
{
    public static GroupHeaderView Read(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 64_000_000
            });
            // Stop after the header: large transaction bodies need not be loaded for the queue.
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.Depth != 2 || reader.LocalName != "GrpHdr") continue;
                using var subtree = reader.ReadSubtree();
                var header = XElement.Load(subtree);
                var n = header.Name.Namespace;
                string? Value(string name) => header.Element(n + name)?.Value;
                var initiatingParty = header.Element(n + "InitgPty");
                var forwardingAgent = header.Element(n + "FwdgAgt");
                return new(true, n.NamespaceName.Split(':').Last(), Value("MsgId") ?? Value("GrpId"),
                    Value("CreDtTm"), Value("NbOfTxs"), Value("CtrlSum"), initiatingParty?.Element(n + "Nm")?.Value,
                    forwardingAgent?.Descendants().FirstOrDefault(e => e.Name.LocalName is "BICFI" or "BIC" or "Nm")?.Value,
                    Value("BtchBookg"), Value("Grpg"), Flatten(header, "GrpHdr").ToArray(), header.ToString());
            }
            return new(false, Notice: "No GrpHdr was found in the original file.");
        }
        catch (XmlException)
        {
            return new(false, Notice: "The group header could not be read from the original XML. See the receipt's validation details.");
        }
    }

    private static IEnumerable<HeaderField> Flatten(XElement element, string path)
    {
        foreach (var attribute in element.Attributes().Where(a => !a.IsNamespaceDeclaration))
            yield return new(path + "/@" + attribute.Name.LocalName, attribute.Value);
        if (!element.HasElements) { yield return new(path, element.Value); yield break; }
        foreach (var child in element.Elements())
        {
            var siblings = element.Elements(child.Name).ToList();
            var suffix = siblings.Count > 1 ? $"[{siblings.IndexOf(child) + 1}]" : "";
            foreach (var field in Flatten(child, path + "/" + child.Name.LocalName + suffix)) yield return field;
        }
    }
}
