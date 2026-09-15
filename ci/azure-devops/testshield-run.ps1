[CmdletBinding()]
param(
    [string]$BaseUrl = $env:TESTSHIELD_BASE_URL,
    [string]$ProjectId = $env:TESTSHIELD_PROJECT_ID,
    [string]$Token = $env:TESTSHIELD_TOKEN,
    [int]$TimeoutSeconds = 300
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-PlaceholderValue([string]$Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $true
    }

    return $Value -match '^\$\(.+\)$'
}

function Protect-Secret([string]$Text, [string]$Secret) {
    if ([string]::IsNullOrEmpty($Text) -or [string]::IsNullOrEmpty($Secret)) {
        return $Text
    }

    return $Text.Replace($Secret, "***")
}

function Get-ExitCode([int]$StatusCode, [string]$Decision) {
    if ($StatusCode -lt 200 -or $StatusCode -ge 300) {
        return 1
    }

    switch -Regex ($Decision) {
        '^(?i)safe$' { return 0 }
        '^(?i)review$' { return 0 }
        default { return 1 }
    }
}

function Get-FindingTag([string]$Category) {
    switch -Regex ($Category) {
        '^(?i)ContractDrift$' { return "DRIFT" }
        '^(?i)Regression$' { return "BLOCK" }
        '^(?i)ExecutionFailure$' { return "EXEC" }
        default { return "INFO" }
    }
}

function Get-CoverageText($Coverage) {
    if ($null -eq $Coverage -or $null -eq $Coverage.coveragePercent) {
        return "unavailable"
    }

    $percent = [decimal]$Coverage.coveragePercent
    if ($percent -eq [math]::Truncate($percent)) {
        return ("{0}%" -f [int]$percent)
    }

    return ("{0}%" -f $percent.ToString("0.##", [System.Globalization.CultureInfo]::InvariantCulture))
}

if (Test-PlaceholderValue $BaseUrl) {
    Write-Error "TestShield base URL is required. Set -BaseUrl or TESTSHIELD_BASE_URL."
    exit 1
}

if (Test-PlaceholderValue $ProjectId) {
    Write-Error "TestShield project ID is required. Set -ProjectId or TESTSHIELD_PROJECT_ID."
    exit 1
}

if (Test-PlaceholderValue $Token) {
    $Token = $null
}

$BaseUrl = $BaseUrl.Trim().TrimEnd("/")
$uri = "{0}/api/projects/{1}/tests/run" -f $BaseUrl, $ProjectId.Trim()
$headers = @{
    Accept = "application/json"
}

if (-not [string]::IsNullOrWhiteSpace($Token)) {
    $headers["Authorization"] = "Bearer $Token"
}

$statusCode = 0
$body = $null
try {
    $response = Invoke-WebRequest -Uri $uri -Method POST -Headers $headers -ContentType "application/json" -TimeoutSec $TimeoutSeconds
    $statusCode = [int]$response.StatusCode
    $body = $response.Content
}
catch {
    $message = Protect-Secret $_.Exception.Message $Token
    if ($_.Exception.Response) {
        try {
            $statusCode = [int]$_.Exception.Response.StatusCode
            $stream = $_.Exception.Response.GetResponseStream()
            if ($stream) {
                $reader = New-Object System.IO.StreamReader($stream)
                $body = $reader.ReadToEnd()
            }
        }
        catch {
            $statusCode = 0
        }
    }

    if ([string]::IsNullOrWhiteSpace($body)) {
        Write-Host "TestShield AI Regression Result"
        Write-Host "Request failed: $message"
        exit 1
    }
}

if ($statusCode -lt 200 -or $statusCode -ge 300) {
    Write-Host "TestShield AI Regression Result"
    Write-Host ("HTTP {0}" -f $statusCode)
    if (-not [string]::IsNullOrWhiteSpace($body)) {
        Write-Host (Protect-Secret $body $Token)
    }
    exit 1
}

try {
    $payload = $body | ConvertFrom-Json
}
catch {
    Write-Host "TestShield AI Regression Result"
    Write-Host "The TestShield response was not valid JSON."
    exit 1
}

$decision = [string]$payload.decision
$risk = $payload.risk
$coverage = $payload.coverage
$findings = @()
if ($payload.findings) {
    $findings = @($payload.findings)
}

$riskLevel = ""
$riskTitle = ""
$reasons = @()
if ($risk) {
    $riskLevel = [string]$risk.level
    $riskTitle = [string]$risk.title
    if ($risk.reasons) {
        $reasons = @($risk.reasons)
    }
}

$passed = 0
$failed = 0
$errors = 0
if ($payload.results) {
    foreach ($item in @($payload.results)) {
        $outcome = ""
        if ($item.validation) {
            $outcome = [string]$item.validation.outcome
        }
        switch -Regex ($outcome) {
            '^(?i)passed$' { $passed++ }
            '^(?i)failed$' { $failed++ }
            '^(?i)error$' { $errors++ }
        }
    }
}

$riskText = "unavailable"
if (-not [string]::IsNullOrWhiteSpace($riskLevel) -and -not [string]::IsNullOrWhiteSpace($riskTitle)) {
    $riskText = "{0} - {1}" -f $riskLevel, $riskTitle
}
elseif (-not [string]::IsNullOrWhiteSpace($riskLevel)) {
    $riskText = $riskLevel
}
elseif (-not [string]::IsNullOrWhiteSpace($riskTitle)) {
    $riskText = $riskTitle
}

$decisionText = "UNKNOWN"
if (-not [string]::IsNullOrWhiteSpace($decision)) {
    $decisionText = $decision.ToUpperInvariant()
}

Write-Host "TestShield AI Regression Result"
Write-Host ("Decision: {0}" -f $decisionText)
Write-Host ("Risk: {0}" -f $riskText)
Write-Host ("Coverage: {0}" -f (Get-CoverageText $coverage))
Write-Host ("Findings: {0}" -f $findings.Count)
if (($passed + $failed + $errors) -gt 0) {
    Write-Host ("Results: {0} passed, {1} failed, {2} errors" -f $passed, $failed, $errors)
}

$detailPrinted = $false
if ($reasons.Count -gt 0) {
    Write-Host ""
    for ($index = 0; $index -lt $reasons.Count; $index++) {
        $tag = "INFO"
        if ($index -lt $findings.Count) {
            $tag = Get-FindingTag ([string]$findings[$index].category)
        }
        Write-Host ("[{0}] {1}" -f $tag, $reasons[$index])
    }
    $detailPrinted = $true
}

if (-not $detailPrinted -and $findings.Count -gt 0) {
    Write-Host ""
    foreach ($finding in $findings) {
        $message = [string]$finding.message
        if ([string]::IsNullOrWhiteSpace($message)) {
            $message = [string]$finding.code
        }
        if (-not [string]::IsNullOrWhiteSpace($message)) {
            Write-Host ("[{0}] {1}" -f (Get-FindingTag ([string]$finding.category)), $message)
        }
    }
}

exit (Get-ExitCode $statusCode $decision)
