# Eseguire dalla root del repository dopo `npx nx build web`:
#   docker build -f apps/web/Dockerfile -t micromes-web:local .
# In alternativa: npx nx run web:docker
FROM nginxinc/nginx-unprivileged:1.27-alpine

ARG VERSION=0.0.0-dev
ARG COMMIT=unknown
ARG BUILD_DATE=unknown

LABEL org.opencontainers.image.title="MicroMES Web" \
      org.opencontainers.image.description="MicroMES frontend (SPA)" \
      org.opencontainers.image.source="https://github.com/illy-w/micromes" \
      org.opencontainers.image.licenses="AGPL-3.0-only" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${COMMIT}" \
      org.opencontainers.image.created="${BUILD_DATE}"

COPY apps/web/nginx.conf /etc/nginx/nginx.conf
COPY apps/web/security-headers.conf /etc/nginx/security-headers.conf
COPY dist/apps/web/browser /usr/share/nginx/html

# nginx-unprivileged gira con l'utente "nginx" (UID 101).
EXPOSE 8080
