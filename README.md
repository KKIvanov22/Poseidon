# Poseidon

## Run With Docker

From the `server` directory, the default compose file runs only the C# backend
and reads `server/.env` for Postgres, hosted RabbitMQ, CORS, Firebase, and port
settings:

```powershell
cd server
docker compose up --build
```

The API will be available at:

```text
http://localhost:8080
```

For the full local development stack with Docker Postgres, Docker RabbitMQ, and
Mailpit, use the dev compose file:

```powershell
cd server
docker compose -f docker-compose.dev.yml up --build
```

## API Documentation

Swagger UI is available at:

```text
http://localhost:8080/swagger
```

