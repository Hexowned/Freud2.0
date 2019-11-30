git pull

dotnet publish --self-contained -r win-x64 -c Release
.\Dependencies\rcedit-x64.exe "Freud\bin\Release\netcoreapp2.1\win-x64\Freud.exe" --set-icon "Freud\icon.ico"

cd .\Frued
dotnet ef database update

cd .\bin\Release\netcoreapp2.1\win-x64
Start-process -FilePath ".\Freud.exe"