# Etapa de compilación con el SDK 10
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copiar la solución y el proyecto
COPY tp-tacs-2026-grupo-4.sln ./
COPY FiguritasApi/FiguritasApi.csproj ./FiguritasApi/

# Restaurar dependencias (ahora con el SDK correcto)
RUN dotnet restore

# Copiar el resto del código y publicar
COPY . .
RUN dotnet publish FiguritasApi/FiguritasApi.csproj -c Release -o out

# Etapa de runtime con ASP.NET 10
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .

ENTRYPOINT ["dotnet", "FiguritasApi.dll"]