# Jedna slika koja servira i API i sagrađeni SPA.
#
# Frontend i backend idu sa istog origina, pa nema CORS-a ni zasebnog hostinga za statiku,
# a `/dionica/*` i `/stanica/*` rješava fallback u Api-ju.

# --- web ---
FROM node:22-alpine AS web
WORKDIR /web

COPY src/Vodostaji.Web/package*.json ./
RUN npm ci

COPY src/Vodostaji.Web/ ./
# Tipovi se ne generišu ovdje: `npm run generate:api` traži živi API. Generisani
# `src/api/schema.ts` je u repozitoriju upravo zato da build ne zavisi od mreže.
RUN npm run build

# --- api ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS api
WORKDIR /src

COPY Directory.Build.props ./
COPY src/Vodostaji.Core/*.csproj src/Vodostaji.Core/
COPY src/Vodostaji.Data/*.csproj src/Vodostaji.Data/
COPY src/Vodostaji.Ingest/*.csproj src/Vodostaji.Ingest/
COPY src/Vodostaji.Api/*.csproj src/Vodostaji.Api/
RUN dotnet restore src/Vodostaji.Api/Vodostaji.Api.csproj

COPY src/ src/
RUN dotnet publish src/Vodostaji.Api/Vodostaji.Api.csproj -c Release -o /app --no-restore

# --- runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# InvariantGlobalization je isključen namjerno: aplikacija stoji ili pada na `Europe/Sarajevo`
# i DST prelazima, pa joj treba puna baza vremenskih zona. Alpine bi je tražio zasebno.
ENV DOTNET_RUNNING_IN_CONTAINER=true \
    ASPNETCORE_URLS=http://+:8080 \
    TZ=UTC

COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot

# Ingest piše GeoJSON koji mapa čita; direktorij mora postojati i biti upisiv.
RUN mkdir -p /app/data
ENV WebRoot=/app/wwwroot

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s \
  CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Vodostaji.Api.dll"]
