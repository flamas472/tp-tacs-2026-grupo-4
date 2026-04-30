# tp-tacs-2026-grupo-4

## Delivery 1

- We started implementing the TP using **ASP .NET Core**.
- We used Artificial Intelligence to create tests for each User Story.
- We created 1 controller per main API resource.
- FiguritasRepetidas have their own repo for a more performant global query than querying each User's figuritas.
- The API has auto-documentation of endpoints via Swagger (/swagger/index.html).
- The Figurita resource refers to the figurita as design or model, while FiguritaRepetida refers to the figurita that a User publishes on the page, the FiguritaRepetida has a Figurita as support, so two users can publish the same Figurita.

## How to Run the Application

### Locally
1. Ensure you have .NET 10 SDK installed.
2. Navigate to the FiguritasApi directory.
3. Run `dotnet restore` to restore dependencies.
4. Run `dotnet run` to start the application.
5. The application will be available at `http://localhost:5219` (HTTP) and `https://localhost:7268` (HTTPS).

### With Docker
1. Ensure you have Docker installed.
2. Run `docker-compose up --build` from the root directory.
3. The application will be available at `http://localhost:5000`.

### Ports
- Local: HTTP on 5219, HTTPS on 7268.
- Docker: Exposed on 5000 (mapped to internal 8080).

### Running Tests
1. Navigate to the FiguritasApi.Tests directory.
2. Run `dotnet test` to execute the unit and integration tests.

### AI Usage
We used AI (GitHub Copilot) to generate initial test structures and some code snippets, but all logic was reviewed and implemented by the team.
