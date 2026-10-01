FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["smart-pet-care-api.csproj", "."]
RUN dotnet restore
COPY . .
RUN dotnet publish "smart-pet-care-api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
# curl exists in this image for the container healthcheck alone: the aspnet
# image ships without it, and the deploy waits on that check to decide whether
# a release actually came up. Before the publish output so a code change does
# not re-run apt.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "smart-pet-care-api.dll"]