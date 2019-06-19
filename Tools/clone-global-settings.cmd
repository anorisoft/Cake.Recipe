@echo off
rem ***************************************************************************
rem * Clone script for Global Settings Repository
rem ***************************************************************************
powershell write-host -fore Cyan  ***************************************************************************
powershell write-host -fore Cyan "Clone script for Global Settings Repository"
powershell write-host -fore Cyan  ***************************************************************************
powershell write-host

IF EXIST GlobalSettings (
	powershell write-host -fore Red  Deleting Global Settings Directory.
	rd GlobalSettings /s /q
	powershell write-host
)

powershell write-host -fore Yellow "Removing Global Settings Submodule from git.
git rm -r GlobalSettings
powershell write-host

powershell write-host -fore Yellow "Adding Global Settings Submodule from git.
git submodule add -f https://github.com/anorisoft/Cake.GlobalSettings.git GlobalSettings
powershell write-host

powershell write-host -fore Yellow "Pull Global Settings from git.
git -C GlobalSettings pull -v --progress
powershell write-host

pause