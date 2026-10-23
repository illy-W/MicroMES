# Eseguire dalla root del repository dopo `npx nx build landing`:
#   docker build -f apps/landing/Dockerfile -t micromes-landing:local .
# In alternativa: npx nx run landing:docker

# Stadio 1: calcola gli hash CSP degli script inline dell'HTML prerenderizzato.
FROM node:24-alpine AS csp
WORKDIR /build
COPY apps/landing/scripts/generate-csp.mjs ./
COPY dist/apps/landing/browser ./browser
RUN node generate-csp.mjs ./browser ./csp.conf

# Stadio 2: sito statico servito da nginx.
FROM nginxinc/nginx-unprivileged:1.27-alpine

COPY apps/landing/nginx.conf /etc/nginx/nginx.conf
COPY apps/landing/security-headers.conf /etc/nginx/security-headers.conf
COPY --from=csp /build/csp.conf /etc/nginx/csp.conf
COPY dist/apps/landing/browser /usr/share/nginx/html

# Utente non root "nginx" (UID 101).
EXPOSE 8080
