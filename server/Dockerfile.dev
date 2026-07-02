FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY server.csproj ./
RUN dotnet restore

COPY . ./
RUN dotnet publish -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && groupadd --system poseidon \
    && useradd --system --gid poseidon --create-home poseidon

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish ./
RUN chown -R poseidon:poseidon /app

USER poseidon

ENTRYPOINT ["dotnet", "server.dll"]
