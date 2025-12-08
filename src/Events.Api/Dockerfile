FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# This is the SDK image – used only for building/publishing the app inside the container.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy just the project file first so Docker can cache restore layers nicely.
COPY ["Events.Api.csproj", "./"]
RUN dotnet restore "Events.Api.csproj"

# Now copy the rest of the source and build it.
COPY . .
RUN dotnet publish "Events.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final runtime image – nice and slim, just the built bits.
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Tell Docker how to start the app.
ENTRYPOINT ["dotnet", "Events.Api.dll"]
