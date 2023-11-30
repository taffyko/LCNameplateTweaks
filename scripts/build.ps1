Set-Location "$PSScriptRoot\.."
dotnet build
if ($?) {
	Remove-Item "..\..\BepInEx\plugins\NameplateTweaks.dll" -ErrorAction SilentlyContinue
	Copy-Item ".\bin\Debug\netstandard2.1\NameplateTweaks.dll" "..\..\BepInEx\scripts"
}