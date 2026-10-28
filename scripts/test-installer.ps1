$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$installer = Join-Path $root 'artifacts/installer/MarkdownViewer.Setup.exe'
$installDirectory = Join-Path $root 'artifacts/installer/test-install'
$registration = 'HKCU:/Software/Classes/MarkdownViewer.Document'
if (Test-Path $registration) { throw 'Run this check without an existing EXE installation.' }
$app = $null

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

    $runtimeConfig = Get-Content (Join-Path $installDirectory 'MarkdownViewerApp.runtimeconfig.json') -Raw | ConvertFrom-Json
    if (!$runtimeConfig.runtimeOptions.includedFrameworks -or $runtimeConfig.runtimeOptions.framework -or $runtimeConfig.runtimeOptions.frameworks) {
        throw 'The installed app is not configured as self-contained.'
    }

    $start = [System.Diagnostics.ProcessStartInfo]::new((Join-Path $installDirectory 'MarkdownViewerApp.exe'))
    $start.UseShellExecute = $false
    $start.WorkingDirectory = $installDirectory
    $start.EnvironmentVariables['PATH'] = (($env:PATH -split ';' | Where-Object { $_ -notmatch '(?i)dotnet' }) -join ';')
    foreach ($name in 'DOTNET_ROOT', 'DOTNET_ROOT_X64', 'DOTNET_ROOT_X86', 'DOTNET_ROOT(x86)') {
        $start.EnvironmentVariables[$name] = Join-Path $installDirectory 'no-system-dotnet'
    }
    $start.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
    $app = [System.Diagnostics.Process]::Start($start)
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        Start-Sleep -Milliseconds 500
        $app.Refresh()
        if ($app.HasExited) { throw "Installed app exited during startup: $($app.ExitCode)" }
        if ($app.MainWindowHandle -ne 0) { break }
    }
    if ($app.MainWindowHandle -eq 0) { throw 'The installed app did not open a window.' }

    $modules = @($app.Modules)
    foreach ($name in 'coreclr.dll', 'hostfxr.dll', 'hostpolicy.dll', 'Microsoft.ui.xaml.dll') {
        $module = $modules | Where-Object ModuleName -eq $name
        if (!$module -or $module.FileName -ne (Join-Path $installDirectory $name)) {
            throw "The installed app did not load its bundled $name"
        }
        Write-Output "Verified bundled runtime module: $($module.FileName)"
    }
} finally {
    if ($null -ne $app) {
        if (!$app.HasExited) {
            $app.Kill()
            $app.WaitForExit()
        }
        $app.Dispose()
    }
    $uninstaller = Join-Path $installDirectory 'unins000.exe'
    if (Test-Path $uninstaller) {
        $uninstall = Start-Process $uninstaller -ArgumentList '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART' -Wait -PassThru -WindowStyle Hidden
        if ($uninstall.ExitCode -ne 0) { throw "Uninstall failed: $($uninstall.ExitCode)" }
    }
}
if (Test-Path $registration) { throw 'Uninstall left the file registration behind.' }
if (Test-Path (Join-Path $installDirectory 'MarkdownViewerApp.exe')) { throw 'Uninstall left the app behind.' }
Write-Output 'Install, repeat install, payload, file associations, bundled runtime launch and uninstall checks passed.'
exit 0
