@echo off
"C:\Program Files\JetBrains\dotCover\dotCover.exe" cover /TargetExecutable=dotnet /TargetArguments="test Stella.Ergosfare.sln --no-build" /Output=coverage.dcvr
