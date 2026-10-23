# Immagine dell'API: copia l'output di `dotnet publish` (build una volta, copia nell'immagine).
# Da root repository:
#   dotnet publish apps/api -c Release -o dist/apps/api -p:OpenApiGenerateDocuments=false -p:InformationalVersion=X.Y.Z
#   docker build -f apps/api/Dockerfile -t micromes-api:local .
# In alternativa: npx nx run Api:docker
FROM mcr.microsoft.com/dotnet/aspnet:10.0

ARG VERSION=0.0.0-dev
ARG COMMIT=unknown
ARG BUILD_DATE=unknown

LABEL org.opencontainers.image.title="MicroMES API" \
      org.opencontainers.image.description="MicroMES backend API" \
      org.opencontainers.image.source="https://github.com/illy-w/micromes" \
      org.opencontainers.image.licenses="AGPL-3.0-only" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${COMMIT}" \
      org.opencontainers.image.created="${BUILD_DATE}"

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    MICROMES_COMMIT=${COMMIT} \
    MICROMES_BUILD_DATE=${BUILD_DATE}

WORKDIR /app
COPY dist/apps/api/ ./

# L'immagine aspnet include l'utente non root "app" (UID 1654).
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "MicroMES.Api.dll"]
