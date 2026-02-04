FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# Copy csproj files and restore dependencies
COPY ["SaaSBase.Api/SaaSBase.Api.csproj", "SaaSBase.Api/"]
COPY ["SaaSBase.Application/SaaSBase.Application.csproj", "SaaSBase.Application/"]
COPY ["SaaSBase.Domain/SaaSBase.Domain.csproj", "SaaSBase.Domain/"]
COPY ["SaaSBase.Infrastructure/SaaSBase.Infrastructure.csproj", "SaaSBase.Infrastructure/"]

RUN dotnet restore "SaaSBase.Api/SaaSBase.Api.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src/SaaSBase.Api"
RUN dotnet build "SaaSBase.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SaaSBase.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SaaSBase.Api.dll"]
