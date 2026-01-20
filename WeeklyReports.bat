cls
@ECHO OFF
ECHO Start time: %date% %time%
git checkout master
git pull
dotnet build BensEngineeringMetrics.sln
cd BensEngineeringMetrics.Console\Bin\Debug\net9.0
BensEngineeringMetricsConsole.exe BUG_STATS
BensEngineeringMetricsConsole.exe INCIDENTS
BensEngineeringMetricsConsole.exe INIT_ALL
BensEngineeringMetricsConsole.exe SPRINT_PLAN
BensEngineeringMetricsConsole.exe ENG_TASK_ANALYSIS
BensEngineeringMetricsConsole.exe ENVEST_EXALATE

ECHO Finish time: %date% %time%

