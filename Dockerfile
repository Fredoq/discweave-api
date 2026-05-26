# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props Cratebase.slnx ./
COPY src/Cratebase.Api/Cratebase.Api.csproj src/Cratebase.Api/
COPY src/Cratebase.Application/Cratebase.Application.csproj src/Cratebase.Application/
COPY src/Cratebase.Domain/Cratebase.Domain.csproj src/Cratebase.Domain/
COPY src/Cratebase.Importing/Cratebase.Importing.csproj src/Cratebase.Importing/
COPY src/Cratebase.Infrastructure/Cratebase.Infrastructure.csproj src/Cratebase.Infrastructure/
RUN dotnet restore src/Cratebase.Api/Cratebase.Api.csproj

COPY . .
RUN dotnet publish src/Cratebase.Api/Cratebase.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM build AS migrations
RUN dotnet tool install --global dotnet-ef --version 10.0.6
ENV PATH="${PATH}:/root/.dotnet/tools"
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "src/Cratebase.Infrastructure/Cratebase.Infrastructure.csproj", "--startup-project", "src/Cratebase.Api/Cratebase.Api.csproj"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Cratebase.Api.dll"]
