#Requires -Version 5.1

[CmdletBinding()]
param(
    [ValidateNotNullOrEmpty()]
    [string]$Recipient = 'michael@rhema-systems.com.gh'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$smtpHostName = 'smtp.office365.com'
$smtpPort = 587
$senderAddress = 'rhemahrdmin@rhema-systems.com.gh'

Write-Host "Testing SMTP from $senderAddress to $Recipient via ${smtpHostName}:$smtpPort"
$securePassword = Read-Host "Enter the app password for $senderAddress" -AsSecureString
if ($securePassword.Length -eq 0) {
    throw 'No app password was entered.'
}

$credential = [System.Management.Automation.PSCredential]::new($senderAddress, $securePassword)
$smtpClient = $null
$mailMessage = $null

try {
    $smtpClient = [System.Net.Mail.SmtpClient]::new($smtpHostName, $smtpPort)
    $smtpClient.EnableSsl = $true
    $smtpClient.UseDefaultCredentials = $false
    $smtpClient.Credentials = $credential.GetNetworkCredential()
    $smtpClient.Timeout = 30000

    $subject = 'Rhema ERP VPS SMTP test ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
    $body = 'This is a test email from the Rhema ERP VPS using the rhemahrdmin mailbox.'
    $mailMessage = [System.Net.Mail.MailMessage]::new($senderAddress, $Recipient, $subject, $body)

    $smtpClient.Send($mailMessage)
    Write-Output "SMTP send succeeded. Check $Recipient for the test email."
}
catch {
    $exception = $_.Exception
    while ($exception.InnerException) {
        $exception = $exception.InnerException
    }

    throw "SMTP send failed: $($exception.Message)"
}
finally {
    if ($mailMessage) {
        $mailMessage.Dispose()
    }
    if ($smtpClient) {
        $smtpClient.Dispose()
    }
}
