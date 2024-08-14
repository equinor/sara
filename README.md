# Inspection Data Analyzer

IDA (Inspection Data Analyzer) is a repository for running pipelines to analyze data coming from various inspections.

See [LocalFunctionProj](./functions/LocalFunctionProj/) for an example of how to set up your pipeline. You can also run this function locally by running
'func start' from the [LocalFunctionProj](./functions/LocalFunctionProj/) folder, and then going to 'http://localhost:7071/api/HttpExample' to trigger it.

If you get 'Can't determine Project to build. Expected 1 .csproj or .fsproj but found 2' run 'dotnet clean' before running 'func start'
