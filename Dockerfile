FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ValePedagio.Api/ValePedagio.Api.csproj ValePedagio.Api/
COPY ValePedagio.Application/ValePedagio.Application.csproj ValePedagio.Application/
COPY ValePedagio.Domain/ValePedagio.Domain.csproj ValePedagio.Domain/
COPY ValePedagio.Infrastructure/ValePedagio.Infrastructure.csproj ValePedagio.Infrastructure/

RUN dotnet restore ValePedagio.Api/ValePedagio.Api.csproj

COPY . .
RUN dotnet publish ValePedagio.Api/ValePedagio.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ValePedagio.Api.dll"]
