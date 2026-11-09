"""Generate the idempotent SQL Server 2016 configuration data script from the manifest."""
import json
from pathlib import Path

base = Path(__file__).resolve().parents[1]
manifest = json.loads((base / "backend/PaymentManagement/Iso20022/manifest.json").read_text(encoding="utf-8"))
lines = [
    "/* PaymentManagementDemo configuration. Apply after 001_schema.sql. No payment outcomes are seeded. */",
    "USE PaymentManagementDemo;",
    "GO",
    "SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON;",
    "SET CONCAT_NULL_YIELDS_NULL ON; SET QUOTED_IDENTIFIER ON; SET NUMERIC_ROUNDABORT OFF;",
    "GO",
    "IF NOT EXISTS(SELECT 1 FROM dbo.t_institutions WHERE code=N'ALFA') INSERT dbo.t_institutions(code,display_name) VALUES(N'ALFA',N'ALFA');",
    "IF NOT EXISTS(SELECT 1 FROM dbo.t_institutions WHERE code=N'AZADEYA') INSERT dbo.t_institutions(code,display_name) VALUES(N'AZADEYA',N'AZADEYA');",
    "GO",
]
for currency, value in (("EUR", "10000"), ("USD", "10000"), ("GBP", "10000"), ("LBP", "1000000000")):
    lines.append(f"IF NOT EXISTS(SELECT 1 FROM dbo.t_demo_routing_rules WHERE currency='{currency}') INSERT dbo.t_demo_routing_rules(currency,threshold) VALUES('{currency}',{value});")
lines.append("GO")
for item in manifest["definitions"]:
    code = item["code"]
    family = item["family"]
    ns = item["namespace"]
    filename = item["schemaFile"]
    sha = item["sha256"]
    source = item["schemaSource"]
    lines.append(f"IF NOT EXISTS(SELECT 1 FROM dbo.t_iso_definitions WHERE code='{code}')")
    lines.append(" INSERT dbo.t_iso_definitions(code,family,namespace_uri,schema_file,schema_sha256,source_url,is_tested)")
    lines.append(f" VALUES('{code}','{family}','{ns}','{filename}',CONVERT(binary(32),'{sha}',2),'{source}',1);")
lines.append("GO")
lines += [
    "DECLARE @alfa int = (SELECT id FROM dbo.t_institutions WHERE code=N'ALFA');",
    "DECLARE @azadeya int = (SELECT id FROM dbo.t_institutions WHERE code=N'AZADEYA');",
]
for item in manifest["definitions"]:
    if item["family"] != "pain.001":
        continue
    input_code = item["code"]
    output_code = item["counterparts"][-1]
    for institution in ("@alfa", "@azadeya"):
        lines.append(f"IF NOT EXISTS(SELECT 1 FROM dbo.t_response_mappings WHERE institution_id={institution} AND input_code='{input_code}')")
        lines.append(f" INSERT dbo.t_response_mappings(institution_id,input_code,output_code) VALUES({institution},'{input_code}','{output_code}');")
lines.append("GO")
(base / "sql/002_data.sql").write_text("\n".join(lines) + "\n", encoding="utf-8")
print("Wrote SQL seed for", len(manifest["definitions"]), "definitions")
