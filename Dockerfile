FROM microsoft/aspnetcore-build:2.0 AS build-env
WORKDIR /app/ThunderED

COPY ThunderED/*.csproj ./
RUN dotnet restore

COPY ThunderED/. ./
COPY Version.cs /app
RUN dotnet publish -c Release -r debian-x64 -o out

FROM microsoft/aspnetcore:2.0
WORKDIR /app/ThunderED
COPY --from=build-env /app/ThunderED/out .
ENTRYPOINT ["dotnet", "ThunderED.dll"]
