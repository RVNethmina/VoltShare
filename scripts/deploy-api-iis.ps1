<#
------------------------------------------------------------------------------
 File        : deploy-api-iis.ps1
 Project     : VoltShare - Smart Solar Microgrid Trading System
 Description : Publishes the VoltShare Web API and hosts it on IIS. Creates the
               application pool and the website on first run, and simply
               refreshes the files on later runs, so the same script serves as
               both the initial deployment and the redeploy command.
 Author      : <IT Number - Member Name>
 Created     : 2026-09-03

 MUST BE RUN FROM AN ELEVATED POWERSHELL WINDOW ("Run as administrator"),
 because creating IIS sites and setting folder permissions require it.
------------------------------------------------------------------------------
#>

[CmdletBinding()]
param(
    # Name of the IIS website and application pool.
    [string]$SiteName    = "VoltShareApi",

    # Port the API is served on. 8080 avoids clashing with the IIS default site.
    [int]$Port           = 8080,

    # Folder IIS serves the application from.
    [string]$TargetPath  = "C:\inetpub\VoltShareApi",

    # Skip "dotnet publish" and deploy whatever is already in publish\api.
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

# Resolve the repository root from this script's own location, so the script
# works no matter which directory it is invoked from.
$RepoRoot    = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $RepoRoot "backend\src\VoltShare.Api\VoltShare.Api.csproj"
$PublishPath = Join-Path $RepoRoot "publish\api"
$AppPoolName = $SiteName

function Write-Step { param([string]$Message) Write-Host "`n==> $Message" -ForegroundColor Cyan }
function Write-Ok   { param([string]$Message) Write-Host "    $Message" -ForegroundColor Green }
function Write-Warn { param([string]$Message) Write-Host "    $Message" -ForegroundColor Yellow }

# -----------------------------------------------------------------------------
# 1. Confirm the script can actually do its job before changing anything.
# -----------------------------------------------------------------------------
Write-Step "Checking prerequisites"

$identity  = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw "This script must be run from an elevated PowerShell window (Run as administrator)."
}
Write-Ok "Running elevated."

# Without the ASP.NET Core Hosting Bundle, IIS has no module that can execute a
# .NET application and every request returns HTTP 500.19.
$ancm = "C:\Program Files\IIS\Asp.Net Core Module\V2\aspnetcorev2.dll"
if (-not (Test-Path $ancm)) {
    throw ("The ASP.NET Core Hosting Bundle is not installed, so IIS cannot host this " +
           "application. Install it from https://dotnet.microsoft.com/download/dotnet/10.0 " +
           "(the 'Hosting Bundle' link under Run server apps), then run 'iisreset' and try again.")
}
Write-Ok ("ASP.NET Core Module V2 found, version " +
          (Get-Item $ancm).VersionInfo.ProductVersion + ".")

Import-Module WebAdministration -ErrorAction Stop
Write-Ok "IIS WebAdministration module loaded."

# The production settings file carries the database connection string and the
# signing keys. It is deliberately excluded from Git, so a fresh clone will not
# have it and the deployment would start but fail to reach MongoDB.
$prodSettings = Join-Path $RepoRoot "backend\src\VoltShare.Api\appsettings.Production.json"
if (-not (Test-Path $prodSettings)) {
    throw ("appsettings.Production.json is missing. It holds the MongoDB connection " +
           "string and signing keys and is not committed to Git. Create it before deploying.")
}
Write-Ok "Production settings file present."

# -----------------------------------------------------------------------------
# 2. Build the deployable output.
# -----------------------------------------------------------------------------
if (-not $SkipPublish) {
    Write-Step "Publishing the API in Release configuration"
    if (Test-Path $PublishPath) { Remove-Item $PublishPath -Recurse -Force }
    dotnet publish $ProjectPath -c Release -o $PublishPath
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
    Write-Ok "Published to $PublishPath."
} else {
    Write-Step "Skipping publish, using existing output"
    if (-not (Test-Path (Join-Path $PublishPath "VoltShare.Api.dll"))) {
        throw "No published output found at $PublishPath. Run without -SkipPublish."
    }
}

# -----------------------------------------------------------------------------
# 3. Create the application pool.
# -----------------------------------------------------------------------------
Write-Step "Configuring the application pool"

if (-not (Test-Path "IIS:\AppPools\$AppPoolName")) {
    New-WebAppPool -Name $AppPoolName | Out-Null
    Write-Ok "Created application pool '$AppPoolName'."
} else {
    Write-Ok "Application pool '$AppPoolName' already exists."
}

