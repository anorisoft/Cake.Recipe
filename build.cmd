@echo off

set scriptFileName=.\Tools\Resources\build.ps1
set scriptFolderPath=%~dp0

rem set command="Start-Process powershell \"-ExecutionPolicy Bypass -NoProfile -NoExit -Command `\"cd \`\"%scriptFolderPath%`\"; & \`\"%scriptFileName%\`\"`\"\" -Verb RunAs"
set command="%scriptFileName%"


cd %scriptFolderPath%
echo %command%
powershell -Command %command%
rem pause
