FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env
WORKDIR /app/ThunderED

COPY ThunderED/*.csproj ./
RUN dotnet restore

COPY ThunderED/. ./
COPY Version.cs /app
COPY version.txt /app
COPY LICENSE /app
RUN dotnet publish -c Release -r debian-x64 -o out

FROM mcr.microsoft.com/dotnet/core/runtime:3.1 AS runtime
WORKDIR /app/ThunderED
COPY --from=build-env /app/ThunderED/out .
ENTRYPOINT ["dotnet", "ThunderED.dll"]
