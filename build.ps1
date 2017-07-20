<#
.SYNOPSIS
This is a Powershell script to bootstrap a Cake build.
.DESCRIPTION
This Powershell script will download NuGet if missing, restore NuGet tools (including Cake)
and execute your Cake build script with the parameters you provide.
.PARAMETER Target
The build script target to run.
.PARAMETER Configuration
The build configuration to use.
.PARAMETER Verbosity
Specifies the amount of information to be displayed.
.PARAMETER WhatIf
Performs a dry run of the build script.
No tasks will be executed.
.PARAMETER ScriptArgs
Remaining arguments are added here.
.LINK
http://cakebuild.net
#>

[CmdletBinding()]
Param(
    [string]$Target = "Default",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("Quiet", "Minimal", "Normal", "Verbose", "Diagnostic")]
    [string]$Verbosity = "Verbose",
    [switch]$WhatIf,
    [Parameter(Position=0,Mandatory=$false,ValueFromRemainingArguments=$true)]
    [string[]]$ScriptArgs
)

$CakeVersion = "0.21.1"
$NugetUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"

# Make sure tools folder exists
$PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent
$ToolPath = Join-Path $PSScriptRoot "tools"
if (!(Test-Path $ToolPath)) {
    Write-Verbose "Creating tools directory..."
    New-Item -Path $ToolPath -Type directory | out-null
}

###########################################################################
# INSTALL NUGET
###########################################################################

$NugetPath = Join-Path $ToolPath "nuget.exe"

# Try find NuGet.exe in path if not exists
if (!(Test-Path $NugetPath)) {
    Write-Host "Trying to find nuget.exe in PATH..."
    $ExistingPaths = $Env:Path -Split ';' | Where-Object { (![string]::IsNullOrEmpty($_)) -and (Test-Path $_ -PathType Container) }
    $NugetExeInPath = Get-ChildItem -Path $ExistingPaths -Filter "nuget.exe" | Select-Object -First 1
    if ($NugetExeInPath -ne $null -and (Test-Path $NugetExeInPath.FullName)) {
        Write-Verbose -Message "Found in PATH at $($NugetExeInPath.FullName)."
        $NugetPath = $NugetExeInPath.FullName
    }
}

# Try download NuGet.exe if not exists
if (!(Test-Path $NugetPath)) {
    Write-Host "Downloading NuGet.exe..."
    (New-Object System.Net.WebClient).DownloadFile($NugetUrl, $NugetPath);
}

# Save nuget.exe path to environment to be available to child processed
$env:NUGET_EXE = $NugetPath

###########################################################################
# INSTALL CAKE
###########################################################################

# Make sure Cake has been installed.
$CakePath = Join-Path $ToolPath "Cake.$CakeVersion/Cake.exe"
if (!(Test-Path $CakePath)) {
    Write-Host "Installing Cake..."
    Invoke-Expression "&`"$NugetPath`" install Cake -Version $CakeVersion -OutputDirectory `"$ToolPath`"" | Out-Null;
    if ($LASTEXITCODE -ne 0) {
        Throw "An error occured while restoring Cake from NuGet."
    }
}

###########################################################################
# RUN BUILD SCRIPT
###########################################################################

# Build the argument list.
$Arguments = @{
    target=$Target;
    configuration=$Configuration;
    verbosity=$Verbosity;
    dryrun=$WhatIf;
}.GetEnumerator() | % {"--{0}=`"{1}`"" -f $_.key, $_.value };

# Start Cake
Write-Host "Running build script..."
Invoke-Expression "& `"$CakePath`" `"build.cake`" $Arguments $ScriptArgs"
exit $LASTEXITCODE
