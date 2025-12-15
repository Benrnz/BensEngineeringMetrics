cls
@ECHO OFF
ECHO Start time: %date% %time%
cd \Development\BensEngineeringMetrics
git checkout master
git pull
dotnet build BensEngineeringMetrics.sln
cd \Development\BensEngineeringMetrics\Bin\Debug\net9.0
BensEngineeringMetricsConsole.exe BUG_STATS
BensEngineeringMetricsConsole.exe INCIDENTS
BensEngineeringMetricsConsole.exe INIT_ALL
BensEngineeringMetricsConsole.exe SPRINT_PLAN
BensEngineeringMetricsConsole.exe ENG_TASK_ANALYSIS

ECHO Finish time: %date% %time%

