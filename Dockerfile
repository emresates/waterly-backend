# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Waterly.Api/Waterly.Api.csproj Waterly.Api/
RUN dotnet restore Waterly.Api/Waterly.Api.csproj

COPY Waterly.Api/ Waterly.Api/
RUN dotnet publish Waterly.Api/Waterly.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- EF Core migration bundle ----------
FROM build AS migrations-build
RUN dotnet tool install --global dotnet-ef --version "10.*"
ENV PATH="$PATH:/root/.dotnet/tools"
# Program.cs refuses to start without these; dummy values are only used to build the bundle.
RUN ConnectionStrings__DefaultConnection="Host=build" \
    Jwt__SigningKey="build-time-placeholder-key-at-least-32-bytes" \
    dotnet ef migrations bundle --project Waterly.Api/Waterly.Api.csproj -o /app/efbundle --force

# ---------- migrator (runs migrations once and exits) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrator
WORKDIR /app
COPY --from=migrations-build /app/efbundle .
COPY --from=build /app/publish/appsettings*.json ./
ENTRYPOINT ["sh", "-c", "./efbundle --connection \"$ConnectionStrings__DefaultConnection\""]

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Waterly.Api.dll"]
