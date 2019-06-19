@echo off
rem ***************************************************************************
rem * Clone script for Cake Resources Repository
rem ***************************************************************************
powershell write-host -fore Cyan  ***************************************************************************
powershell write-host -fore Cyan "Clone script for Cake Resources Repository"
powershell write-host -fore Cyan  ***************************************************************************
powershell write-host

IF EXIST Resources (
	powershell write-host -fore Red Resources Settings Directory.
	rd Resources /s /q
	powershell write-host
)

powershell write-host -fore Yellow "Removing Resources Submodule from git.
git rm -r Resources
powershell write-host

powershell write-host -fore Yellow "Adding Resources Submodule from git.
git submodule add -f https://github.com/anorisoft/Cake.Resources.git Resources
powershell write-host


powershell write-host -fore Yellow "Pull Resources from git.
git -C GlobalSettings pull -v --progress
powershell write-host

pause
