$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
try {
    $targetDirectory = Join-Path $env:LOCALAPPDATA 'Programs\MemoPrise'
    New-Item -ItemType Directory -Path $targetDirectory -Force | Out-Null
    $appPath = Join-Path $targetDirectory 'MemoPrise.exe'
    $sourceApp = Join-Path $PSScriptRoot 'MemoPrise.exe'
    if (-not (Test-Path -LiteralPath $sourceApp)) { $sourceApp = Join-Path $PSScriptRoot 'MemoPrise\MemoPrise.exe' }
    Copy-Item -LiteralPath $sourceApp -Destination $appPath -Force
    $shortcutShell = New-Object -ComObject WScript.Shell
    foreach ($shortcutPath in @((Join-Path ([Environment]::GetFolderPath('Desktop')) 'MémoPrise.lnk'), (Join-Path ([Environment]::GetFolderPath('StartMenu')) 'Programs\MémoPrise.lnk'))) {
        $shortcut = $shortcutShell.CreateShortcut($shortcutPath)
        $shortcut.TargetPath = $appPath
        $shortcut.WorkingDirectory = $targetDirectory
        $shortcut.IconLocation = $appPath + ',0'
        $shortcut.Description = 'Rappels de médicaments et suivi des prises'
        $shortcut.Save()
    }
    $runPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    New-Item -Path $runPath -Force | Out-Null
    Set-ItemProperty -Path $runPath -Name MemoPrise -Value ('"' + $appPath + '" --background')
    [System.Windows.Forms.MessageBox]::Show('MémoPrise est installé. Un raccourci est disponible sur le Bureau. Le démarrage avec Windows est activé et peut être désactivé dans les paramètres.', 'Installation terminée') | Out-Null
    Start-Process -FilePath $appPath -WindowStyle Hidden
} catch {
    [System.Windows.Forms.MessageBox]::Show(('Installation impossible : ' + $_.Exception.Message + "`r`nSi MémoPrise est déjà ouvert, quittez-le avant de réinstaller."), 'MémoPrise') | Out-Null
    exit 1
}

