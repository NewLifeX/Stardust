@echo off

set base=..\Bin\Agent
set clover=..\..\..\Tools\clover.exe

del %base%\*.zip /f/q
del %base%\*.tar.gz /f/q

pushd %base%\net9.0
set name=staragent90
%clover% tar ..\%name%.tar.gz *.exe *.dll *.runtimeconfig.json
%clover% zip ..\%name%.zip *.exe *.dll *.runtimeconfig.json
popd

pushd %base%\net8.0
set name=staragent80
%clover% tar ..\%name%.tar.gz *.exe *.dll *.runtimeconfig.json
%clover% zip ..\%name%.zip *.exe *.dll *.runtimeconfig.json
popd

pushd %base%\net7.0
set name=staragent70
%clover% tar ..\%name%.tar.gz *.exe *.dll *.runtimeconfig.json
%clover% zip ..\%name%.zip *.exe *.dll *.runtimeconfig.json
popd

pushd %base%\net6.0
set name=staragent60
%clover% tar ..\%name%.tar.gz *.exe *.dll *.runtimeconfig.json
%clover% zip ..\%name%.zip *.exe *.dll *.runtimeconfig.json
popd

pushd %base%\net5.0
set name=staragent50
%clover% tar ..\%name%.tar.gz *.exe *.dll *.runtimeconfig.json
%clover% zip ..\%name%.zip *.exe *.dll *.runtimeconfig.json
popd

pushd %base%\netcoreapp3.1
set name=staragent31
%clover% tar ..\%name%.tar.gz *.exe *.dll *.runtimeconfig.json
%clover% zip ..\%name%.zip *.exe *.dll *.runtimeconfig.json
popd

pushd %base%\net461
set name=staragent461
%clover% zip ..\%name%.zip *.exe *.dll StarAgent.exe.config
popd

pushd %base%\net45
set name=staragent45
%clover% zip ..\%name%.zip *.exe *.dll StarAgent.exe.config
popd
