<#
  Smoke test of the MSI on a clean machine (run elevated, e.g. a CI runner):
  install with properties, upgrade to a newer build keeping the properties,
  uninstall and verify nothing is left behind.

    ./installer/Test-Installer.ps1 -OldMsi a.msi -NewMsi b.msi
#>
param(
    [Parameter(Mandatory)] [string] $OldMsi,
    [Parameter(Mandatory)] [string] $NewMsi
)

$ErrorActionPreference = 'Stop'
$exe = Join-Path $env:ProgramFiles 'MustRead\MustRead.exe'
$runKey = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'
$appKey = 'HKLM:\SOFTWARE\MustRead'
$shortcut = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\MustRead.lnk'

function Invoke-Msi([string[]] $arguments) {
    $log = Join-Path $env:TEMP "mustread-msi-$([guid]::NewGuid().ToString('N')).log"
    $p = Start-Process msiexec.exe -ArgumentList ($arguments + @('/qn', '/norestart', '/l*v', $log)) -Wait -PassThru
    if ($p.ExitCode -ne 0) {
        Get-Content $log -Tail 60
        throw "msiexec $($arguments -join ' ') exit code $($p.ExitCode)"
    }
}

function Assert-True([bool] $condition, [string] $message) {
    if (-not $condition) { throw "FALLITO: $message" }
    Write-Host "ok - $message"
}

Write-Host '== Installazione con SERVERPATH e AUTOSTART'
Invoke-Msi @('/i', (Resolve-Path $OldMsi), 'SERVERPATH=\\server\mustread', 'AUTOSTART=1')
Assert-True (Test-Path $exe) 'eseguibile installato in Program Files'
Assert-True (Test-Path $shortcut) 'collegamento nel menu Start per tutti gli utenti'
Assert-True ((Get-ItemProperty $appKey).ServerPath -eq '\\server\mustread') 'SERVERPATH salvato in HKLM'
Assert-True ((Get-ItemProperty $runKey).MustRead -like '*MustRead.exe" --autostart') 'avvio automatico per tutti gli utenti'

Write-Host '== Aggiornamento senza ripassare le proprietà'
Invoke-Msi @('/i', (Resolve-Path $NewMsi))
Assert-True (Test-Path $exe) 'eseguibile presente dopo l''aggiornamento'
Assert-True ((Get-ItemProperty $appKey).ServerPath -eq '\\server\mustread') 'SERVERPATH conservato'
Assert-True ($null -ne (Get-ItemProperty $runKey -ErrorAction SilentlyContinue).MustRead) 'avvio automatico conservato'
$products = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue |
    Where-Object { $_.DisplayName -eq 'MustRead' }
Assert-True (@($products).Count -eq 1) 'una sola versione installata (major upgrade)'

Write-Host '== Disinstallazione'
Invoke-Msi @('/x', (Resolve-Path $NewMsi))
Assert-True (-not (Test-Path $exe)) 'eseguibile rimosso'
Assert-True (-not (Test-Path $shortcut)) 'collegamento rimosso'
Assert-True ($null -eq (Get-ItemProperty $runKey -ErrorAction SilentlyContinue).MustRead) 'avvio automatico rimosso'

Write-Host '== Installazione senza proprietà'
Invoke-Msi @('/i', (Resolve-Path $NewMsi))
Assert-True (Test-Path $exe) 'installazione base'
Assert-True ($null -eq (Get-ItemProperty $runKey -ErrorAction SilentlyContinue).MustRead) 'nessun avvio automatico se AUTOSTART non è 1'
Invoke-Msi @('/x', (Resolve-Path $NewMsi))

Write-Host 'Smoke test installer superato.'
