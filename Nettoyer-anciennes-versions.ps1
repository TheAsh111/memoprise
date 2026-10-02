[CmdletBinding(SupportsShouldProcess)]
param()
$ErrorActionPreference = 'Stop'
$workspacePath = [IO.Path]::GetFullPath($PSScriptRoot)
$distributionPath = Join-Path $workspacePath 'distribution'
$mainPath = Join-Path $distributionPath 'MemoPrise\MemoPrise.exe'
if (-not (Test-Path -LiteralPath $mainPath)) { throw 'Application principale introuvable.' }
$distribution = Get-Item -LiteralPath $distributionPath
if ($distribution.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Le dossier distribution ne doit pas être un lien.' }
$obsolete = @(Get-ChildItem -LiteralPath $distributionPath | Where-Object {
    ($_.PSIsContainer -and $_.Name -match '^(MemoPrise-[0-9]+(\.[0-9]+)*|installer-payload(-[0-9]+(\.[0-9]+)*)?)$') -or
    (-not $_.PSIsContainer -and $_.Name -match '^Installer-MemoPrise-[0-9]+(\.[0-9]+)*\.exe$')
})
# Validate every absolute target before deleting any of them.
foreach ($item in $obsolete) {
    $resolved = (Resolve-Path -LiteralPath $item.FullName).Path
    if ((Split-Path $resolved -Parent) -ne $distribution.FullName -or $item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Ancien exemplaire hors du dossier attendu.' }
    if ($item.PSIsContainer -and @(Get-ChildItem -LiteralPath $resolved -Recurse -Force | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count -gt 0) { throw 'Lien détecté dans un ancien exemplaire.' }
}
foreach ($process in @(Get-Process MemoPrise -ErrorAction SilentlyContinue)) {
    foreach ($item in $obsolete | Where-Object PSIsContainer) {
        if ($process.Path -and $process.Path.StartsWith($item.FullName+'\',[StringComparison]::OrdinalIgnoreCase)) { throw "Quittez l’ancienne application avant le nettoyage." }
    }
}
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$startup = Get-ItemPropertyValue -Path $runKey -Name MemoPrise -ErrorAction SilentlyContinue
if ($startup) {
    foreach ($item in $obsolete | Where-Object PSIsContainer) {
        if ($startup.Contains($item.FullName+'\MemoPrise.exe') -and $PSCmdlet.ShouldProcess('Démarrage de MémoPrise','Utiliser le dossier principal')) {
            Set-ItemProperty -Path $runKey -Name MemoPrise -Value ('"'+$mainPath+'" --background')
            break
        }
    }
}
$removedBytes = 0L
foreach ($item in $obsolete) {
    if ($PSCmdlet.ShouldProcess($item.FullName,'Supprimer cet ancien exemplaire')) {
        if ($item.PSIsContainer) {
            $removedBytes += (Get-ChildItem -LiteralPath $item.FullName -File -Recurse | Measure-Object Length -Sum).Sum
            Remove-Item -LiteralPath $item.FullName -Recurse -Force
        } else {
            $removedBytes += $item.Length
            Remove-Item -LiteralPath $item.FullName -Force
        }
    }
}
$mainDirectory = Join-Path $distributionPath 'MemoPrise'
if ((Get-Item -LiteralPath $mainDirectory).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Le dossier principal ne doit pas être un lien.' }
foreach ($file in Get-ChildItem -LiteralPath $mainDirectory -File | Where-Object {$_.Name -match '^(preview-.*\.png|ui-(verification|error)\.txt|MemoPrise\.pdb)$'}) {
    if ($PSCmdlet.ShouldProcess($file.FullName,'Supprimer ce fichier de vérification généré')) { Remove-Item -LiteralPath $file.FullName -Force }
}
$packageCheck = Join-Path $workspacePath 'tests\package-check'
if (Test-Path -LiteralPath $packageCheck) {
    $checkItem = Get-Item -LiteralPath $packageCheck
    if ($checkItem.Attributes -band [IO.FileAttributes]::ReparsePoint -or @(Get-ChildItem -LiteralPath $packageCheck -Recurse -Force | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count -gt 0) { throw 'Dossier de vérification inattendu.' }
    if ($PSCmdlet.ShouldProcess($checkItem.FullName,"Supprimer la copie temporaire de vérification de l’installateur")) { Remove-Item -LiteralPath $checkItem.FullName -Recurse -Force }
}
Write-Host ('Nettoyage terminé. Espace libéré : {0:N2} Go.' -f ($removedBytes/1GB))
Write-Host 'Application conservée dans distribution\MemoPrise. Traitements et historique conservés.'
