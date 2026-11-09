param([string]$BaseUrl = 'http://127.0.0.1:5000')

$ErrorActionPreference = 'Stop'
$base = $BaseUrl.TrimEnd('/')
$root = Split-Path -Parent $PSScriptRoot
$id = [guid]::NewGuid().ToString('N').Substring(0, 8)
$temp = Join-Path ([IO.Path]::GetTempPath()) "pmd-smoke-$id"

function Assert([bool]$condition, [string]$what) {
    if (-not $condition) { throw "FAILED: $what" }
    Write-Host "PASS: $what"
}

function New-BaseRequest {
    return @{
        correlationId = [guid]::NewGuid().ToString()
        userData = @{ userName = 'SmokeTest' }
    }
}

function Invoke-PaymentAction([string]$action, [hashtable]$fields = @{}, [int]$expectedStatus = 200) {
    $fields = $fields.Clone()
    $fields.baseReq = New-BaseRequest
    $response = Invoke-RestMethod -Method Post -Uri "$base/api/PaymentManagement/$action" -ContentType 'application/json' -Body ($fields | ConvertTo-Json -Depth 8)
    if ($response.baseResp.statusCode -ne $expectedStatus -or
        $response.baseResp.correlationId -ne $fields.baseReq.correlationId -or
        $response.req.baseReq.correlationId -ne $fields.baseReq.correlationId) {
        throw "Unexpected $action response: $($response.baseResp | ConvertTo-Json -Compress)"
    }
    if (($expectedStatus -eq 200) -ne ($response.baseResp.returnCode -eq '0x0000')) {
        throw "Unexpected $action return code: $($response.baseResp.returnCode)"
    }
    return $response
}

function Upload([int]$institutionId, [string]$path) {
    $baseReq = New-BaseRequest
    $form = @{
        institutionId = [string]$institutionId
        file = Get-Item -LiteralPath $path
        'baseReq.correlationId' = $baseReq.correlationId
        'baseReq.userData.userName' = $baseReq.userData.userName
    }
    $response = Invoke-RestMethod -Method Post -Uri "$base/api/PaymentManagement/UploadFile" -Form $form
    if ($response.baseResp.returnCode -ne '0x0000' -or $response.baseResp.correlationId -ne $baseReq.correlationId) {
        throw "Upload failed: $($response.baseResp | ConvertTo-Json -Compress)"
    }
    return $response.receiptId
}

function Download([string]$action, [hashtable]$fields, [string]$path) {
    $fields.baseReq = New-BaseRequest
    $response = Invoke-WebRequest -Method Post -Uri "$base/api/PaymentManagement/$action" -ContentType 'application/json' -Body ($fields | ConvertTo-Json -Depth 4) -OutFile $path -PassThru
    if (-not $response.Headers['Content-Disposition']) {
        throw "$action did not return an attachment."
    }
}

