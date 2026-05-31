# See https://aka.ms/customizecontainer to learn how to customize your debug container
# and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 26543
EXPOSE 5050

# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["BatchPlant/BatchPlant.csproj", "BatchPlant/"]
RUN dotnet restore "./BatchPlant/BatchPlant.csproj"
COPY . .
WORKDIR "/src/BatchPlant"
RUN dotnet build "./BatchPlant.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./BatchPlant.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Create config directory and copy default config for Docker override path
RUN mkdir -p /config
COPY BatchPlant/Config/BatchPlantServer.Config.xml /config/BatchPlantServer.Config.xml

ENTRYPOINT ["dotnet", "BatchPlant.dll"]