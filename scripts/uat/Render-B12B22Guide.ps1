#Requires -Version 7.2
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$guideRepo=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$guideSource=Join-Path $guideRepo 'docs/TDC_B12_B22_REHEARSAL_STEPS.md'
$guideTarget=Join-Path $guideRepo 'docs/TDC_B12_B22_REHEARSAL_STEPS.html'
$guideText=Get-Content -LiteralPath $guideSource -Raw
$guideBody=(ConvertFrom-Markdown -InputObject $guideText).Html
$guideBody=$guideBody.Replace('href="TDC_CUSTOMER_PROCUREMENT_STORES_WALKTHROUGH.md"','href="TDC_CUSTOMER_PROCUREMENT_STORES_WALKTHROUGH.html"')
$guideLinks=@(foreach($match in [regex]::Matches($guideBody,'<h2 id="([^"]+)">(B(?:1[2-9]|2[0-2]))\.[^<]*</h2>')){
 '<a href="#'+$match.Groups[1].Value+'">'+$match.Groups[2].Value+'</a>'
})
if($guideLinks.Count -ne 11){throw 'Expected exactly eleven B12-B22 sections.'}
$guideStageLabels=@([regex]::Matches(($guideLinks -join ''),'>B(\d+)</a>')|ForEach-Object{[int]$_.Groups[1].Value})
if(($guideStageLabels -join ',') -ne ((12..22) -join ',')){throw 'Rehearsal stages must appear once each, in B12-B22 order.'}
foreach($id in [regex]::Matches(($guideLinks -join ''),'href="#([^"]+)"')){
 if(-not $guideBody.Contains('id="'+$id.Groups[1].Value+'"')){throw 'Broken navigation anchor.'}
}
$guideTemplate=@'
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>B12–B22: step-by-step rehearsal</title>
<style>
:root{color-scheme:light;--ink:#17324b;--line:#dbe3eb}*{box-sizing:border-box}
body{margin:0;color:var(--ink);background:#f3f6f9;font:15px/1.5 "Segoe UI",Arial,sans-serif}
header{background:#153b60;color:white;padding:16px max(20px,calc((100vw - 920px)/2));font-weight:600}
header span{display:block;font-size:13px;font-weight:400;color:#d4e2ef}
nav{position:sticky;top:0;z-index:1;display:flex;flex-wrap:wrap;gap:6px;background:white;border-bottom:1px solid var(--line);padding:8px max(16px,calc((100vw - 920px)/2))}
nav a{padding:4px 10px;border:1px solid var(--line);border-radius:5px;text-decoration:none;font-size:13px}
main{max-width:960px;margin:16px auto;background:white;padding:26px 30px;border:1px solid var(--line);border-radius:8px}
h1{font-size:27px;line-height:1.2}h2{font-size:21px;margin-top:32px;border-top:1px solid var(--line);padding-top:20px;scroll-margin-top:100px}h3{font-size:17px;margin-top:22px}
p{margin:10px 0}ol,ul{padding-left:23px}li{margin:8px 0}a{color:#115e9a}code{font-size:.9em;background:#f0f4f8;padding:1px 3px;border-radius:3px;overflow-wrap:anywhere}
table{border-collapse:collapse;width:100%;font-size:14px;margin:14px 0}td,th{padding:7px 9px;border:1px solid var(--line);text-align:left;overflow-wrap:anywhere}th{background:#edf3f8}
footer{max-width:960px;margin:16px auto;padding:0 16px;color:#526779;font-size:13px}
@media(max-width:650px){main{margin:8px;padding:16px}h1{font-size:23px}h2{font-size:19px}table{font-size:12px}nav a{padding:3px 7px}}
@page{size:A4 portrait;margin:14mm}@media print{body{background:white;font-size:10pt;line-height:1.4}header,nav,footer{display:none}main{max-width:none;margin:0;padding:0;border:0}h1{font-size:20pt}h2{font-size:15pt;break-after:avoid;scroll-margin-top:0}h3{font-size:12pt;break-after:avoid}li{margin:5px 0;break-inside:avoid}table{font-size:9pt}tr{break-inside:avoid}thead{display:table-header-group}a{color:inherit;text-decoration:none}p{orphans:3;widows:3}}
</style></head><body>
<header>Rehearsal steps · B12–B22<span>Use the same record and environment after each account change · Print: Ctrl+P</span></header>
<nav aria-label="Rehearsal stages">__NAV__</nav><main>__BODY__</main>
<footer>Updated 13 September 2026. B12–B22 browser walkthrough in progress. Not signed off. <a href="TDC_B12_B22_READINESS.md">Preparation checklist</a></footer>
</body></html>
'@
$guideTemplate.Replace('__NAV__',($guideLinks -join '')).Replace('__BODY__',$guideBody)|Set-Content -LiteralPath $guideTarget -Encoding utf8
[pscustomobject]@{Source=$guideSource;Html=$guideTarget;Stages=$guideLinks.Count;Words=([regex]::Matches($guideText,'\S+')).Count;HtmlSha256=(Get-FileHash -LiteralPath $guideTarget).Hash}|ConvertTo-Json
