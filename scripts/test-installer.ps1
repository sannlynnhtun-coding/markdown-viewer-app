$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$installer = Join-Path $root 'artifacts/installer/MarkdownViewer.Setup.exe'
$installDirectory = Join-Path $root 'artifacts/installer/test-install'
$registration = 'HKCU:/Software/Classes/MarkdownViewer.Document'
if (Test-Path $registration) { throw 'Run this check without an existing EXE installation.' }

try {
    foreach ($attempt in 1..2) {
        $setup = Start-Process $installer -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', "/DIR=`"$installDirectory`"") -Wait -PassThru -WindowStyle Hidden
        if ($setup.ExitCode -ne 0) { throw "Install attempt $attempt failed: $($setup.ExitCode)" }
        foreach ($file in 'MarkdownViewerApp.exe', 'MarkdownViewerApp.pri', 'Assets/AppIcon.ico', 'coreclr.dll', 'Microsoft.ui.xaml.dll', 'unins000.exe') {
            if (!(Test-Path (Join-Path $installDirectory $file))) { throw "Missing installed file: $file" }
        }
        $command = (Get-Item "$registration/shell/open/command").GetValue('')
        if ($command -ne "`"$installDirectory\MarkdownViewerApp.exe`" `"%1`"") { throw 'Incorrect Open with command.' }
        foreach ($extension in '.md', '.markdown') {
            $key = Get-Item "HKCU:/Software/Classes/$extension/OpenWithProgids"
            if ($key.GetValueNames() -notcontains 'MarkdownViewer.Document') { throw "Missing association: $extension" }
        }
    }
} finally {
    $uninstaller = Join-Path $installDirectory 'unins000.exe'
    if (Test-Path $uninstaller) {
        $uninstall = Start-Process $uninstaller -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait -PassThru -WindowStyle Hidden
        if ($uninstall.ExitCode -ne 0) { throw "Uninstall failed: $($uninstall.ExitCode)" }
    }
}
if (Test-Path $registration) { throw 'Uninstall left the file registration behind.' }
if (Test-Path (Join-Path $installDirectory 'MarkdownViewerApp.exe')) { throw 'Uninstall left the app behind.' }
Write-Output 'Install, repeat install, payload, file associations and uninstall checks passed.'
exit 0
