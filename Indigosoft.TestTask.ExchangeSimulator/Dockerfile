FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Indigosoft.TestTask.sln ./
COPY Indigosoft.TestTask.Core/Indigosoft.TestTask.Core.csproj Indigosoft.TestTask.Core/
COPY Indigosoft.TestTask.ExchangeSimulator/Indigosoft.TestTask.ExchangeSimulator.csproj Indigosoft.TestTask.ExchangeSimulator/

RUN dotnet restore Indigosoft.TestTask.ExchangeSimulator/Indigosoft.TestTask.ExchangeSimulator.csproj

COPY . .

RUN dotnet publish Indigosoft.TestTask.ExchangeSimulator/Indigosoft.TestTask.ExchangeSimulator.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5100
EXPOSE 5100

ENTRYPOINT ["dotnet", "Indigosoft.TestTask.ExchangeSimulator.dll"]
