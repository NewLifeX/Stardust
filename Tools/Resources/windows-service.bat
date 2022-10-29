
copy Backup/StarAgent.config agent/Config/

cd agent
StarAgent.exe -stop
StarAgent.exe -u
StarAgent.exe -i
StarAgent.exe -start
cd ..
