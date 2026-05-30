# syntax=docker/dockerfile:1.7

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY .editorconfig global.json Directory.Build.props Directory.Packages.props DiscWeave.slnx ./
COPY src/DiscWeave.Api/DiscWeave.Api.csproj src/DiscWeave.Api/
COPY src/DiscWeave.Application/DiscWeave.Application.csproj src/DiscWeave.Application/
COPY src/DiscWeave.Domain/DiscWeave.Domain.csproj src/DiscWeave.Domain/
COPY src/DiscWeave.Importing/DiscWeave.Importing.csproj src/DiscWeave.Importing/
COPY src/DiscWeave.Infrastructure/DiscWeave.Infrastructure.csproj src/DiscWeave.Infrastructure/
RUN dotnet restore src/DiscWeave.Api/DiscWeave.Api.csproj

COPY src/DiscWeave.Api/ src/DiscWeave.Api/
COPY src/DiscWeave.Application/ src/DiscWeave.Application/
COPY src/DiscWeave.Domain/ src/DiscWeave.Domain/
COPY src/DiscWeave.Importing/ src/DiscWeave.Importing/
COPY src/DiscWeave.Infrastructure/ src/DiscWeave.Infrastructure/
RUN dotnet publish src/DiscWeave.Api/DiscWeave.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM build AS migrations
RUN dotnet tool install --global dotnet-ef --version 10.0.6
ENV PATH="${PATH}:/root/.dotnet/tools"
ENTRYPOINT ["dotnet", "ef", "database", "update", "--project", "src/DiscWeave.Infrastructure/DiscWeave.Infrastructure.csproj", "--startup-project", "src/DiscWeave.Api/DiscWeave.Api.csproj"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN mkdir -p /var/lib/discweave/release-covers /var/lib/discweave/desktop \
    && chown -R app:app /var/lib/discweave

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .
USER app
ENTRYPOINT ["dotnet", "DiscWeave.Api.dll"]
