# Worker image (the machine layer). Generic: it serves any test project; every run brings its own build.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS tool
RUN dotnet tool install --tool-path /worker Fuzn.TestFuzn.Worker

FROM mcr.microsoft.com/dotnet/runtime:10.0
COPY --from=tool /worker /worker

# Logs can be large; keep the last two runs per test project.
ENV TESTFUZN_KEEP_LAST_N_RUNS=2

# Add what your tests need: other .NET runtimes, OS libraries, browsers, tools ...
# For Playwright tests with InstallPlaywright, add the browsers' OS libraries, e.g.:
# RUN apt-get update && apt-get install -y --no-install-recommends libnss3 libatk-bridge2.0-0 libgbm1 libasound2 && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["/worker/testfuzn-worker"]
CMD ["--pool", "default", "--work-dir", "/work"]
