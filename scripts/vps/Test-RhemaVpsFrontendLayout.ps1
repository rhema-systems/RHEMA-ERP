[CmdletBinding()]
param([switch]$RunNextSmoke)
$ErrorActionPreference='Stop'
$repo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $PSScriptRoot 'Set-StagedFrontendRuntime.ps1')
function Assert-Test {param([bool]$Condition,[string]$Message) if(-not $Condition){throw $Message}}
$root=Join-Path $repo ('tmp\vps-layout-'+[guid]::NewGuid().ToString('N'))
[void](New-Item -ItemType Directory -Path $root)
$encoding=New-Object Text.UTF8Encoding($false)
foreach($directory in @('.next','.next-production')) {
    $fixture=Join-Path $root $directory.TrimStart('.')
    [void](New-Item -ItemType Directory -Path (Join-Path $fixture '.next'))
    [IO.File]::WriteAllText((Join-Path $fixture 'package.json'),'{"scripts":{"start":"node scripts/run-next-start.js","prestart":"node scripts/copy-assets.js"},"dependencies":{"next":"15.5.24"}}')
    [IO.File]::WriteAllText((Join-Path $fixture 'next.config.js'),"module.exports = {distDir: process.env.NEXT_DIST_DIR || '.next-production'};")
    $metadata=@{config=@{distDir=$directory};files=@("$directory/server/pages-manifest.json",'package.json')}
    [IO.File]::WriteAllText((Join-Path $fixture '.next/required-server-files.json'),($metadata|ConvertTo-Json -Depth 8))
    Set-StagedFrontendRuntime $fixture
    $package=Get-Content (Join-Path $fixture 'package.json') -Raw|ConvertFrom-Json
    $metadata=Get-Content (Join-Path $fixture '.next/required-server-files.json') -Raw|ConvertFrom-Json
    Assert-Test ($package.scripts.start -eq 'next start' -and -not $package.scripts.PSObject.Properties['prestart']) 'Runtime retained repository-only script dependencies.'
    Assert-Test ($metadata.config.distDir -eq '.next' -and $metadata.files[0] -eq '.next/server/pages-manifest.json' -and $metadata.files[1] -eq 'package.json') 'Staged manifest paths are inconsistent.'
    $previousDistDir=$env:NEXT_DIST_DIR
    $env:NEXT_DIST_DIR='.next-dev'
    try {
        $actual=& node -e "console.log(require(process.argv[1]).distDir)" (Join-Path $fixture 'next.config.js')
        Assert-Test ($LASTEXITCODE -eq 0 -and $actual -eq '.next') 'Runtime inherited an incorrect output directory.'
    } finally { [Environment]::SetEnvironmentVariable('NEXT_DIST_DIR',$previousDistDir,'Process') }
}
$source=Get-Content (Join-Path $repo 'scripts/Deploy-RhemaVps.ps1') -Raw
Assert-Test ($source.Contains("NEXT_DIST_DIR = '.next'") -and $source.Contains("NEXT_OUTPUT = ''")) 'Release build does not pin the expected regular output.'
Assert-Test ($source.Contains('Join-Path $frontendRoot $ReuseFrontendOutputDirectory') -and $source.Contains('Frontend source or build inputs changed; refusing to reuse')) 'Explicit reuse lost directory selection or source parity guard.'
Write-Host 'PASS: regular/reused output metadata, actual Node runtime configuration, startup scripts, explicit build/reuse layout.'
if(-not $RunNextSmoke){return}

