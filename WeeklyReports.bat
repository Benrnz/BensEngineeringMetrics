cls
@ECHO OFF
ECHO Start time: %date% %time%
git checkout master
git pull
dotnet build BensEngineeringMetrics.sln
cd BensEngineeringMetrics.Console\Bin\Debug\net10.0
BensEngineeringMetrics.Console.exe BATCH BUG_STATS INCIDENTS ENG_TASK_ANALYSIS BUG_STATS_NZB DAILY

ECHO Finish time: %date% %time%

