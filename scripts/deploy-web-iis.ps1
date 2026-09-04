<#
------------------------------------------------------------------------------
 File        : deploy-web-iis.ps1
 Project     : VoltShare - Smart Solar Microgrid Trading System
 Description : Builds the React back-office application and hosts it on IIS
               beside the Web API. Once this has run, the whole system is
               served by IIS and nothing has to be started by hand: IIS is a
               Windows service and starts with the machine.
 Author      : <IT Number - Member Name>
 Created     : 2026-09-03

 MUST BE RUN FROM AN ELEVATED POWERSHELL WINDOW ("Run as administrator").
------------------------------------------------------------------------------
#>

[CmdletBinding()]
param(
    # Name of the IIS website and application pool for the web client.
    [string]$SiteName   = "VoltShareWeb",

    # Port the site is served on. The API uses 8080.
    [int]$Port          = 8081,

    # Folder IIS serves the compiled site from.
    [string]$TargetPath = "C:\inetpub\VoltShareWeb",

    # Skip "npm run build" and deploy whatever is already in web\dist.
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$RepoRoot    = Split-Path -Parent $PSScriptRoot
$WebRoot     = Join-Path $RepoRoot "web"
$DistPath    = Join-Path $WebRoot "dist"
$AppPoolName = $SiteName

function Write-Step { param([string]$Message) Write-Host "`n==> $Message" -ForegroundColor Cyan }
function Write-Ok   { param([string]$Message) Write-Host "    $Message" -ForegroundColor Green }
function Write-Warn { param([string]$Message) Write-Host "    $Message" -ForegroundColor Yellow }

# -----------------------------------------------------------------------------
# 1. Prerequisites.
# -----------------------------------------------------------------------------
Write-Step "Checking prerequisites"

$identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "This script must be run from an elevated PowerShell window (Run as administrator)."
}
Write-Ok "Running elevated."

Import-Module WebAdministration -ErrorAction Stop
Write-Ok "IIS WebAdministration module loaded."

# -----------------------------------------------------------------------------
# 2. Build the site.
# -----------------------------------------------------------------------------
if (-not $SkipBuild) {
    Write-Step "Building the React application"

    Push-Location $WebRoot
    try {
        # The build reads .env.production, which points at the API address the
        # deployed site should call.
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "npm run build failed with exit code $LASTEXITCODE." }
    } finally {
        Pop-Location
    }

    Write-Ok "Built to $DistPath."
} else {
    Write-Step "Skipping build, using existing output"
}

if (-not (Test-Path (Join-Path $DistPath "index.html"))) {
    throw "No build output found at $DistPath. Run without -SkipBuild."
}

# The SPA fallback rules live in public\web.config and are copied by the build.
if (-not (Test-Path (Join-Path $DistPath "web.config"))) {
    Write-Warn "web.config is missing from the build output; deep links may return 404."
}

# -----------------------------------------------------------------------------
# 3. Application pool.
# -----------------------------------------------------------------------------
Write-Step "Configuring the application pool"

if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName | Out-Null
    Write-Ok "Created application pool '$AppPoolName'."
} else {
    Write-Ok "Application pool '$AppPoolName' already exists."
}

# This site is static files only, so no managed runtime is needed at all.
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
Write-Ok "Set managed runtime to 'No Managed Code'."

# -----------------------------------------------------------------------------
# 4. Copy the compiled site.
# -----------------------------------------------------------------------------
$siteExists = Test-Path "IIS:\Sites\$SiteName"
if ($siteExists) {
    Write-Step "Stopping the running site before copying files"
    try { Stop-Website -Name $SiteName } catch { Write-Warn "Site was not running." }
    Start-Sleep -Seconds 1
    Write-Ok "Stopped."
}

Write-Step "Copying the built site to $TargetPath"

if (-not (Test-Path $TargetPath)) {
    New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
}

# /MIR mirrors the folder so assets from a previous build are removed.
robocopy $DistPath $TargetPath /MIR /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE." }
Write-Ok "Files copied."

# -----------------------------------------------------------------------------
# 5. Permissions.
# -----------------------------------------------------------------------------
Write-Step "Setting folder permissions"

$poolIdentity = "IIS AppPool\$AppPoolName"
icacls $TargetPath /grant "${poolIdentity}:(OI)(CI)(RX)" /T /C /Q | Out-Null
Write-Ok "Granted read and execute to the application pool identity."

# -----------------------------------------------------------------------------
# 6. Website.
# -----------------------------------------------------------------------------
Write-Step "Configuring the website"

if (-not $siteExists) {
    New-Website -Name $SiteName -Port $Port -PhysicalPath $TargetPath `
                -ApplicationPool $AppPoolName | Out-Null
    Write-Ok "Created site '$SiteName' on port $Port."
} else {
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name physicalPath -Value $TargetPath
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name applicationPool -Value $AppPoolName
    Write-Ok "Updated existing site '$SiteName'."
}

# -----------------------------------------------------------------------------
# 7. Firewall.
# -----------------------------------------------------------------------------
Write-Step "Opening the firewall port"

$ruleName = "VoltShare Web (TCP $Port)"
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP `
                        -LocalPort $Port -Action Allow -Profile Private | Out-Null
    Write-Ok "Created inbound rule for TCP $Port on private networks."
} else {
    Write-Ok "Firewall rule already present."
}

# -----------------------------------------------------------------------------
# 8. Start and verify.
# -----------------------------------------------------------------------------
Write-Step "Starting the site"

Start-WebAppPool -Name $AppPoolName
Start-Website -Name $SiteName
Write-Ok "Started."

Write-Step "Verifying the deployment"

$url = "http://localhost:$Port/"
$ok = $false

foreach ($attempt in 1..8) {
    try {
        $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 10
        if ($response.StatusCode -eq 200) {
            Write-Ok "Site responded with HTTP 200."
            $ok = $true
            break
        }
    } catch {
        Start-Sleep -Seconds 2
    }
}

if (-not $ok) { throw "Deployment completed but the site did not respond at $url." }

$lanIp = (Get-NetIPAddress -AddressFamily IPv4 |
          Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" } |
          Select-Object -First 1 -ExpandProperty IPAddress)

Write-Host "`n=============================================================" -ForegroundColor Green
Write-Host " VoltShare Web deployed successfully" -ForegroundColor Green
Write-Host "=============================================================" -ForegroundColor Green
Write-Host " Web application : http://localhost:$Port"
Write-Host " On the network  : http://${lanIp}:$Port"
Write-Host " API it calls    : see web\.env.production"
Write-Host " Files           : $TargetPath"
Write-Host ""
Write-Host " IIS runs as a Windows service, so both sites now start"
Write-Host " automatically with the machine. Nothing to launch by hand."
Write-Host "=============================================================`n" -ForegroundColor Green
