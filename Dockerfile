# Use the official .NET 10.0 SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# Copy the csproj and restore dependencies
COPY YuGo/YuGo.csproj ./YuGo/
RUN dotnet restore ./YuGo/YuGo.csproj

# Copy everything else and publish the release
COPY . ./
RUN dotnet publish ./YuGo/YuGo.csproj -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build-env /app/out .

# Expose the API port
EXPOSE 5042

# Run the application
ENTRYPOINT ["dotnet", "YuGo.dll"]
