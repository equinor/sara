# https://hub.docker.com/_/microsoft-dotnet
FROM node:26-slim@sha256:36793bcbb160a6a16cede1e62b93f84e26b78a972a8a4c0b879c1c12f22761ec AS frontend-build
ENV CI=true
RUN npm install --global corepack@0.36.0 && corepack enable
WORKDIR /app
COPY frontend/package.json frontend/pnpm-lock.yaml frontend/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY frontend/ .
RUN pnpm build --outDir /frontend-dist

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
WORKDIR /source

# copy everything
COPY . .

WORKDIR /source/api
RUN dotnet publish -c release -o /app

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee
WORKDIR /app
COPY --from=build /app ./
COPY --from=frontend-build /frontend-dist ./wwwroot

EXPOSE 8100

# Runtime user change to non-root for added security
RUN useradd -ms /bin/bash --uid 1001 isar
RUN chown -R 1001 /app
RUN chmod 755 /app
USER 1001

ENTRYPOINT ["dotnet", "api.dll", "--urls=http://0.0.0.0:8100"]