New-Item -ItemType Directory -Path $temp | Out-Null
try {
    $mixed = Join-Path $temp "pmd-mixed-$id.xml"
    $duplicate = Join-Path $temp "pmd-duplicate-$id.xml"
    $invalid = Join-Path $temp "pmd-invalid-$id.xml"
    $download = Join-Path $temp 'download.xml'

    $institutions = (Invoke-PaymentAction GetAllInstitutions).institutions
    $alfa = $institutions.Where({ $_.code -eq 'ALFA' })[0]
    Assert ($null -ne $alfa) 'ALFA seed exists and request correlation ID is echoed'
    $definitions = (Invoke-PaymentAction GetIsoDefinitions).definitions
    $rules = (Invoke-PaymentAction GetRoutingRules).rules
    $mappings = (Invoke-PaymentAction GetResponseMappings @{ institutionId = $alfa.id }).mappings
    Assert ($definitions.Count -eq 30 -and $rules.Count -gt 0 -and $mappings.Count -gt 0) 'settings load definitions, routing rules, and institution mappings'

    (Get-Content (Join-Path $root 'demo/samples/mixed-demo.xml') -Raw).Replace('MIXED-DEMO', "MIXED-$id") | Set-Content -Encoding utf8 $mixed
    Copy-Item -LiteralPath $mixed -Destination $duplicate
    (Get-Content (Join-Path $root 'demo/samples/all-invalid.xml') -Raw).Replace('ALL-INVALID', "INVALID-$id") | Set-Content -Encoding utf8 $invalid

    $receiptId = Upload $alfa.id $mixed
    $receipt = (Invoke-PaymentAction GetFileReceipt @{ receiptId = $receiptId }).receipt
    Assert ($receipt.importState -eq 'Imported' -and $receipt.locationState -eq 'Archived') 'source imported and archived'

    $queue = (Invoke-PaymentAction GetFileReceipts @{ institutionId = $alfa.id; search = "pmd-mixed-$id" }).receipts
    Assert ($queue.Count -eq 1 -and $queue[0].groupHeader.messageId -eq "MIXED-$id") 'queue includes original GrpHdr'
    Download DownloadOriginal @{ receiptId = $receiptId } $download
    Assert ((Get-FileHash $mixed).Hash -eq (Get-FileHash $download).Hash) 'original download retains source bytes'
    $missing = Invoke-PaymentAction DownloadOriginal @{ receiptId = [guid]::NewGuid() } 404
    Assert ($missing.baseResp.errors.Count -gt 0) 'missing download returns a correlated error envelope'

    $batchId = $receipt.batchId
    $view = (Invoke-PaymentAction GetBatch @{ batchId = $batchId }).batchView
    $groups = (Invoke-PaymentAction GetPaymentGroups @{ batchId = $batchId }).paymentGroups
    Assert ($groups.Count -eq 2) 'batch retains both payment groups'
    Assert ($view.batch.transferCount -eq 7 -and $view.batch.systemRejectedCount -eq 3) 'all groups and automatic Invalid outcomes retained'
    Assert (-not $view.canFinalize) 'pending transfers block submission'
    foreach ($category in @('LOCAL', 'ACH', 'RTGS', 'SWIFT')) {
        Invoke-PaymentAction ProcessCategory @{ batchId = $batchId; category = $category } | Out-Null
    }

    $all = (Invoke-PaymentAction GetTransfers @{ batchId = $batchId; take = 100 }).transfers
    $execution = $all.Where({ $_.category -ne 'INVALID' })
    $first = @{ id = $execution[0].id; version = $execution[0].version }
    $second = @{ id = $execution[1].id; version = $execution[1].version }
    Invoke-PaymentAction SaveTransferDecisions @{ batchId = $batchId; items = @($first); decision = 'Approved' } | Out-Null
    $stale = Invoke-PaymentAction SaveTransferDecisions @{ batchId = $batchId; items = @($first, $second); decision = 'Rejected'; reason = 'Stale test' } 409
    $unchanged = (Invoke-PaymentAction GetTransfer @{ transferId = $second.id }).transfer
    Assert ($stale.details[0].id -eq $first.id -and $unchanged.decision -eq 'Pending') 'stale bulk selection returns conflicting ID and changes no transfers'

    $all = (Invoke-PaymentAction GetTransfers @{ batchId = $batchId; take = 100 }).transfers
    $items = @($all.Where({ $_.category -ne 'INVALID' }).ForEach({ @{ id = $_.id; version = $_.version } }))
    Invoke-PaymentAction SaveTransferDecisions @{ batchId = $batchId; items = $items; decision = 'Approved' } | Out-Null
    $view = (Invoke-PaymentAction GetBatch @{ batchId = $batchId }).batchView
    Assert ($view.canFinalize) 'all definitive outcomes unlock explicit submission'

    $submission = @{ batchId = $batchId; operationId = [guid]::NewGuid(); expectedBatchVersion = $view.batch.version }
    $report1 = (Invoke-PaymentAction FinalizeBatch $submission).reportId
    $repeated = (Invoke-PaymentAction FinalizeBatch $submission).reportId
    Assert ($repeated -eq $report1) 'retrying the same final submission returns the existing report'
    Download DownloadReport @{ reportId = $report1 } $download
    $firstHash = (Get-FileHash $download).Hash
    $xml = [xml](Get-Content -LiteralPath $download -Raw)
    Assert (@($xml.Document.CstmrPmtStsRpt.OrgnlPmtInfAndSts.TxInfAndSts).Count -eq 7) 'first pain.002 contains all seven transfer results'

    Invoke-PaymentAction ReopenBatch @{ batchId = $batchId } | Out-Null
    $local = (Invoke-PaymentAction GetTransfers @{ batchId = $batchId; category = 'LOCAL' }).transfers[0]
    Invoke-PaymentAction SaveTransferDecisions @{
        batchId = $batchId
        items = @(@{ id = $local.id; version = $local.version })
        decision = 'Rejected'
        reason = 'Synthetic external rejection'
    } | Out-Null
    $view = (Invoke-PaymentAction GetBatch @{ batchId = $batchId }).batchView
    $report2 = (Invoke-PaymentAction FinalizeBatch @{ batchId = $batchId; operationId = [guid]::NewGuid(); expectedBatchVersion = $view.batch.version }).reportId
    $reports = (Invoke-PaymentAction GetReports @{ batchId = $batchId }).reports
    Assert ($reports.Count -eq 2 -and $report2 -ne $report1) 'reopen creates an independent revision'
    $audit = (Invoke-PaymentAction GetAudit @{ batchId = $batchId }).audit
    Assert ($audit.Where({ $_.action -eq 'Finalized' }).Count -eq 2) 'activity records both final submissions'
    Download DownloadReport @{ reportId = $report1 } $download
    Assert ($firstHash -eq (Get-FileHash $download).Hash) 'earlier report bytes are unchanged'

    $duplicateId = Upload $alfa.id $duplicate
    $duplicateReceipt = (Invoke-PaymentAction GetFileReceipt @{ receiptId = $duplicateId }).receipt
    Assert ($duplicateReceipt.importState -eq 'Duplicate' -and $duplicateReceipt.duplicateOf -eq $receiptId) 'same-institution byte duplicate is linked'
    $validation = (Invoke-PaymentAction GetValidationErrors @{ receiptId = $duplicateId }).validationErrors
    Assert ($validation.duplicateOf -eq $receiptId) 'validation action identifies the original receipt'
    Download DownloadReport @{ reportId = $duplicateReceipt.technicalDocumentId } $download
    Assert ($null -ne (Get-Content -LiteralPath $download -Raw | ConvertFrom-Json)) 'technical JSON downloads as an attachment'

    $invalidId = Upload $alfa.id $invalid
    $invalidReceipt = (Invoke-PaymentAction GetFileReceipt @{ receiptId = $invalidId }).receipt
    $invalidBatchId = $invalidReceipt.batchId
    $invalidBatch = (Invoke-PaymentAction GetBatch @{ batchId = $invalidBatchId }).batchView
    Assert ($invalidBatch.canFinalize -and $invalidBatch.batch.pendingCount -eq 0) 'all-Invalid file needs no Process action'
    $invalidReport = Invoke-PaymentAction FinalizeBatch @{ batchId = $invalidBatchId; operationId = [guid]::NewGuid(); expectedBatchVersion = $invalidBatch.batch.version }
    Assert ($null -ne $invalidReport.reportId) 'all-Invalid file can be explicitly submitted'
}
finally {
    Get-ChildItem -LiteralPath $temp -File | Remove-Item
    Remove-Item -LiteralPath $temp
}
