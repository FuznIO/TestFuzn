# The worker app built from this repo, for the local test services (src/docker-compose.yml).
# For a worker image from the published tool, see examples/cluster/Dockerfile.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["Directory.Packages.props", "."]
COPY ["TestFuzn.Worker/TestFuzn.Worker.csproj", "TestFuzn.Worker/"]
RUN dotnet restore "./TestFuzn.Worker/TestFuzn.Worker.csproj"
COPY . .
RUN dotnet publish "./TestFuzn.Worker/TestFuzn.Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

# aspnet, not runtime: test projects that host a web app need ASP.NET Core too.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
COPY --from=build /app/publish /worker
RUN mkdir /work && chown $APP_UID /work
USER $APP_UID

# Logs can be large; keep the last two runs per test project.
ENV TESTFUZN_KEEP_LAST_N_RUNS=2

ENTRYPOINT ["dotnet", "/worker/Fuzn.TestFuzn.Worker.dll"]
CMD ["--pool", "docker", "--work-dir", "/work"]
