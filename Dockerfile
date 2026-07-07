FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/CryptoTracker.csproj backend/
RUN dotnet restore backend/CryptoTracker.csproj
COPY backend/ backend/
RUN dotnet publish backend/CryptoTracker.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "CryptoTracker.dll"]
