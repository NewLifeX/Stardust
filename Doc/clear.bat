@echo off
set root=%cd%\..\
pushd ..\

::for /r %root% %%i in (*.pdb,*.vshost.*) do (del %%i)

::for /r %root% %%i in (obj,bin) do (IF EXIST %%i RD /s /q %%i)
::for /r %root% %%i in (obj,bin) do (IF EXIST %%i echo %%i %%~ti)
for /f "delims=" %%i in ('dir /ad/b/s .') do (
    if EXIST %%i\bin echo %%i\bin
    if EXIST %%i\bin rd /s/q %%i\bin
    if EXIST %%i\obj echo %%i\obj
    if EXIST %%i\obj rd /s/q %%i\obj
)
::for /f "delims=" %%i in ('dir /ad/b .') do (if EXIST %%i\bin echo %%i\bin)

popd