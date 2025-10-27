# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "CallbreakApp.csproj"
RUN dotnet publish "CallbreakApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY appsettings.Production.json .  
EXPOSE 80
ENTRYPOINT ["dotnet", "CallbreakApp.dll"]