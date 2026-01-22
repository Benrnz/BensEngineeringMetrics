cls
@ECHO OFF
ECHO Start time: %date% %time%
git checkout master
git pull
dotnet build BensEngineeringMetrics.sln
cd BensEngineeringMetrics.Console\Bin\Debug\net9.0
BensEngineeringMetrics.Console.exe BUG_STATS
BensEngineeringMetrics.Console.exe INCIDENTS
BensEngineeringMetrics.Console.exe INIT_ALL
BensEngineeringMetrics.Console.exe SPRINT_PLAN
BensEngineeringMetrics.Console.exe ENG_TASK_ANALYSIS
BensEngineeringMetrics.Console.exe ENVEST_EXALATE

ECHO Finish time: %date% %time%

