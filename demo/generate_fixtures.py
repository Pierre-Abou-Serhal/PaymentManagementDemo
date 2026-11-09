"""Generate synthetic pain.001.001.13 examples. No real customer data."""
from pathlib import Path
from xml.etree import ElementTree as ET

NS = "urn:iso:std:iso:20022:tech:xsd:pain.001.001.13"
ET.register_namespace("", NS)
OUT = Path(__file__).parent / "samples"
OUT.mkdir(exist_ok=True)


def el(parent, name, text=None, **attrs):
    node = ET.SubElement(parent, f"{{{NS}}}{name}", attrs)
    if text is not None:
        node.text = str(text)
    return node


def iban(country, bban):
    provisional = country + "00" + bban
    moved = (bban + country + "00").upper()
    digits = "".join(str(ord(c) - 55) if c.isalpha() else c for c in moved)
    return country + f"{98 - int(digits) % 97:02d}" + bban


OWN = iban("LB", "0019" + "0" * 20)
OTHER = iban("LB", "0020" + "1" * 20)
FOREIGN = iban("DE", "370400440532013000")


def document(name, items, group_count=1):
    root = ET.Element(f"{{{NS}}}Document")
    body = el(root, "CstmrCdtTrfInitn")
    hdr = el(body, "GrpHdr")
    el(hdr, "MsgId", name)
    el(hdr, "CreDtTm", "2026-11-08T12:00:00Z")
    el(hdr, "NbOfTxs", len(items))
    el(el(hdr, "InitgPty"), "Nm", "Synthetic Demo Institution")
    buckets = [[] for _ in range(group_count)]
    for index, item in enumerate(items):
        buckets[index % group_count].append(item)
    for index, bucket in enumerate(buckets, 1):
        group = el(body, "PmtInf")
        el(group, "PmtInfId", f"GROUP-{index}")
        el(group, "PmtMtd", "TRF")
        el(el(group, "ReqdExctnDt"), "Dt", "2026-11-09")
        el(el(group, "Dbtr"), "Nm", f"Synthetic Debtor {index}")
        el(el(el(group, "DbtrAcct"), "Id"), "IBAN", OWN)
        el(el(el(group, "DbtrAgt"), "FinInstnId"), "BICFI", "AAAA LB BB".replace(" ", ""))
        for ordinal, (account, currency, amount, equivalent) in enumerate(bucket, 1):
            tx = el(group, "CdtTrfTxInf")
            pid = el(tx, "PmtId")
            el(pid, "InstrId", f"I-{index}-{ordinal}")
            el(pid, "EndToEndId", f"E2E-{index}-{ordinal}")
            amt = el(tx, "Amt")
            if equivalent:
                eq = el(amt, "EqvtAmt")
                el(eq, "Amt", amount, Ccy="USD")
                el(eq, "CcyOfTrf", currency)
            else:
                el(amt, "InstdAmt", amount, Ccy=currency)
            el(el(tx, "Cdtr"), "Nm", f"Synthetic Beneficiary {index}-{ordinal}")
            account_id = el(el(tx, "CdtrAcct"), "Id")
            if account.startswith("OTHER:"):
                el(el(account_id, "Othr"), "Id", account[6:])
            else:
                el(account_id, "IBAN", account)
            el(el(tx, "RmtInf"), "Ustrd", f"Synthetic invoice {index}-{ordinal}")
    path = OUT / (name.lower() + ".xml")
    ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)
    return path


mixed = [
    (OWN, "USD", "10.00", False),
    (OTHER, "EUR", "9999.99", False),
    (OTHER, "EUR", "10000.00", False),
    (FOREIGN, "GBP", "20.00", False),
    ("OTHER:12345678", "USD", "5.00", False),
    (OTHER, "JPY", "30.00", False),
    (OTHER, "EUR", "30.00", True),
]
document("MIXED-DEMO", mixed, 2)
document("ALL-INVALID", mixed[4:], 2)
document("ALL-APPROVED", mixed[:4], 2)
document("ALL-REJECTED", mixed[:4], 2)
document("ONE-HUNDRED", [mixed[i % len(mixed)] for i in range(100)], 5)
(OUT / "malformed.xml").write_text("<Document><unclosed>", encoding="utf-8")
(OUT / "xsd-invalid.xml").write_text((OUT / "mixed-demo.xml").read_text(encoding="utf-8").replace("<PmtMtd>TRF</PmtMtd>", "<PmtMtd>NOPE</PmtMtd>"), encoding="utf-8")
print("Synthetic IBANs:", OWN, OTHER, FOREIGN)

