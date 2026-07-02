FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Indigosoft.TestTask.sln ./
COPY Indigosoft.TestTask.Core/Indigosoft.TestTask.Core.csproj Indigosoft.TestTask.Core/
COPY Indigosoft.TestTask.Database/Indigosoft.TestTask.Database.csproj Indigosoft.TestTask.Database/
COPY Indigosoft.TestTask.Aggregator/Indigosoft.TestTask.Aggregator.csproj Indigosoft.TestTask.Aggregator/

RUN dotnet restore Indigosoft.TestTask.Aggregator/Indigosoft.TestTask.Aggregator.csproj

COPY . .

RUN dotnet publish Indigosoft.TestTask.Aggregator/Indigosoft.TestTask.Aggregator.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5200
EXPOSE 5200

ENTRYPOINT ["dotnet", "Indigosoft.TestTask.Aggregator.dll"]
