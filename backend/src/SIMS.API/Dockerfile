ARG ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:8.0
ARG SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:8.0

FROM ${ASPNET_IMAGE} AS base
WORKDIR /app
RUN apt-get update && \
    apt-get install -yq --no-install-recommends \
    libasound2 libatk1.0-0 libc6 libcairo2 libcups2 libdbus-1-3 \
    libexpat1 libfontconfig1 libgcc1 libgconf-2-4 libgdk-pixbuf2.0-0 libglib2.0-0 libgtk-3-0 libnspr4 \
    libpango-1.0-0 libpangocairo-1.0-0 libstdc++6 libx11-6 libx11-xcb1 libxcb1 \
    libxcursor1 libxdamage1 libxext6 libxfixes3 libxi6 libxrandr2 libxrender1 libxss1 libxtst6 \
    libnss3 libgbm1 && \
    rm -rf /var/lib/apt/lists/*
EXPOSE 8080

FROM ${SDK_IMAGE} AS build
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
