# 게임 서버 이미지: SDK 이미지로 publish 한 뒤 ASP.NET 런타임 이미지에 올린다 (빌드 컨텍스트 = 저장소 루트)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY shared/Ashveil.Shared.csproj shared/
COPY server/src/Ashveil.Server/Ashveil.Server.csproj server/src/Ashveil.Server/
RUN dotnet restore server/src/Ashveil.Server/Ashveil.Server.csproj
COPY shared/ shared/
COPY server/src/ server/src/
RUN dotnet publish server/src/Ashveil.Server/Ashveil.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Ashveil.Server.dll"]
