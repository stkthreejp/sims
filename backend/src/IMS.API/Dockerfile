FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/IMS.API/IMS.API.csproj", "src/IMS.API/"]
COPY ["src/IMS.Application/IMS.Application.csproj", "src/IMS.Application/"]
COPY ["src/IMS.Domain/IMS.Domain.csproj", "src/IMS.Domain/"]
COPY ["src/IMS.Infrastructure/IMS.Infrastructure.csproj", "src/IMS.Infrastructure/"]
RUN dotnet restore "src/IMS.API/IMS.API.csproj"
COPY . .
WORKDIR "/src/src/IMS.API"
RUN dotnet build "IMS.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "IMS.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "IMS.API.dll"]
