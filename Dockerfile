FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY QuoteFlow.slnx .
COPY src/QuoteFlow.Core/QuoteFlow.Core.csproj src/QuoteFlow.Core/
COPY src/QuoteFlow.Application/QuoteFlow.Application.csproj src/QuoteFlow.Application/
COPY src/QuoteFlow.Infrastructure/QuoteFlow.Infrastructure.csproj src/QuoteFlow.Infrastructure/
COPY src/QuoteFlow.API/QuoteFlow.API.csproj src/QuoteFlow.API/

RUN dotnet restore src/QuoteFlow.API/QuoteFlow.API.csproj

COPY src/ src/

RUN dotnet publish src/QuoteFlow.API/QuoteFlow.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "QuoteFlow.API.dll"]
