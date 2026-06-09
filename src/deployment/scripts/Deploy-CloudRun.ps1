#requires -Version 7.0
<#
.SYNOPSIS
    End-to-end: register a service, environment, and Cloud Run target, publish a
    container release, and deploy it to Google Cloud Run via the Deployment API.

.DESCRIPTION
    Drives the Deployment.Api REST surface to model and run a container deployment
    to Google Cloud Run. Each step is idempotent: existing services / environments
    / targets / releases (matched by name, resource id, or version) are reused
    rather than recreated, so the script is safe to re-run.

    The API itself talks to Cloud Run via the GoogleCloudRunDeploymentAdapter,
    which authenticates with Application Default Credentials (set
    GOOGLE_APPLICATION_CREDENTIALS or run under Workload Identity). The target
    Cloud Run service must already exist — the adapter updates it in place.

.EXAMPLE
    ./Deploy-CloudRun.ps1 `
        -GcpProject my-proj -Region us-central1 -CloudRunService orders-api `
        -Image "us-docker.pkg.dev/my-proj/repo/orders-api@sha256:abc123..." `
        -SemanticVersion 1.4.0 -BuildNumber 42 -CommitSha abc1234

.EXAMPLE
    # Production env that gates on approval (4-eyes): triggerer and approver differ.
    ./Deploy-CloudRun.ps1 -GcpProject my-proj -Region us-central1 -CloudRunService orders-api `
        -Image "us-docker.pkg.dev/my-proj/repo/orders-api@sha256:abc123..." `
        -EnvironmentName Production -PromotionRank 4 -RequiresApproval -IsProduction `
        -TriggeredBy "ci@heydt.org" -Approver "mike@heydt.org"
