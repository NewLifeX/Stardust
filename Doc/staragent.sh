# staragent cube.zip urls=http://*:80
# dotnet startagent.dll cube.zip urls=htts://*:80
# echo $@ # cube.zip urls=http://*:80
# echo $(pwd) # Workspace 
# BASEDIR=$(dirname "$0")
# echo "$BASEDIR"
SCRIPT=$(readlink -f "$0")
SCRIPTPATH=$(dirname "$SCRIPT")
# echo $SCRIPTPATH
# echo 
STAR_AGENT=$SCRIPTPATH"/staragent.dll" # star agent 的全路径
# echo $STAR_AGENT
ZIP_DIR=$(pwd)
dotnet $STAR_AGENT $ZIP_DIR"/"$@