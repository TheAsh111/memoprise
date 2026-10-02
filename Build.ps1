param([switch]$SkipInstaller)
$ErrorActionPreference = 'Stop'
$workspaceDirectory = $PSScriptRoot
Push-Location $workspaceDirectory
try {
    dotnet run --project tests/MemoPrise.Tests.csproj -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Tests échoués.' }
    dotnet publish MemoPrise/MemoPrise.csproj -c Release -r win-x64 --self-contained true -o distribution/MemoPrise -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded
    if ($LASTEXITCODE -ne 0) { throw 'Compilation échouée.' }
    if (-not $SkipInstaller) {
        $appDirectory = Join-Path $workspaceDirectory 'distribution\MemoPrise'
        $distributionDirectory = Join-Path $workspaceDirectory 'distribution'
        $sedPath = Join-Path $workspaceDirectory 'distribution\installer.sed'
        $sed = [IO.File]::ReadAllText($sedPath)
        $sed = [Regex]::Replace($sed, '(?m)^TargetName=.*$', ('TargetName=' + (Join-Path $workspaceDirectory 'distribution\Installer-MemoPrise.exe')))
        $sed = [Regex]::Replace($sed, '(?m)^SourceFiles0=.*$', ('SourceFiles0=' + $appDirectory + '\'))
        $sed = [Regex]::Replace($sed, '(?m)^SourceFiles1=.*$', ('SourceFiles1=' + $distributionDirectory + '\'))
        $sed = $sed.Replace("`r`n", "`n").Replace("`n", "`r`n")
        [IO.File]::WriteAllText($sedPath, $sed, [Text.Encoding]::ASCII)
        $packagingProcess = Start-Process -FilePath "$env:SystemRoot\System32\iexpress.exe" -ArgumentList '/N','/Q',$sedPath -WindowStyle Hidden -Wait -PassThru
        if ($packagingProcess.ExitCode -ne 0) { throw ('Construction installateur impossible : ' + $packagingProcess.ExitCode) }
        if (-not (Test-Path 'distribution\Installer-MemoPrise.exe')) { throw 'Installateur non créé.' }
    }
} finally { Pop-Location }
