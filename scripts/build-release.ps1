Set-Location "$PSScriptRoot\.."
dotnet build -c Release
if ($?) {
	Copy-Item ".\bin\Release\netstandard2.1\NameplateTweaks.dll" "..\..\BepInEx\plugins"
}