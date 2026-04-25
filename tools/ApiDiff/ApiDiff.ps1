#requires -Version 7.0
<#
.SYNOPSIS
  API parity harness — captures + compares JSON responses across two backend instances.

.PARAMETER Mode
  Capture | Compare

.PARAMETER BaseUrl
  Required for Capture. Base URL of the API (e.g. http://localhost:8000).

.PARAMETER OutDir
  Required for Capture. Where to write per-endpoint JSON snapshots.

.PARAMETER BaselineDir
  Required for Compare. Directory of captured baseline.

.PARAMETER CurrentUrl
  Required for Compare. Base URL of the API to compare against the baseline.

.PARAMETER ReportPath
  Required for Compare. Where to write parity-report.md.

.NOTES
  - Strips volatile fields (createdAt, updatedAt, traceId, expiresAt, lastActivityAt, ip, userAgent, deviceName, ETag headers) before diff.
  - Detail-tier endpoints get full body diff. Smoke-tier only diffs status code + array length.
#>
param(
  [Parameter(Mandatory=$true)][ValidateSet('Capture','Compare')][string]$Mode,
  [string]$BaseUrl,
  [string]$OutDir,
  [string]$BaselineDir,
  [string]$CurrentUrl,
  [string]$ReportPath,
  [string]$ConfigPath = "$PSScriptRoot/endpoints.json"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

# -- volatile field strip list ------------------------------------------------
$Script:VolatileKeys = @(
  'createdAt','updatedAt','deletedAt','expiresAt','lastActivityAt',
  'traceId','etag','eTag','ETag','requestId',
  'ip','ipAddress','userAgent','deviceName',
  'magicLinkToken','sessionToken','accessToken','refreshToken','jwt',
  'qrToken','claimToken'
)

function Normalize-Value {
  param($v)
  if ($null -eq $v) { return $null }
  if ($v -is [System.Collections.IDictionary]) {
    $sorted = [ordered]@{}
    foreach ($k in ($v.Keys | Sort-Object)) {
      if ($Script:VolatileKeys -contains $k) {
        $sorted[$k] = '<<VOLATILE>>'
      } else {
        $sorted[$k] = Normalize-Value $v[$k]
      }
    }
    return $sorted
  }
  if ($v -is [PSCustomObject]) {
    $sorted = [ordered]@{}
    foreach ($p in ($v.PSObject.Properties.Name | Sort-Object)) {
      if ($Script:VolatileKeys -contains $p) {
        $sorted[$p] = '<<VOLATILE>>'
      } else {
        $sorted[$p] = Normalize-Value $v.$p
      }
    }
    return $sorted
  }
  if ($v -is [System.Collections.IEnumerable] -and $v -isnot [string]) {
    $arr = @()
    foreach ($item in $v) { $arr += ,(Normalize-Value $item) }
    return ,$arr
  }
  return $v
}

function To-NormalJson {
  param($obj)
  $n = Normalize-Value $obj
  return ($n | ConvertTo-Json -Depth 100 -Compress:$false)
}

# -- HTTP helpers -------------------------------------------------------------
function New-Session {
  $session = [Microsoft.PowerShell.Commands.WebRequestSession]::new()
  return $session
}

function Login-Role {
  param([string]$BaseUrl,[hashtable]$AuthCfg)
  $sess = New-Session
  $headers = @{ 'X-Portal' = $AuthCfg.portal; 'Content-Type' = 'application/json' }
  $bodyJson = ($AuthCfg.body | ConvertTo-Json -Compress)
  $url = "$BaseUrl$($AuthCfg.endpoint)"
  try {
    $resp = Invoke-WebRequest -Uri $url -Method POST -Headers $headers -Body $bodyJson `
      -WebSession $sess -SkipHttpErrorCheck -SkipCertificateCheck
    if ([int]$resp.StatusCode -ge 200 -and [int]$resp.StatusCode -lt 300) { return $sess }
    Write-Warning "Login failed for $($AuthCfg.portal): HTTP $([int]$resp.StatusCode)"
    return $null
  } catch {
    Write-Warning "Login error for $($AuthCfg.portal): $_"
    return $null
  }
}

function Invoke-Endpoint {
  param(
    [string]$BaseUrl,
    [string]$Path,
    [Microsoft.PowerShell.Commands.WebRequestSession]$Session,
    [string]$Portal
  )
  $headers = @{ 'Accept' = 'application/json' }
  if ($Portal) { $headers['X-Portal'] = $Portal }
  $url = "$BaseUrl$Path"
  $resp = $null
  try {
    $resp = Invoke-WebRequest -Uri $url -Method GET -Headers $headers `
      -WebSession $Session -SkipHttpErrorCheck -SkipCertificateCheck `
      -MaximumRedirection 0
  } catch {
    return @{ status = -1; body = $null; error = $_.Exception.Message }
  }
  $body = $null
  $ctRaw = $resp.Headers['Content-Type']
  $contentType = if ($ctRaw) { ($ctRaw | Select-Object -First 1) } else { '' }
  if ($contentType -like 'application/json*' -and $resp.Content) {
    try { $body = $resp.Content | ConvertFrom-Json -Depth 100 -AsHashtable } catch { $body = $resp.Content }
  } else {
    $body = $resp.Content
  }
  return @{ status = [int]$resp.StatusCode; body = $body; contentType = $contentType }
}

# -- main ---------------------------------------------------------------------
$cfg = Get-Content $ConfigPath -Raw | ConvertFrom-Json -AsHashtable
$endpoints = $cfg.endpoints

function Get-NestedValue {
  param($obj, [string]$Path)
  # Path like "items[0].eventId" — split on '.' and '[N]' tokens
  $cur = $obj
  $tokens = [System.Collections.Generic.List[string]]::new()
  $buf = ''
  $i = 0
  while ($i -lt $Path.Length) {
    $c = $Path[$i]
    if ($c -eq '.') {
      if ($buf) { $tokens.Add($buf); $buf = '' }
      $i++; continue
    }
    if ($c -eq '[') {
      if ($buf) { $tokens.Add($buf); $buf = '' }
      $end = $Path.IndexOf(']', $i)
      $tokens.Add($Path.Substring($i+1, $end-$i-1))
      $i = $end + 1; continue
    }
    $buf += $c; $i++
  }
  if ($buf) { $tokens.Add($buf) }
  foreach ($t in $tokens) {
    if ($null -eq $cur) { return $null }
    if ($t -match '^\d+$') {
      $idx = [int]$t
      if ($cur -is [System.Collections.IList]) { $cur = $cur[$idx] } else { return $null }
    } else {
      if ($cur -is [System.Collections.IDictionary]) { $cur = $cur[$t] }
      elseif ($cur -is [PSCustomObject] -and $cur.PSObject.Properties[$t]) { $cur = $cur.$t }
      else { return $null }
    }
  }
  return $cur
}

function Resolve-Fixtures {
  param([string]$BaseUrl, [hashtable]$Sessions, [hashtable]$Cfg)
  $fixtures = @{}
  foreach ($name in $Cfg.fixtures.Keys) {
    if ($name -eq 'comment') { continue }
    $fx = $Cfg.fixtures[$name]
    $sess = if ($fx.auth) { $Sessions[$fx.auth] } else { $null }
    $portal = if ($fx.auth) { $Cfg.auth[$fx.auth].portal } else { $null }
    $r = Invoke-Endpoint -BaseUrl $BaseUrl -Path $fx.probe -Session $sess -Portal $portal
    if ($r.status -ge 200 -and $r.status -lt 300 -and $r.body) {
      $val = Get-NestedValue $r.body $fx.extract
      if ($val) { $fixtures[$name] = $val.ToString() }
    }
    if (-not $fixtures.ContainsKey($name)) {
      Write-Warning "Fixture '$name' could not be resolved (probe=$($fx.probe), status=$($r.status))"
    }
  }
  return $fixtures
}

function Resolve-Path {
  param([string]$Template, [hashtable]$Fixtures)
  $p = $Template
  foreach ($k in $Fixtures.Keys) {
    $p = $p -replace ("\{" + [regex]::Escape($k) + "\}"), $Fixtures[$k]
  }
  return $p
}

function Run-Capture {
  param([string]$BaseUrl, [string]$OutDir)
  if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }

  $sessions = @{}
  foreach ($role in $cfg.auth.Keys) {
    $sessions[$role] = Login-Role -BaseUrl $BaseUrl -AuthCfg $cfg.auth[$role]
    Write-Host "auth $role : $(if ($sessions[$role]) {'ok'} else {'fail'})"
  }

  $fixtures = Resolve-Fixtures -BaseUrl $BaseUrl -Sessions $sessions -Cfg $cfg
  $fixtures.GetEnumerator() | ForEach-Object { Write-Host "fixture $($_.Key) = $($_.Value)" }
  $fixtures | ConvertTo-Json | Out-File -FilePath (Join-Path $OutDir '_fixtures.json') -Encoding utf8

  $manifest = @()
  foreach ($ep in $endpoints) {
    $resolvedPath = Resolve-Path $ep.path $fixtures
    if ($resolvedPath -match '\{[^}]+\}') {
      Write-Warning "skip $($ep.id) : unresolved fixture in $resolvedPath"
      continue
    }
    $sess = if ($ep.auth) { $sessions[$ep.auth] } else { $null }
    $portal = if ($ep.auth) { $cfg.auth[$ep.auth].portal } else { $null }
    $r = Invoke-Endpoint -BaseUrl $BaseUrl -Path $resolvedPath -Session $sess -Portal $portal
    $entry = @{
      id = $ep.id
      method = $ep.method
      path = $resolvedPath
      auth = $ep.auth
      tier = $ep.tier
      status = $r.status
      contentType = $r.contentType
    }
    if ($ep.tier -eq 'detail' -and $r.body) {
      $entry.body = (Normalize-Value $r.body)
    } elseif ($r.body -is [System.Collections.IDictionary] -and $r.body.ContainsKey('items')) {
      $entry.itemCount = @($r.body.items).Count
    } elseif ($r.body -is [System.Collections.IEnumerable] -and $r.body -isnot [string]) {
      $entry.itemCount = @($r.body).Count
    }
    $entry | ConvertTo-Json -Depth 100 | Out-File -FilePath (Join-Path $OutDir "$($ep.id).json") -Encoding utf8
    Write-Host ("[{0}] {1,-40} {2}" -f $r.status, $ep.id, $resolvedPath)
    $manifest += $entry
  }
  $manifest | ConvertTo-Json -Depth 100 | Out-File -FilePath (Join-Path $OutDir '_manifest.json') -Encoding utf8
  Write-Host "captured $($manifest.Count) endpoints to $OutDir"
}

function Run-Compare {
  param([string]$BaselineDir, [string]$CurrentUrl, [string]$ReportPath)
  $baselineManifestPath = Join-Path $BaselineDir '_manifest.json'
  if (-not (Test-Path $baselineManifestPath)) { throw "baseline manifest missing: $baselineManifestPath" }
  $baselineManifest = Get-Content $baselineManifestPath -Raw | ConvertFrom-Json -AsHashtable

  # Capture current into a temp dir
  $currentDir = Join-Path ([System.IO.Path]::GetTempPath()) ("apidiff-current-" + [guid]::NewGuid())
  Write-Host "capturing current: $CurrentUrl -> $currentDir"
  Run-Capture -BaseUrl $CurrentUrl -OutDir $currentDir

  $rows = @()
  $pass = 0; $fail = 0
  foreach ($base in $baselineManifest) {
    $cur = Get-Content (Join-Path $currentDir "$($base.id).json") -Raw -ErrorAction SilentlyContinue | ConvertFrom-Json -AsHashtable
    $row = @{ id = $base.id; path = $base.path; tier = $base.tier; baselineStatus = $base.status }
    if (-not $cur) { $row.result = 'MISSING'; $row.detail = 'no current capture'; $fail++; $rows += $row; continue }
    $row.currentStatus = $cur.status
    if ($base.status -ne $cur.status) {
      $row.result = 'STATUS_MISMATCH'; $row.detail = "$($base.status) -> $($cur.status)"; $fail++; $rows += $row; continue
    }
    if ($base.tier -eq 'detail') {
      $bJ = $base.body | ConvertTo-Json -Depth 100 -Compress
      $cJ = $cur.body  | ConvertTo-Json -Depth 100 -Compress
      if ($bJ -ne $cJ) {
        $row.result = 'BODY_MISMATCH'
        # crude diff: list keys present in one but not the other; compare top-level keys
        $row.detail = "body bytes differ ($([math]::Abs($bJ.Length - $cJ.Length)) byte delta)"
        $fail++
      } else {
        $row.result = 'PASS'; $pass++
      }
    } else {
      if ($base.itemCount -ne $cur.itemCount) {
        $row.result = 'COUNT_MISMATCH'; $row.detail = "items $($base.itemCount) -> $($cur.itemCount)"; $fail++
      } else {
        $row.result = 'PASS'; $pass++
      }
    }
    $rows += $row
  }

  $sb = [System.Text.StringBuilder]::new()
  [void]$sb.AppendLine("# API parity report")
  [void]$sb.AppendLine("")
  [void]$sb.AppendLine("- Baseline: ``$BaselineDir``")
  [void]$sb.AppendLine("- Current:  ``$CurrentUrl``")
  [void]$sb.AppendLine("- Pass: $pass   Fail: $fail   Total: $($rows.Count)")
  [void]$sb.AppendLine("")
  [void]$sb.AppendLine("| id | tier | baseline | current | result | detail |")
  [void]$sb.AppendLine("|---|---|---|---|---|---|")
  foreach ($r in $rows) {
    [void]$sb.AppendLine(("| {0} | {1} | {2} | {3} | {4} | {5} |" -f $r.id, $r.tier, $r.baselineStatus, ($r.currentStatus ?? '-'), $r.result, ($r.detail ?? '')))
  }
  Set-Content -Path $ReportPath -Value $sb.ToString() -Encoding utf8
  Write-Host "report -> $ReportPath  (pass=$pass fail=$fail)"
  if ($fail -gt 0) { exit 1 }
}

switch ($Mode) {
  'Capture' {
    if (-not $BaseUrl) { throw '-BaseUrl required for Capture' }
    if (-not $OutDir)  { throw '-OutDir required for Capture' }
    Run-Capture -BaseUrl $BaseUrl -OutDir $OutDir
  }
  'Compare' {
    if (-not $BaselineDir) { throw '-BaselineDir required for Compare' }
    if (-not $CurrentUrl)  { throw '-CurrentUrl required for Compare' }
    if (-not $ReportPath)  { throw '-ReportPath required for Compare' }
    Run-Compare -BaselineDir $BaselineDir -CurrentUrl $CurrentUrl -ReportPath $ReportPath
  }
}