#>
[CmdletBinding()]
param(
    # --- API endpoint ---
    [string] $BaseUrl = "http://localhost:9601",   # http profile from launchSettings.json

    # --- The service (deployable unit) ---
    [string] $ServiceName     = "orders-api",
    [int]    $ServiceKind     = 0,                  # 0=WebApi 1=Mvc 2=WorkerService 3=AzureFunction 4=Console 5=Other
    [string] $RepositoryUrl   = "https://example.com/orders-api.git",
    [string] $TargetFramework = "net10.0",

    # --- Google Cloud Run target ---
    [Parameter(Mandatory)] [string] $GcpProject,
    [Parameter(Mandatory)] [string] $Region,
    [Parameter(Mandatory)] [string] $CloudRunService,

    # --- The release (container image) ---
    [Parameter(Mandatory)] [string] $Image,        # image ref; digest form recommended
    [string] $SemanticVersion = "1.0.0",
    [string] $BuildNumber     = "1",
    [string] $CommitSha       = "0000000",

    # --- The environment ---
    [string] $EnvironmentName = "Development",
    [int]    $PromotionRank   = 1,
    [switch] $RequiresApproval,
    [switch] $IsProduction,

    # --- Deployment metadata ---
    [int]    $Strategy     = 0,                     # 0=Direct 1=BlueGreen 2=Canary 3=Rolling (Direct only, in v1)
    [int]    $Trigger      = 1,                     # 0=Manual 1=Pipeline 2=AutoPromote
    [string] $TriggeredBy  = "deploy-script@heydt.org",
    [string] $Approver     = "mike@heydt.org",      # must differ from TriggeredBy when RequiresApproval

    # --- Poll behaviour ---
    [int]    $PollSeconds  = 5,
    [int]    $TimeoutSeconds = 600,

    # Print the planned calls (method, path, JSON body) without hitting the API.
    [switch] $DryRun
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Cloud Run service resource name the adapter expects in Target.ResourceId.
$resourceId = "projects/$GcpProject/locations/$Region/services/$CloudRunService"

# ---- enum constants (wire values) ----
$ArtifactType_ContainerImage = 2
$TargetKind_GoogleCloudRun   = 5
$Status_Succeeded = 2; $Status_Failed = 3; $Status_RolledBack = 4; $Status_Cancelled = 5
$Approval_Pending = 0; $Approval_Approved = 1
$terminalStatuses = @($Status_Succeeded, $Status_Failed, $Status_RolledBack, $Status_Cancelled)

# --------------------------------------------------------------------------
# Dry-run support: emit the planned call and synthesize deterministic ids so the
# downstream steps can still print a coherent plan without touching the API.
# --------------------------------------------------------------------------
$script:planSeq = 0
function New-PlaceholderId {
    $script:planSeq++
    # e.g. 00000000-0000-0000-0000-000000000001 — obviously not a real id.
    return ("00000000-0000-0000-0000-{0:D12}" -f $script:planSeq)
}
function Write-Plan {
    param([string]$Method, [string]$Path, $Body)
    Write-Host "    [dry-run] $Method $BaseUrl$Path" -ForegroundColor DarkYellow
    if ($null -ne $Body) {
        Write-Host ("               " + ($Body | ConvertTo-Json -Depth 8 -Compress)) -ForegroundColor DarkGray
    }
}

# --------------------------------------------------------------------------
# HTTP helpers
# --------------------------------------------------------------------------
function Invoke-Api {
    param([string]$Method, [string]$Path, $Body)
    if ($DryRun -and $Method -ne 'GET') {
        Write-Plan -Method $Method -Path $Path -Body $Body
        # Return a synthetic response shape the callers understand.
        return [pscustomobject]@{ StatusCode = 0; Content = $null; Headers = @{} }
    }
    $uri = "$BaseUrl$Path"
    $args = @{ Method = $Method; Uri = $uri; SkipHttpErrorCheck = $true }
    if ($null -ne $Body) {
        $args.Body = ($Body | ConvertTo-Json -Depth 8 -Compress)
        $args.ContentType = 'application/json'
    }
    $resp = Invoke-WebRequest @args
    if ($resp.StatusCode -ge 400) {
        throw "API $Method $Path -> HTTP $($resp.StatusCode): $($resp.Content)"
    }
    return $resp
}

# POST that returns a created resource id. Prefers the trailing GUID of the
# Location header (every Results.Created sets one); falls back to the body's `id`.
function New-Resource {
    param([string]$Path, $Body)
    if ($DryRun) {
        Write-Plan -Method POST -Path $Path -Body $Body
        return New-PlaceholderId
    }
    $resp = Invoke-Api -Method POST -Path $Path -Body $Body
    $loc = $resp.Headers.Location
    if ($loc) {
        $segment = ([string]$loc).TrimEnd('/').Split('/')[-1]
        $g = [guid]::Empty
        if ([guid]::TryParse($segment, [ref]$g)) { return $g.Guid }
    }
    if ($resp.Content) {
        $obj = $resp.Content | ConvertFrom-Json
        if ($obj.PSObject.Properties.Name -contains 'id') { return [string]$obj.id }
    }
    throw "Could not extract created id from POST $Path (Location='$loc')."
}

# In dry-run, reuse-lookups return nothing so every step plans a create.
function Get-Api {
    param([string]$Path)
    if ($DryRun) { return $null }
    (Invoke-Api -Method GET -Path $Path).Content | ConvertFrom-Json
}

function Step { param([string]$Message) Write-Host "==> $Message" -ForegroundColor Cyan }

# --------------------------------------------------------------------------
# 1. Service (deployable unit) — reuse by name
# --------------------------------------------------------------------------
Step "Service '$ServiceName'"
$services  = Get-Api "/api/deployment/services"
$service   = if ($services) { $services | Where-Object { $_.name -eq $ServiceName } | Select-Object -First 1 } else { $null }
if ($service) {
    $serviceId = $service.id
    Write-Host "    reusing existing service $serviceId"
} else {
    $serviceId = New-Resource "/api/deployment/services" @{
        name = $ServiceName; kind = $ServiceKind
        repositoryUrl = $RepositoryUrl; targetFramework = $TargetFramework
    }
    Write-Host "    registered service $serviceId"
}

# --------------------------------------------------------------------------
# 2. Environment — reuse by name
# --------------------------------------------------------------------------
Step "Environment '$EnvironmentName'"
$environments = Get-Api "/api/deployment/environments"
$environment  = if ($environments) { $environments | Where-Object { $_.name -eq $EnvironmentName } | Select-Object -First 1 } else { $null }
if ($environment) {
    $environmentId = $environment.id
    Write-Host "    reusing existing environment $environmentId"
} else {
    $environmentId = New-Resource "/api/deployment/environments" @{
        name = $EnvironmentName; promotionRank = $PromotionRank
        requiresApproval = [bool]$RequiresApproval; isProduction = [bool]$IsProduction
    }
    Write-Host "    registered environment $environmentId"
}

# --------------------------------------------------------------------------
# 3. Cloud Run target — reuse by resourceId
# --------------------------------------------------------------------------
Step "Cloud Run target '$resourceId'"
$envDetail = Get-Api "/api/deployment/environments/$environmentId"
$target = if ($envDetail) { $envDetail.targets | Where-Object { $_.resourceId -eq $resourceId } | Select-Object -First 1 } else { $null }
if ($target) {
    $targetId = $target.id
    Write-Host "    reusing existing target $targetId"
} else {
    $targetId = New-Resource "/api/deployment/environments/$environmentId/targets" @{
        targetKind = $TargetKind_GoogleCloudRun; resourceId = $resourceId
        region = $Region; slot = $null
    }
    Write-Host "    added target $targetId"
}

# --------------------------------------------------------------------------
# 4. Release (container image) — reuse by version
# --------------------------------------------------------------------------
Step "Release $ServiceName v$SemanticVersion"
$releases = Get-Api "/api/deployment/releases?deployableUnitId=$serviceId"
$release  = if ($releases) { $releases | Where-Object { $_.semanticVersion -eq $SemanticVersion } | Select-Object -First 1 } else { $null }
if ($release) {
    $releaseId = $release.id
    Write-Host "    reusing existing release $releaseId"
} else {
    $releaseId = New-Resource "/api/deployment/releases" @{
        deployableUnitId = $serviceId; semanticVersion = $SemanticVersion
        buildNumber = $BuildNumber; commitSha = $CommitSha
        artifactType = $ArtifactType_ContainerImage; artifactUri = $Image
    }
    Write-Host "    published release $releaseId  ($Image)"
}

# --------------------------------------------------------------------------
# 5. Start the deployment
# --------------------------------------------------------------------------
Step "Starting deployment"
$startResp = Invoke-Api -Method POST -Path "/api/deployment/deployments" -Body @{
    releaseId            = $releaseId
    environmentId        = $environmentId
    targetIds            = @($targetId)
    strategy             = $Strategy
    trigger              = $Trigger
    triggeredByPrincipal = $TriggeredBy
    skipPromotionPathReason = $null
    overrideFreezeReason    = $null
}
if ($DryRun) {
    $started = [pscustomobject]@{ parentDeploymentId = (New-PlaceholderId); childDeploymentIds = @(New-PlaceholderId) }
} else {
    $started = $startResp.Content | ConvertFrom-Json
}

$parentId = $started.parentDeploymentId
$childIds = @($started.childDeploymentIds)
Write-Host "    parent  : $parentId"
Write-Host "    children: $($childIds -join ', ')"
$trackId = if ($parentId) { $parentId } else { $childIds[0] }

# --------------------------------------------------------------------------
# 6. Approve if the environment gates on it (4-eyes: approver != triggerer)
# --------------------------------------------------------------------------
if ($RequiresApproval) {
    Step "Approval required — looking for a pending approval"
    if ($Approver -eq $TriggeredBy) {
        throw "RequiresApproval set but -Approver ('$Approver') equals -TriggeredBy. 4-eyes rule rejects self-approval."
    }
    $detail   = Get-Api "/api/deployment/deployments/$trackId"
    $approvals = if ($detail) { @($detail.approvals) } else { @() }
    $pending   = $approvals | Where-Object { $_.status -eq $Approval_Pending } | Select-Object -First 1
    if ($DryRun) {
        Write-Plan -Method POST -Path "/api/deployment/deployments/$trackId/approve" -Body @{
            approvalId = '<pending-approval-id>'; approverPrincipal = $Approver
            verdict = $Approval_Approved; comment = "Approved via Deploy-CloudRun.ps1"
        }
    } elseif ($pending) {
        Invoke-Api -Method POST -Path "/api/deployment/deployments/$trackId/approve" -Body @{
            approvalId       = $pending.id
            approverPrincipal = $Approver
            verdict          = $Approval_Approved
            comment          = "Approved via Deploy-CloudRun.ps1"
        } | Out-Null
        Write-Host "    approved by $Approver"
    } else {
        Write-Host "    no pending approval row found (env may not have generated one); continuing"
    }
}

# --------------------------------------------------------------------------
# 7. Poll to a terminal state
# --------------------------------------------------------------------------
if ($DryRun) {
    Step "Polling deployment $trackId (timeout ${TimeoutSeconds}s)"
    Write-Host "    [dry-run] GET $BaseUrl/api/deployment/deployments/$trackId (every ${PollSeconds}s until terminal)" -ForegroundColor DarkYellow
    Step "Dry run complete — no API calls were made."
    exit 0
}

Step "Polling deployment $trackId (timeout ${TimeoutSeconds}s)"
$deadline = [datetime]::UtcNow.AddSeconds($TimeoutSeconds)
$statusNames = @{ 0='Queued';1='Running';2='Succeeded';3='Failed';4='RolledBack';5='Cancelled';6='HealthChecking' }
while ($true) {
    $d = Get-Api "/api/deployment/deployments/$trackId"
    $status = [int]$d.head.status
    Write-Host ("    [{0:HH:mm:ss}] {1}" -f [datetime]::Now, $statusNames[$status])
    if ($terminalStatuses -contains $status) {
        if ($status -eq $Status_Succeeded) {
            Step "Deployment SUCCEEDED"
            exit 0
        }
        $reason = if ($d.failureReason) { $d.failureReason } elseif ($d.cancellationReason) { $d.cancellationReason } else { "(no reason recorded)" }
        Step "Deployment ended as $($statusNames[$status]): $reason"
        exit 1
    }
    if ([datetime]::UtcNow -gt $deadline) {
        throw "Timed out after ${TimeoutSeconds}s; last status was $($statusNames[$status])."
    }
    Start-Sleep -Seconds $PollSeconds
}
