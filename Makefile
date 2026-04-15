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

check:
	dotnet build api
	cd frontend && pnpm exec tsc --noEmit

test:
	dotnet test

format:
	dotnet tool restore
	dotnet csharpier format .

run-docker-compose:
	docker compose up --build

run-docker-compose-nginx:
	docker compose -f various/docker-compose.nginx.yaml up --build
