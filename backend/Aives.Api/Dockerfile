FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Aives.Domain/Aives.Domain.csproj Aives.Domain/
COPY Aives.Application/Aives.Application.csproj Aives.Application/
COPY Aives.Infrastructure/Aives.Infrastructure.csproj Aives.Infrastructure/
COPY Aives.Api/Aives.Api.csproj Aives.Api/
RUN dotnet restore Aives.Api/Aives.Api.csproj
COPY . .
RUN dotnet publish Aives.Api/Aives.Api.csproj -c Release -o /out --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /out .
RUN mkdir -p /app/wwwroot/uploads && chown -R app:app /app/wwwroot
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Aives.Api.dll"]
