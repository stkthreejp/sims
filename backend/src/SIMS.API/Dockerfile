FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/SIMS.API/SIMS.API.csproj", "src/SIMS.API/"]
COPY ["src/SIMS.Application/SIMS.Application.csproj", "src/SIMS.Application/"]
COPY ["src/SIMS.Domain/SIMS.Domain.csproj", "src/SIMS.Domain/"]
COPY ["src/SIMS.Infrastructure/SIMS.Infrastructure.csproj", "src/SIMS.Infrastructure/"]
RUN dotnet restore "src/SIMS.API/SIMS.API.csproj"
COPY . .
WORKDIR "/src/src/SIMS.API"
RUN dotnet build "SIMS.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SIMS.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SIMS.API.dll"]
