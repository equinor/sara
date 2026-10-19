# https://hub.docker.com/_/microsoft-dotnet
FROM node:26-slim@sha256:0d1e33586362bb9e608c4e751e7a9e7edb55c9ac11461ed5666af696cc461dd4 AS frontend-build
ENV CI=true
RUN npm install --global corepack@0.36.0 && corepack enable
WORKDIR /app
COPY frontend/package.json frontend/pnpm-lock.yaml frontend/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY frontend/ .
RUN pnpm build --outDir /frontend-dist

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:0d99ebf1ccad15037b5c98c9fe8979d6b8dbac23812feae2277a0b277b5c5ffa AS build
WORKDIR /source

# copy everything
COPY . .

WORKDIR /source/api
RUN dotnet publish -c release -o /app

# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:d5633778c754e89a386882eddf8dbee6942447a8a4b8267c19cf1e1158d0b0ac
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
