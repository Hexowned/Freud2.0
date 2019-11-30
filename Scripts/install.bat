@echo off

if [%1]==[] goto exec
echo Waiting for the bot to shut down ...
wait 5

:exec
del Freud.zip
del FreudResources.zip
echo Downloading ...
Powershell.exe -executionpolicy remotesigned -File dl.ps1
echo Extracting ...
"C:\Program Files\WinRAR\WinRAR.exe" x Freud.zip -aoa
"C:\Program Files\WinRAR\WinRAR.exe" x FreudResources.zip -aoa "-oResources"
echo Starting the bot ...
START dotnet Freud.dll