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
    && curl -fsSL --retry 3 https://supabase-downloads.s3-ap-southeast-1.amazonaws.com/prod/ssl/prod-ca-2021.crt -o /publish/supabase-root-ca.crt \
    && test "$(openssl x509 -in /publish/supabase-root-ca.crt -noout -fingerprint -sha256 | cut -d= -f2 | tr -d ':')" = "807025AD50D4ED219D2C9C7D299C004F824EB00CF7F65AFEF607D07B72E6CAFA" \
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
