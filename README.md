# tp-tacs-2026-grupo-4

## Entrega 1:

- Empezamos la implementación del TP utilizando **ASP .NET Core**
- Utilizamos Inteligencia Artificial para crear el esqueleto de los tests de cada User Story y para popular datos de prueba.
- La estructura del backend es Controller-Model-Repository, Model es el primero en definirse, luego Repository, conectando Model con la infraestructura (instraestructura que solo concierne a la capa de repository), luego Controller se encarga de orquestar los repositorios e implementar operaciones para lograr los objetivos de cada endpoint.
- Utilizamos Data Transfer Objects para el input de los datos, estos son transformados a objetos de dominio a necesidad.
- Creamos 1 controller por cada recurso principal (independiente) de la API
- Intercambio define sus estados en base a qué fechas tiene grabadas, a su vez, Intercambio es un recurso independiente de sus 2 usuarios (proponente y propuesto)
- Las FiguritasRepetidas tienen su propio repo para una consulta global más performante que ir consultando por cada Usuario sus figuritas
- La API tiene autodocumentación de los endpoints mediante Swagger (/swagger/index.html)
- El recurso Figurita refiere a la figurita como diseño o modelo, mientras que FiguritaRepetida refiere a la figurita que un Usuario publica en la página, la FiguritaRepetida tiene una Figurita como sustento, de esta forma, dos usuarios pueden publicar la misma Figurita.