$fixture=Join-Path $root 'real-next'
[void](New-Item -ItemType Directory -Path (Join-Path $fixture 'pages'))
[void](New-Item -ItemType Junction -Path (Join-Path $fixture 'node_modules') -Target (Join-Path $repo 'frontend/node_modules'))
[IO.File]::WriteAllText((Join-Path $fixture 'package.json'),'{"scripts":{"start":"node scripts/not-packaged.js","prestart":"node scripts/not-packaged-either.js"},"dependencies":{"next":"15.5.24","react":"19.1.5","react-dom":"19.1.5"}}',$encoding)
[IO.File]::WriteAllText((Join-Path $fixture 'next.config.js'),"module.exports = {distDir: process.env.NEXT_DIST_DIR || '.next-production', experimental: {cpus: 1}};",$encoding)
[IO.File]::WriteAllText((Join-Path $fixture 'pages/index.js'),"export default function Page(){return <main>VPS_LAYOUT_SMOKE_OK</main>} export async function getServerSideProps(){return {props:{}}}",$encoding)
[IO.File]::WriteAllText((Join-Path $fixture 'middleware.js'),"import {NextResponse} from 'next/server'; export function middleware(){const res=NextResponse.next();res.headers.set('x-vps-layout','ok');return res;} export const config={matcher:'/'};",$encoding)
$node=(Get-Command node).Source
$next=Join-Path $repo 'frontend/node_modules/next/dist/bin/next'
function New-TestStartInfo {param([string]$Arguments)
    $start=New-Object Diagnostics.ProcessStartInfo
    $start.FileName=$node;$start.Arguments=$Arguments;$start.WorkingDirectory=$fixture
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.EnvironmentVariables.Clear()
    foreach($name in @('PATH','SystemRoot','WINDIR','TEMP','TMP','USERPROFILE','APPDATA','LOCALAPPDATA')){
        $value=[Environment]::GetEnvironmentVariable($name,'Process');if($value){$start.EnvironmentVariables[$name]=$value}
    }
    $start.EnvironmentVariables['NODE_ENV']='production'
    $start.EnvironmentVariables['NEXT_TELEMETRY_DISABLED']='1'
    return $start
}
$build=New-Object Diagnostics.Process
$build.StartInfo=New-TestStartInfo ('"'+$next+'" build')
try{
    [void]$build.Start();$stdout=$build.StandardOutput.ReadToEndAsync();$stderr=$build.StandardError.ReadToEndAsync()
    if(-not $build.WaitForExit(180000)){$build.Kill();throw 'Fixture build timed out.'}
    $out=$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult()
    if($build.ExitCode -ne 0){throw "Fixture build failed: $out"}
}finally{$build.Dispose()}
Assert-Test (Test-Path (Join-Path $fixture '.next-production/BUILD_ID')) 'Fixture failed to reproduce the original output location.'
Copy-Item -LiteralPath (Join-Path $fixture '.next-production') -Destination (Join-Path $fixture '.next') -Recurse
Set-StagedFrontendRuntime $fixture
$listener=New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback,0)
$listener.Start();$port=$listener.LocalEndpoint.Port;$listener.Stop()
$server=New-Object Diagnostics.Process
$server.StartInfo=New-TestStartInfo ('"'+$next+'" start -H 127.0.0.1 -p '+$port)
try{
    [void]$server.Start();$stdout=$server.StandardOutput.ReadToEndAsync();$stderr=$server.StandardError.ReadToEndAsync()
    $passed=$false
    for($attempt=0;$attempt -lt 30;$attempt++){
        if($server.HasExited){throw 'Fixture server exited before readiness.'}
        try{
            $response=Invoke-WebRequest "http://127.0.0.1:$port/" -UseBasicParsing -TimeoutSec 2
            if($response.StatusCode -eq 200 -and $response.Content.Contains('VPS_LAYOUT_SMOKE_OK')){$passed=$true;break}
        }catch{}
        Start-Sleep -Milliseconds 500
    }
    Assert-Test $passed 'Repackaged production output failed to serve its compiled page.'
    Assert-Test ($response.Headers['x-vps-layout'] -eq 'ok') 'Repackaged middleware did not execute.'
    $scripts=[regex]::Matches($response.Content,'<script[^>]+src="([^"]+)"')
    Assert-Test ($scripts.Count -gt 0) 'Fixture emitted no script assets.'
    foreach($script in $scripts){
        $asset=Invoke-WebRequest ("http://127.0.0.1:$port"+$script.Groups[1].Value) -UseBasicParsing -TimeoutSec 5
        Assert-Test ($asset.StatusCode -eq 200) 'Compiled page script was not served from the repackaged output.'
    }
    Write-Host 'PASS: actual Next.js build into .next-production, staged as .next, HTTP 200 for SSR, middleware and script assets.'
}finally{
    if(-not $server.HasExited){$server.Kill();$server.WaitForExit()};$server.Dispose()
}
