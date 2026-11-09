param(
    [Parameter(Mandatory = $true)][ValidateSet('Install','Start','Stop','Uninstall')][string]$Action,
    [string]$ServiceName = 'PaymentManagementDemo',
    [string]$Executable = ''
)
$ErrorActionPreference = 'Stop'
switch ($Action) {
    'Install' {
        if (-not $Executable) { throw 'Pass -Executable with the full path to PaymentManagement.Api.exe.' }
        $resolved = (Resolve-Path -LiteralPath $Executable).Path
        if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) { throw "Service $ServiceName already exists." }
        New-Service -Name $ServiceName -DisplayName 'Payment Management Demo' -BinaryPathName ('"' + $resolved + '"') -StartupType Automatic
        Write-Host "Installed $ServiceName. Configure its service identity and permissions before starting."
    }
    'Start' { Start-Service -Name $ServiceName }
    'Stop' { Stop-Service -Name $ServiceName }
    'Uninstall' {
        $service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
        if ($service -and $service.Status -ne 'Stopped') { Stop-Service -Name $ServiceName }
        if ($service) { & sc.exe delete $ServiceName }
    }
}
