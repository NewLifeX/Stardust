@echo off
setlocal enabledelayedexpansion

@REM create star folder
mkdir star

@REM traverse all zip files in the current directory
for %%f in (*.zip) do (
    @REM skip agent.zip
    if /I "%%f" NEQ "agent.zip" (
        @REM get filename (without extension)
        set "filename=%%~nf"

        @REM filename to lowercase
        for %%A in (a b c d e f g h i j k l m n o p q r s t u v w x y z) do (
            call set "filename=!filename:%%A=%%A!"
        )

        @REM remove star characters from filename
        set "filename=!filename:star=!"
        echo !filename!

        @REM create folder with filename
        mkdir "star\!filename!" 2>nul
        
        @REM move zip file to folder
        move /Y "%%f" "star\!filename!\"
    )
)

@REM tar agent folder
tar -xf agent.zip

@REM test agent
call agent\StarAgent.exe

echo move completed
endlocal
pause

