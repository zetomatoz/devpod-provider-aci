FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props ./
COPY Directory.Packages.props ./
COPY samples/dotnet-hello-world/HelloWorld.csproj ./samples/dotnet-hello-world/
COPY samples/dotnet-hello-world/packages.lock.json ./samples/dotnet-hello-world/
COPY samples/dotnet-hello-world/Program.cs ./samples/dotnet-hello-world/

WORKDIR /src/samples/dotnet-hello-world

RUN dotnet restore --locked-mode HelloWorld.csproj
RUN mkdir -p /app/publish
RUN dotnet publish HelloWorld.csproj -c Release -o /app/publish /p:UseAppHost=false --no-restore

FROM mcr.microsoft.com/devcontainers/dotnet:8.0
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
WORKDIR /workspace

COPY --from=build /app/publish/ ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "HelloWorld.dll"]