# One schema-valid baseline for each modern and 2006 initiation definition.
for version in range(2, 14):
    code = f"pain.001.001.{version:02d}"
    namespace = f"urn:iso:std:iso:20022:tech:xsd:{code}"
    tree = ET.parse(OUT / "mixed-demo.xml")
    root = tree.getroot()
    for node in root.iter():
        node.tag = f"{{{namespace}}}{node.tag.split('}')[-1]}"
    body = list(root)[0]
    if version == 2:
        body.tag = f"{{{namespace}}}{code}"
        header = body.find(f"{{{namespace}}}GrpHdr")
        assert header is not None
        grpg = ET.Element(f"{{{namespace}}}Grpg")
        grpg.text = "MIXD"
        header.insert(3, grpg)
        for other in root.findall(f".//{{{namespace}}}Othr"):
            account_id = other.find(f"{{{namespace}}}Id")
            other.tag = f"{{{namespace}}}BBAN"
            other.text = account_id.text if account_id is not None else "12345678"
            other.clear()
            other.text = "12345678"
    if version <= 7:
        for date in root.findall(f".//{{{namespace}}}ReqdExctnDt"):
            date.text = list(date)[0].text
            date.remove(list(date)[0])
    if version <= 3:
        for bic in root.findall(f".//{{{namespace}}}BICFI"):
            bic.tag = f"{{{namespace}}}BIC"
    ET.register_namespace("", namespace)
    tree.write(OUT / f"version-{code}.xml", encoding="utf-8", xml_declaration=True)

for code in ("pain.001.001.01", "pain.001.002.01", "pain.001.003.01"):
    namespace = f"urn:iso:std:iso:20022:xsd:{code}"
    ET.register_namespace("", namespace)
    def old(parent, name, text=None, **attrs):
        node = ET.SubElement(parent, f"{{{namespace}}}{name}", attrs)
        node.text = str(text) if text is not None else None
        return node
    root = ET.Element(f"{{{namespace}}}Document")
    body = old(root, code)
    header = old(body, "GrpHdr")
    old(header, "GrpId", "LEGACY-DEMO")
    old(header, "CreDtTm", "2026-11-08T12:00:00Z")
    old(old(header, "InitgPty"), "Nm", "Synthetic Demo Institution")
    for index, account in enumerate((OWN, OTHER), 1):
        group = old(body, "PmtInf")
        old(group, "ReqdExctnDt", "2026-11-09")
        if code == "pain.001.001.01":
            old(group, "PmtMtdByFrstAgt", "TRF")
        old(old(old(group, "DbtrAcct"), "Id"), "IBAN", OWN)
        agent = old(group, "FrstAgt")
        if code == "pain.001.001.01": old(old(agent, "FinInstnId"), "BIC", "AAAALBBB")
        else: old(agent, "BIC", "AAAALBBB")
        tx = old(group, "PmtTx")
        pid = old(tx, "PmtId")
        old(pid, "InstrId", f"LEG-{index}")
        old(pid, "EndToEndId", f"LEG-E2E-{index}")
        if code == "pain.001.003.01": old(old(tx, "Purp"), "Prtry", "DEMO")
        old(old(tx, "Amt"), "InstdAmt", "100.00", Ccy="USD")
        old(old(tx, "Cdtr"), "Nm", f"Synthetic Beneficiary {index}")
        if code == "pain.001.001.01": old(old(old(tx, "CdtrAcct"), "Id"), "IBAN", account)
        elif code == "pain.001.002.01": old(old(tx, "CdtrAcct"), "IBAN", account)
        if code == "pain.001.002.01": old(old(tx, "FnlAgt"), "BIC", "AAAALBBB")
        if code == "pain.001.003.01": old(old(tx, "FnlAgt"), "BIC", "AAAALBBB")
        old(tx, "ChrgBr", "SHA")
        if code == "pain.001.003.01": old(old(tx, "CdtrAcct"), "IBAN", account)
    ET.ElementTree(root).write(OUT / f"version-{code}.xml", encoding="utf-8", xml_declaration=True)
