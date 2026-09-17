# Multi-stage build for the vulnerable API. Flags are supplied at RUNTIME via env vars
# (never baked in — see BUILD.md §6 / .env.example).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY VulnApi.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# The compose file runs this read-only with a tmpfs /tmp; the app writes nothing to disk.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "VulnApi.dll"]
