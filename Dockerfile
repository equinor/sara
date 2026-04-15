# https://hub.docker.com/_/microsoft-dotnet
FROM node:22-slim AS frontend-build
RUN corepack enable && corepack prepare pnpm@latest --activate
WORKDIR /app
COPY frontend/package.json frontend/pnpm-lock.yaml frontend/.npmrc ./
RUN pnpm install --frozen-lockfile
COPY frontend/ .
RUN pnpm build --outDir /frontend-dist

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# copy everything
COPY . .

WORKDIR /source/api
RUN dotnet publish -c release -o /app

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app ./
COPY --from=frontend-build /frontend-dist ./wwwroot
RUN apt-get update && apt-get install -y  --no-install-recommends apt-utils libgdiplus libc6-dev

EXPOSE 8100

# Runtime user change to non-root for added security
RUN useradd -ms /bin/bash --uid 1001 isar
RUN chown -R 1001 /app
RUN chmod 755 /app
USER 1001

ENTRYPOINT ["dotnet", "api.dll", "--urls=http://0.0.0.0:8100"]
