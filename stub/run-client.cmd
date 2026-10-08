@echo off
setlocal
goto :compile

::BEGIN_SOURCE
{{CLIENT_SOURCE}}
::END_SOURCE

:compile
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo .NET Framework C# compiler was not found.
    exit /b 1
)

set "SOURCE=%TEMP%\chrysails-client-%RANDOM%-%RANDOM%.cs"
set "OUTPUT=%SOURCE:.cs=.exe%"
set "SCRIPT_PATH=%~f0"
powershell.exe -NoProfile -Command "$text = [IO.File]::ReadAllText($env:SCRIPT_PATH); $match = [regex]::Match($text, '(?s)::BEGIN_SOURCE\r?\n(.*?)\r?\n::END_SOURCE'); if (-not $match.Success) { exit 1 }; [IO.File]::WriteAllText($env:SOURCE, $match.Groups[1].Value)"
if errorlevel 1 (
    echo Could not extract client source.
    exit /b 1
)

"%CSC%" /nologo /target:exe /out:"%OUTPUT%" "%SOURCE%"
if errorlevel 1 (
    del "%SOURCE%"
    exit /b 1
)

"%OUTPUT%"
set "RESULT=%ERRORLEVEL%"
del "%SOURCE%" "%OUTPUT%"
exit /b %RESULT%
