run:
	$(MAKE) -j2 run-api run-frontend

run-api:
	dotnet run --project api

run-frontend:
	@echo "Waiting for backend on port 8100..."
	@while ! curl -sf http://localhost:8100/api/health > /dev/null 2>&1; do sleep 0.5; done
	@echo "Frontend ready at \033[36mhttp://localhost:8099\033[0m"
	cd frontend && pnpm dev --clearScreen false

build:
	dotnet build api

test:
	dotnet test

format:
	dotnet tool restore
	dotnet csharpier format .
