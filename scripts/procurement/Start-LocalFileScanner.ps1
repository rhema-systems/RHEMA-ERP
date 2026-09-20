# Restore the existing local scanner without weakening the clean-scan requirement.
$ErrorActionPreference = 'Stop'
$scannerExe = 'C:\Program Files\ClamAV\clamd.exe'
$scannerConfig = 'C:\ProgramData\ClamAV\clamd.conf'

function Test-LocalScannerPing {
    $scannerClient = [Net.Sockets.TcpClient]::new()
    try {
        if (-not $scannerClient.ConnectAsync('127.0.0.1', 3310).Wait(1000)) { return $false }
        $scannerStream = $scannerClient.GetStream()
        $scannerStream.ReadTimeout = 1000
        $scannerStream.WriteTimeout = 1000
        $scannerPing = [Text.Encoding]::ASCII.GetBytes("zPING`0")
        $scannerStream.Write($scannerPing, 0, $scannerPing.Length)
        $scannerReply = [Collections.Generic.List[byte]]::new()
        while ($scannerReply.Count -lt 32) {
            $scannerByte = $scannerStream.ReadByte()
            if ($scannerByte -le 0 -or $scannerByte -eq 10) { break }
            $scannerReply.Add([byte]$scannerByte)
        }
        return [Text.Encoding]::ASCII.GetString($scannerReply.ToArray()).Trim() -eq 'PONG'
    } catch { return $false }
    finally { $scannerClient.Dispose() }
}

$scannerListeners = @(Get-NetTCPConnection -LocalPort 3310 -State Listen -ErrorAction SilentlyContinue)
if ($scannerListeners | Where-Object { $_.LocalAddress -notin @('127.0.0.1', '::1') }) {
    throw 'Scanner port 3310 must be loopback-only. No service or firewall settings were changed.'
}
if (Test-LocalScannerPing) {
    Write-Host 'Local file scanner is ready.'
    return
}
if ($scannerListeners.Count -gt 0) {
    throw 'Port 3310 is occupied but the scanner did not answer PING. Check ClamAV before starting rehearsal.'
}
if (-not (Test-Path -LiteralPath $scannerExe) -or -not (Test-Path -LiteralPath $scannerConfig)) {
    throw 'The local ClamAV installation/configuration is missing. A working malware scanner is required for uploads.'
}
$scannerSettings = Get-Content -LiteralPath $scannerConfig
$scannerAddress = @($scannerSettings | Where-Object { $_ -match '^\s*TCPAddr\s+' })
$scannerPort = @($scannerSettings | Where-Object { $_ -match '^\s*TCPSocket\s+' })
if ($scannerAddress.Count -ne 1 -or $scannerAddress[0].Trim() -ne 'TCPAddr 127.0.0.1' -or
    $scannerPort.Count -ne 1 -or $scannerPort[0].Trim() -ne 'TCPSocket 3310') {
    throw 'The existing scanner configuration must bind only 127.0.0.1:3310. No configuration was changed.'
}
# If a previous launch is still loading its signature database, wait for it instead of duplicating it.
$scannerProcess = Get-Process -Name clamd -ErrorAction SilentlyContinue
if (-not $scannerProcess) {
    $scannerProcess = Start-Process -FilePath $scannerExe -ArgumentList ('--config-file="' + $scannerConfig + '"') `
        -WorkingDirectory (Split-Path -Path $scannerExe -Parent) -WindowStyle Hidden -PassThru
}
$scannerDeadline = [DateTime]::UtcNow.AddSeconds(45)
while ([DateTime]::UtcNow -lt $scannerDeadline) {
    if (Test-LocalScannerPing) { Write-Host 'Local file scanner is ready.'; return }
    Start-Sleep -Milliseconds 500
}
throw 'ClamAV is not ready yet. Check C:\ProgramData\ClamAV\logs\clamd.log and retry. Clean-scan protection remains enabled.'
