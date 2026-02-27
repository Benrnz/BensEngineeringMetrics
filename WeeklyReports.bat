cls
@ECHO OFF
ECHO Start time: %date% %time%
git checkout master
git pull
dotnet build BensEngineeringMetrics.sln
cd BensEngineeringMetrics.Console\Bin\Debug\net9.0
BensEngineeringMetrics.Console.exe BATCH BUG_STATS INCIDENTS INIT_ALL SPRINT_PLAN ENG_TASK_ANALYSIS ENVEST_EXALATE REMAINING_WORK_ENVEST BUG_STATS_NZB

ECHO Finish time: %date% %time%

