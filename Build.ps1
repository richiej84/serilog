param(
    [String] $majorMinor = "0.0",  # 1.4
    [String] $patch = "0",         # $env:APPVEYOR_BUILD_VERSION
    [String] $customLogger = "",   # C:\Program Files\AppVeyor\BuildAgent\Appveyor.MSBuildLogger.dll
    [Switch] $notouch
)

function Set-AssemblyVersions($informational, $assembly)
{
    (Get-Content assets/CommonAssemblyInfo.cs) |
        ForEach-Object { $_ -replace """1.0.0.0""", """$assembly""" } |
        ForEach-Object { $_ -replace """1.0.0""", """$informational""" } |
        Set-Content assets/CommonAssemblyInfo.cs
}

function Install-NuGetPackages()
{
    nuget restore Serilog.sln
}

function Invoke-MSBuild($solution, $customLogger)
{
    msbuild "$solution" /verbosity:minimal /p:Configuration=Release /logger:"$customLogger"
}

function Invoke-Build($majorMinor, $patch, $customLogger, $notouch)
{
    $package="$majorMinor.$patch"

    Write-Output "Building Serilog $package"

    if (-not $notouch)
    {
        $assembly = "$majorMinor.0.0"

        Write-Output "Assembly version will be set to $assembly"
        Set-AssemblyVersions $package $assembly
    }

    Install-NuGetPackages
    
    Invoke-MSBuild "Serilog-net40.sln" $customLogger
    Invoke-MSBuild "Serilog.sln" $customLogger
}


$ErrorActionPreference = "Stop"
Invoke-Build $majorMinor $patch $customLogger $notouch