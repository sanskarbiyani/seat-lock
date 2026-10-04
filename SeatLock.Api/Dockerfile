FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["SeatLock.Api/SeatLock.Api.csproj", "SeatLock.Api/"]
RUN dotnet restore "SeatLock.Api/SeatLock.Api.csproj"

COPY . .
WORKDIR "/src/SeatLock.Api"

RUN dotnet publish "SeatLock.Api.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "SeatLock.Api.dll"]