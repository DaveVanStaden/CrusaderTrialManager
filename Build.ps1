param(
    [string]$GamePath,
    [string]$LoaderPath,
    [switch]$PluginOnly,
    [string]$ManagerFile = 'CrusaderTrialManager-1.0.exe'
)
$ErrorActionPreference='Stop'
if(!$GamePath){
 $setting=Join-Path $PSScriptRoot 'game-folder.txt'
 if(Test-Path -LiteralPath $setting){$GamePath=(Get-Content -LiteralPath $setting -Raw).Trim()}
 else{throw 'Provide -GamePath pointing to your game installation.'}
}
if(!$LoaderPath){$LoaderPath=$GamePath}
$csc='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$managed=Join-Path $GamePath 'Stronghold Crusader Definitive Edition_Data\Managed'
$core=Join-Path $LoaderPath 'BepInEx\core'
$src=Join-Path $PSScriptRoot 'source'
if(!$PluginOnly){
 & $csc /nologo /target:winexe /platform:x64 "/win32icon:$PSScriptRoot\app.ico" "/out:$PSScriptRoot\$ManagerFile" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Core.dll /reference:System.Xml.dll "$src\Profile.cs" "$src\Catalog.cs" "$src\Manager.cs" "$src\Theme.cs" "$src\AssemblyInfo.cs"
 if($LASTEXITCODE -ne 0){throw 'Manager build failed'}
}
& $csc /nologo /target:library /platform:x64 "/out:$PSScriptRoot\CrusaderTrailPlugin.dll" "/reference:$core\BepInEx.dll" "/reference:$core\0Harmony.dll" "/reference:$managed\Assembly-CSharp.dll" "/reference:$managed\Noesis.NoesisGUI.dll" "/reference:$managed\UnityEngine.dll" "/reference:$managed\UnityEngine.CoreModule.dll" "/reference:$managed\UnityEngine.JSONSerializeModule.dll" /reference:System.Core.dll /reference:System.Xml.dll "$src\Profile.cs" "$src\Catalog.cs" "$src\ScopedIntPatch.cs" "$src\Plugin.cs" "$src\AssemblyInfo.cs"
if($LASTEXITCODE -ne 0){throw 'Plugin build failed'}


