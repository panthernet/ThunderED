FROM mcr.microsoft.com/dotnet/core/aspnet:3.1-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

RUN mkdir "/opt/thdwebserver"

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-buster AS build
WORKDIR /src
COPY ["THDWebServer/THDWebServer.csproj", "THDWebServer/"]
RUN dotnet restore "THDWebServer/THDWebServer.csproj"
COPY . .
WORKDIR "/src/THDWebServer"
RUN dotnet build "THDWebServer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "THDWebServer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "THDWebServer.dll"]

#FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-env
#WORKDIR /app/ThunderED
#
#COPY ThunderED/*.csproj ./
#RUN dotnet restore
#
#COPY ThunderED/. ./
#COPY Version.cs /app
#COPY version.txt /app
#COPY LICENSE /app
#RUN dotnet publish -c Release -r debian-x64 -o out
#
#FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS build-res
#WORKDIR /app/Restarter
#
#COPY Restarter/*.csproj ./
#RUN dotnet restore
#
#COPY Restarter/. ./
#COPY Version.cs /app
#COPY version.txt /app
#COPY LICENSE /app
#RUN dotnet publish -c Release -r debian-x64 -o out
#
#
#FROM mcr.microsoft.com/dotnet/core/runtime:3.1 AS runtime
#WORKDIR /app/ThunderED
#COPY --from=build-env /app/ThunderED/out .
#COPY --from=build-res /app/Restarter/out .
#ENTRYPOINT ["dotnet", "ThunderED.dll"]

