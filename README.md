# smart-fleet-insight-backend

Backend service for Smart Fleet Insight.

## Database

Start PostgreSQL locally with Docker:

```bash
docker compose -f deploy/docker-compose.yml up -d
```

## API

From `SmartFleet/SmartFleet`, restore dependencies and run the API:

```bash
dotnet restore
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```

## DBeaver connection

- Host: `localhost`
- Port: `5432`
- Database: `SmartFleet`
- User: `smartfleet`
- Password: `smartfleet_pw`

## Example request

```http
POST /api/vehicles
Content-Type: application/json

{
  "registrationPlate": "AB12345",
  "vehicleType": "Van",
  "fuelType": "Diesel",
  "bodyType": "Pickup",
  "kilometersDriven": 120000,
  "co2Emission": 165.5
}
```
