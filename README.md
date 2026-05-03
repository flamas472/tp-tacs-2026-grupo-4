# tp-tacs-2026-grupo-4

# Figuritas - Descripción del Proyecto y como ejecutarlo

La solución está dividida en 2 partes principales:

- **Backend API**: `Figuritas.Api`
  - Expone endpoints REST para gestionar usuarios, figuritas, intercambios y subastas.
  - Corre como una API independiente basada en ASP.NET Core.
  - Documentado mediante Swagger

- **Frontend SPA**: `Figuritas.Client`
  - Es una aplicación Blazor WebAssembly que se ejecuta en el navegador.
  - Es una SPA de renderizado del lado del cliente (client-side rendering).
  - Se entrega como archivos estáticos y se sirve con `nginx` que además hace reverse proxy para redirigir peticiones del cliente a la API REST `Figuritas.Api`.

- **Código compartido**: `Figuritas.Shared`
  - Contiene los modelos y tipos usados por la API y por el cliente.

## Ejecución con Docker Compose

Para levantar el sistema se usa `docker-compose`.

- El backend se expone en el puerto **5000** del equipo. La URL raíz redirige a la documentación swagger.
- El frontend se expone en el puerto **5001** del equipo.

### Comando

```bash
docker-compose up --build
```

Después de iniciar, la aplicación se puede usar desde:

- Backend API: `http://localhost:5000`
- Frontend SPA: `http://localhost:5001`

Si fuera necesario moodificar los puertos de ejecución: editar `docker-compose.yml` en la raíz de proyecto. Modificar el primer valor de cada propiedad `ports` de cada aplicación según sea necesario. NO modificar el segundo valor que indica el puerto interno del contenedor docker porque hará que deje de funcionar las peticiones del cliente a la API REST.



### Ejecución de pruebas
1. Posicionarse en la carpeta Figuritas.Api.Tests.
2. Correr el siguiente comando para ejecutar las pruebas unitarias y de integración.
    ```bash
    docker-compose up --build
    ```


### Uso de Inteligencia Artificial
 - Utilizamos GitHub Copilot para generar estructuras iniciales de tests y algunos fragmentos de código pero toda la lógica fue revisada e implementada por el equipo.
 - Utilizamos GitHub Copilot para realizar refactorizaciones importantes a la solución que de forma manual hubieran llevado mucho más tiempo y complicaciones.
 - Utilizamos Gemini para consultas generales de arquitectura, ecosistema .NET, sintaxis de C#, buenas prácticas y organización de los proyectos.
 - Utilizamos Gemini para generar porciones de código a modo de plantilla para introducirnos a conceptos y al lenguaje que los integrantes del equipo utilizamos por primera vez.


## Decisiones de diseño
 - Como equipo, decidimos implementar la solución utilizando el ecosistema .NET por interés particular y porque tiene todas las herramientas necesarias para cumplir con todos los requisitos del trabajo práctico utilizando un único lenguaje de programación: C#.
  - Utilizamos ASP.NET Core 10 como framework principal para implementar el backend (API REST)
  - Utilizamos Blazor WebAssembly (WASM) como framework principal para implementar el frontend. Blazor permite ejecutar código C# directamente en el navegador del cliente a través de un entorno de ejecución binario. Este framework incorpora la sintaxis Razor que permite combinar HTML y C# para crear componentes dinámicos e interactivos. **TODO: actualmente el frontend utiliza bootstrap, pero posiblemente cambiemos a Tailwind**
 - Para servir la aplicación frontend (client-side SPA) incluimos `nginx` que también hace de proxy reverso para las consultas HTTP REST que deben dirigirse a la API backend.
 - Separamos la lógica de modelo en el proyecto Shared que puede ser utilizado por los proyectos Figuritas.Api (backend) y Figuritas.Client (frontend).
 - Aprovechamos el motor de Inyección de Dependencias nativo de .NET para gestionar el ciclo de vida de los servicios y el estado de la aplicación. Esto desacopla la lógica de negocio y evita problemas comunes de otros frameworks como el prop drilling en React, eliminando la necesidad de hooks complejos para el manejo de servicios globales.

