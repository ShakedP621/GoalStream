FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# SDK image just for building.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file first for better Docker layer caching.
COPY ["Highlights.Api.csproj", "./"]
RUN dotnet restore "Highlights.Api.csproj"

# Then copy the rest of the source and publish.
COPY . .
RUN dotnet publish "Highlights.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final runtime stage.
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Highlights.Api.dll"]
