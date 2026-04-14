# tp-tacs-2026-grupo-4

## Entrega 1:

-Empezamos la implementación del TP utilizando **ASP .NET Core**
-Utilizamos Inteligencia Artificial para crear los tests de cada User Story.
-Creamos 1 controller por cada recurso principal de la API
-Las FiguritasRepetidas tienen su propio repo para una consulta global más performante que ir consultando por cada Usuario sus figuritas
-La API tiene autodocumentación de los endpoints mediante Swagger (/swagger/index.html)
-El recurso Figurita refiere a la figurita como diseño o modelo, mientras que FiguritaRepetida refiere a la figurita que un Usuario publica en la página, la FiguritaRepetida tiene una Figurita como sustento, de esta forma, dos usuarios pueden publicar la misma Figurita.