param([string]$BaseUrl = 'http://127.0.0.1:5000')
$ErrorActionPreference = 'Stop'
$base = $BaseUrl.TrimEnd('/')
$root = Split-Path -Parent $PSScriptRoot
$id = [guid]::NewGuid().ToString('N').Substring(0, 8)
$mixed = Join-Path ([IO.Path]::GetTempPath()) "pmd-mixed-$id.xml"
$duplicate = Join-Path ([IO.Path]::GetTempPath()) "pmd-duplicate-$id.xml"
$invalid = Join-Path ([IO.Path]::GetTempPath()) "pmd-invalid-$id.xml"
function Assert([bool]$condition, [string]$what) { if (-not $condition) { throw "FAILED: $what" }; Write-Host "PASS: $what" }
function Upload([int]$institutionId, [string]$path) {
    return Invoke-RestMethod -Method Post -Uri "$base/api/file-receipts/upload" -Form @{ institutionId = [string]$institutionId; file = (Get-Item -LiteralPath $path) }
}
function JsonPost([string]$path, [object]$body) {
    return Invoke-RestMethod -Method Post -Uri "$base$path" -ContentType 'application/json' -Body ($body | ConvertTo-Json -Depth 8)
}
try {
    $institutions = Invoke-RestMethod "$base/api/institutions"
    $alfa = $institutions.Where({$_.code -eq 'ALFA'})[0]
    Assert ($null -ne $alfa) 'ALFA seed exists'
    (Get-Content (Join-Path $root 'demo/samples/mixed-demo.xml') -Raw).Replace('MIXED-DEMO', "MIXED-$id") | Set-Content -Encoding utf8 $mixed
    Copy-Item -LiteralPath $mixed -Destination $duplicate
    (Get-Content (Join-Path $root 'demo/samples/all-invalid.xml') -Raw).Replace('ALL-INVALID', "INVALID-$id") | Set-Content -Encoding utf8 $invalid
    $arrival = Upload $alfa.id $mixed
    $receipt = Invoke-RestMethod "$base/api/file-receipts/$($arrival.receiptId)"
    Assert ($receipt.importState -eq 'Imported' -and $receipt.locationState -eq 'Archived') 'source imported and archived'
    $batchId = $receipt.batchId
    $view = Invoke-RestMethod "$base/api/batches/$batchId"
    Assert ($view.batch.transferCount -eq 7 -and $view.batch.systemRejectedCount -eq 3) 'all groups and automatic Invalid outcomes retained'
    Assert (-not $view.canFinalize) 'pending transfers block submission'
    foreach ($category in @('LOCAL','ACH','RTGS','SWIFT')) { JsonPost "/api/batches/$batchId/categories/$category/process" @{} | Out-Null }
    $all = Invoke-RestMethod "$base/api/batches/$batchId/transfers?take=100"
    $execution = $all.Where({$_.category -ne 'INVALID'})
    JsonPost "/api/batches/$batchId/transfer-decisions" @{items=@(@{id=$execution[0].id;version=$execution[0].version});decision='Approved';reason=$null} | Out-Null
    $stale = $false
    try { JsonPost "/api/batches/$batchId/transfer-decisions" @{items=@(@{id=$execution[0].id;version=$execution[0].version},@{id=$execution[1].id;version=$execution[1].version});decision='Rejected';reason='Stale test'} | Out-Null }
    catch { $stale = [int]$_.Exception.Response.StatusCode -eq 409 }
    $second = Invoke-RestMethod "$base/api/transfers/$($execution[1].id)"
    Assert ($stale -and $second.decision -eq 'Pending') 'stale bulk selection changes none of its transfers'
    $all = Invoke-RestMethod "$base/api/batches/$batchId/transfers?take=100"
    $execution = $all.Where({$_.category -ne 'INVALID'})
    $items = @($execution.ForEach({@{id=$_.id;version=$_.version}}))
    JsonPost "/api/batches/$batchId/transfer-decisions" @{items=$items;decision='Approved';reason=$null} | Out-Null
    $view = Invoke-RestMethod "$base/api/batches/$batchId"
    Assert ($view.canFinalize) 'all definitive outcomes unlock explicit submission'
    $report1 = JsonPost "/api/batches/$batchId/finalize" @{operationId=[guid]::NewGuid();expectedBatchVersion=$view.batch.version}
    $firstBytes = (Invoke-WebRequest "$base/api/reports/$($report1.reportId)/download").Content
    $firstHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($firstBytes)))
    $xml = [xml]$firstBytes
    Assert (@($xml.Document.CstmrPmtStsRpt.OrgnlPmtInfAndSts.TxInfAndSts).Count -eq 7) 'first pain.002 contains all seven transfer results'
    JsonPost "/api/batches/$batchId/reopen" @{} | Out-Null
    $local = (Invoke-RestMethod "$base/api/batches/$batchId/transfers?category=LOCAL")[0]
    JsonPost "/api/batches/$batchId/transfer-decisions" @{items=@(@{id=$local.id;version=$local.version});decision='Rejected';reason='Synthetic external rejection'} | Out-Null
    $view = Invoke-RestMethod "$base/api/batches/$batchId"
    $report2 = JsonPost "/api/batches/$batchId/finalize" @{operationId=[guid]::NewGuid();expectedBatchVersion=$view.batch.version}
    $reports = Invoke-RestMethod "$base/api/batches/$batchId/reports"
    Assert (@($reports).Count -eq 2 -and $report2.reportId -ne $report1.reportId) 'reopen creates an independent revision'
    $again = (Invoke-WebRequest "$base/api/reports/$($report1.reportId)/download").Content
    Assert ($firstHash -eq [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($again)))) 'earlier report bytes are unchanged'
    $duplicateArrival = Upload $alfa.id $duplicate
    $duplicateReceipt = Invoke-RestMethod "$base/api/file-receipts/$($duplicateArrival.receiptId)"
    Assert ($duplicateReceipt.importState -eq 'Duplicate' -and $duplicateReceipt.duplicateOf -eq $receipt.id) 'same-institution byte duplicate is linked'
    $invalidArrival = Upload $alfa.id $invalid
    $invalidReceipt = Invoke-RestMethod "$base/api/file-receipts/$($invalidArrival.receiptId)"
    $invalidBatch = Invoke-RestMethod "$base/api/batches/$($invalidReceipt.batchId)"
    Assert ($invalidBatch.canFinalize -and $invalidBatch.batch.pendingCount -eq 0) 'all-Invalid file needs no Process action'
    $invalidReport = JsonPost "/api/batches/$($invalidReceipt.batchId)/finalize" @{operationId=[guid]::NewGuid();expectedBatchVersion=$invalidBatch.batch.version}
    Assert ($null -ne $invalidReport.reportId) 'all-Invalid file can be explicitly submitted'
}
finally { foreach ($path in @($mixed,$duplicate,$invalid)) { Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue } }
