run:
	dotnet run --project api

build:
	dotnet build api

test:
	dotnet test

format:
	dotnet tool restore
	dotnet csharpier format .
