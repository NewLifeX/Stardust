@echo off

set name=StarWeb
set clover=..\..\Tools\clover.exe

rem 遍历当前目录中的exe文件
for %%f in (*.exe) do (
    rem 获取文件名（去掉扩展名）
    set "name=%%~nf"
    goto :found
)

rem 如果找到了exe文件，打印文件名
:found
if defined name (
    del %name%.zip /f/q
    %clover% zip %name%.zip *.exe *.dll *.pdb appsettings.json *.runtimeconfig.json
) else (
    echo No exe file found in the current directory.
)
