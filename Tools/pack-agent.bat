set base=..\Bin\Agent

del %base%\*.zip /f/q
del %base%\*.tar.gz /f/q

pushd %base%\net9.0
set name=staragent90
bash -c "tar -czf %name%.tar.gz -- *.exe *.dll *.runtimeconfig.json"
move %name%.tar.gz ..\
bash -c "zip -9 %name%.zip -- *.exe *.dll *.runtimeconfig.json"
move %name%.zip ..\
popd

pushd %base%\net8.0
set name=staragent80
bash -c "tar -czf %name%.tar.gz -- *.exe *.dll *.runtimeconfig.json"
move %name%.tar.gz ..\
bash -c "zip -9 %name%.zip -- *.exe *.dll *.runtimeconfig.json"
move %name%.zip ..\
popd

pushd %base%\net7.0
set name=staragent70
bash -c "tar -czf %name%.tar.gz -- *.exe *.dll *.runtimeconfig.json"
move %name%.tar.gz ..\
bash -c "zip -9 %name%.zip -- *.exe *.dll *.runtimeconfig.json"
move %name%.zip ..\
popd

pushd %base%\net6.0
set name=staragent60
bash -c "tar -czf %name%.tar.gz -- *.exe *.dll *.runtimeconfig.json"
move %name%.tar.gz ..\
bash -c "zip -9 %name%.zip -- *.exe *.dll *.runtimeconfig.json"
move %name%.zip ..\
popd

pushd %base%\net5.0
set name=staragent50
bash -c "tar -czf %name%.tar.gz -- *.exe *.dll *.runtimeconfig.json"
move %name%.tar.gz ..\
bash -c "zip -9 %name%.zip -- *.exe *.dll *.runtimeconfig.json"
move %name%.zip ..\
popd

pushd %base%\netcoreapp3.1
set name=staragent31
bash -c "tar -czf %name%.tar.gz -- *.exe *.dll *.runtimeconfig.json"
move %name%.tar.gz ..\
bash -c "zip -9 %name%.zip -- *.exe *.dll *.runtimeconfig.json"
move %name%.zip ..\
popd

pushd %base%\net461
set name=staragent461
bash -c "zip -9 %name%.zip -- *.exe *.dll StarAgent.exe.config"
move %name%.zip ..\
popd

pushd %base%\net45
set name=staragent45
bash -c "zip -9 %name%.zip -- *.exe *.dll StarAgent.exe.config"
move %name%.zip ..\
popd
