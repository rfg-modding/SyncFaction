set -e
rm -rf coverage
dotnet test -p:CollectCoverage=true -p:CoverletOutput="../../../coverage/" -p:MergeWith="../../../coverage/coverage.json" -p:CoverletOutputFormat=\"json,opencover\" -maxcpucount:1 $1
reportgenerator -reports:coverage/coverage.opencover.xml -targetdir:coverage/report

