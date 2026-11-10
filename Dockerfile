FROM node:24-bookworm-slim AS frontend
WORKDIR /src
COPY package*.json ./
RUN npm ci
COPY index.html vite.config.ts tsconfig.json ./
COPY public ./public
COPY src ./src
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY NuGet.Config global.json ./
COPY backend ./backend
RUN dotnet publish backend/SmartCampus.Api -c Release -o /publish
# Supabase's Postgres pooler uses a private CA not found in the OS trust store.
# Fetch the official public CA during the build and fail closed if unavailable.
RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates openssl \
    && curl -fsSL --retry 3 https://supabase.com/downloads/prod-ca-2021.crt -o /publish/supabase-root-ca.crt \
    && openssl x509 -in /publish/supabase-root-ca.crt -noout \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend /publish ./
COPY --from=frontend /src/dist ./wwwroot
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
ENV PGSSLROOTCERT=/app/supabase-root-ca.crt
EXPOSE 8080
ENTRYPOINT ["dotnet", "SmartCampus.Api.dll"]
