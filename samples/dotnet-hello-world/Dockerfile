FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY HelloWorld.csproj ./
COPY packages.lock.json ./
COPY Program.cs ./

RUN dotnet restore --locked-mode HelloWorld.csproj
RUN dotnet publish HelloWorld.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/devcontainers/dotnet:8.0
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
WORKDIR /workspace

COPY --from=build /app/publish/ ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "HelloWorld.dll"]
