# ==========================================
# 1. Etapa de Compilación Única (SDK)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# Copiamos la solución y los tres proyectos
COPY tp-tacs-2026-grupo-4.sln ./
COPY FiguritasApi/FiguritasApi.csproj ./FiguritasApi/
COPY FiguritasApi.Tests/FiguritasApi.Tests.csproj ./FiguritasApi.Tests/
COPY FiguritasClient/FiguritasClient.csproj ./FiguritasClient/
COPY FiguritasApi.Shared/FiguritasApi.Shared.csproj ./FiguritasApi.Shared/

# Restauramos todo de una vez
RUN dotnet restore

# Copiamos todo el código fuente
COPY . .

# Publicamos la API
RUN dotnet publish FiguritasApi/FiguritasApi.csproj -c Release -o /app/out-api

# Publicamos el Cliente Blazor
RUN dotnet publish FiguritasClient/FiguritasClient.csproj -c Release -o /app/out-client


# ==========================================
# 2. Etapa de Runtime para la API
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api-runtime
WORKDIR /app
COPY --from=build-env /app/out-api .
# Informamos que la API usa el 8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "FiguritasApi.dll"]


# ==========================================
# 3. Etapa de Runtime para el Cliente (Nginx)
# ==========================================
FROM nginx:alpine AS client-runtime
WORKDIR /usr/share/nginx/html

# Copiamos los archivos de Blazor (wwwroot) desde la etapa de compilación
COPY --from=build-env /app/out-client/wwwroot .

# Copiamos la configuración de Nginx (debe estar en la carpeta del cliente)
COPY FiguritasClient/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80