# "No Managed Code" is required: the .NET runtime is loaded by the ASP.NET Core
# Module itself, not by the old .NET Framework pipeline. Leaving this set to a
# CLR version is the most common cause of a failed ASP.NET Core deployment.
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name managedRuntimeVersion -Value ""
Set-ItemProperty "IIS:\AppPools\$AppPoolName" -Name startMode -Value "AlwaysRunning"
Write-Ok "Set managed runtime to 'No Managed Code'."

# -----------------------------------------------------------------------------
# 4. Stop the site before replacing files, so no DLL is locked mid copy.
# -----------------------------------------------------------------------------
$siteExists = Test-Path "IIS:\Sites\$SiteName"
if ($siteExists) {
    Write-Step "Stopping the running site before copying files"
    try { Stop-Website -Name $SiteName } catch { Write-Warn "Site was not running." }
    try { Stop-WebAppPool -Name $AppPoolName } catch { Write-Warn "Pool was not running." }

    # Give the worker process a moment to release its file handles.
    Start-Sleep -Seconds 2
    Write-Ok "Stopped."
}

# -----------------------------------------------------------------------------
# 5. Copy the published files.
# -----------------------------------------------------------------------------
Write-Step "Copying published files to $TargetPath"

if (-not (Test-Path $TargetPath)) {
    New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
}

# /MIR mirrors the folder so files removed from the build are removed from the
# deployment too. The logs folder is excluded so existing logs survive.
robocopy $PublishPath $TargetPath /MIR /NFL /NDL /NJH /NJS /NP /XD logs | Out-Null

# robocopy uses exit codes below 8 to report success with varying detail.
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE." }
Write-Ok "Files copied."

$logPath = Join-Path $TargetPath "logs"
if (-not (Test-Path $logPath)) { New-Item -ItemType Directory -Path $logPath -Force | Out-Null }

# -----------------------------------------------------------------------------
# 6. Grant the application pool identity access to the folder.
# -----------------------------------------------------------------------------
Write-Step "Setting folder permissions"

# The pool runs as the virtual account "IIS AppPool\<pool name>". It needs to
# read the application, and to write into the logs folder for stdout logging.
$poolIdentity = "IIS AppPool\$AppPoolName"
icacls $TargetPath /grant "${poolIdentity}:(OI)(CI)(RX)" /T /C /Q | Out-Null
icacls $logPath    /grant "${poolIdentity}:(OI)(CI)(M)"  /T /C /Q | Out-Null
Write-Ok "Granted read and execute on the application, and write on logs."

# -----------------------------------------------------------------------------
# 7. Create the website.
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
# 8. Allow other devices on the network to reach the API.
# -----------------------------------------------------------------------------
Write-Step "Opening the firewall port"

# Without this rule the Android device and the emulator can reach the API from
# this machine only, and every call from a phone times out.
$ruleName = "VoltShare API (TCP $Port)"
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP `
                        -LocalPort $Port -Action Allow -Profile Private | Out-Null
    Write-Ok "Created inbound rule for TCP $Port on private networks."
} else {
    Write-Ok "Firewall rule already present."
}

# -----------------------------------------------------------------------------
# 9. Start everything and prove it works.
# -----------------------------------------------------------------------------
Write-Step "Starting the site"

Start-WebAppPool -Name $AppPoolName
Start-Website -Name $SiteName
Write-Ok "Started."

Write-Step "Verifying the deployment"

$healthUrl = "http://localhost:$Port/api/v1/health"
$ok = $false

# The first request has to start the worker process and open the MongoDB
# connection, so allow several attempts before declaring failure.
foreach ($attempt in 1..10) {
    try {
        $response = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 15
        Write-Ok ("Health check passed: status=" + $response.status + ", database=" + $response.database)
        $ok = $true
        break
    } catch {
        Start-Sleep -Seconds 3
    }
}

if (-not $ok) {
    Write-Warn "Health check did not succeed."
    Write-Warn "Check the start-up log written to: $logPath"
    throw "Deployment completed but the API did not respond at $healthUrl."
}

$lanIp = (Get-NetIPAddress -AddressFamily IPv4 |
          Where-Object { $_.IPAddress -notlike "127.*" -and $_.IPAddress -notlike "169.254.*" } |
          Select-Object -First 1 -ExpandProperty IPAddress)

Write-Host "`n=============================================================" -ForegroundColor Green
Write-Host " VoltShare API deployed successfully" -ForegroundColor Green
Write-Host "=============================================================" -ForegroundColor Green
Write-Host " Swagger UI    : http://localhost:$Port/swagger"
Write-Host " Health check  : $healthUrl"
Write-Host " From a phone  : http://${lanIp}:$Port/api/v1"
Write-Host " Emulator host : http://10.0.2.2:$Port/api/v1"
Write-Host " Files         : $TargetPath"
Write-Host " Start-up logs : $logPath"
Write-Host "=============================================================`n" -ForegroundColor Green
