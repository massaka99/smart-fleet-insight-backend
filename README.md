# Fleet Insight – Backend

Backend service for Fleet Insight.

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

## MQTT Telemetry

Start the telemetry simulator stack so the Mosquitto broker is available before running the backend ingestion service. From the simulator project root, run:

```bash
docker compose up -d
```

**Connection details**

- Host (backend container): `host.docker.internal`
- Host (backend running directly on macOS): `localhost`
- TCP port: `1883`
- WebSocket port: `9001`
- Topic: `fleet/telemetry`
- QoS: `0`
- ClientId: use any unique value, e.g. `fleet-insight-ingestion`
- CleanSession: `true`
- Username / password: leave blank (anonymous access)
- TLS: disabled (plain MQTT over TCP)

**Sample .NET configuration**
```ini
# Backend running in Docker
Telemetry__Mqtt__Host=host.docker.internal
Telemetry__Mqtt__Port=1883
Telemetry__Mqtt__Topic=fleet/telemetry
Telemetry__Mqtt__Username=
Telemetry__Mqtt__Password=
Telemetry__Mqtt__ClientId=fleet-insight-ingestion
Telemetry__Mqtt__UseTls=false
Telemetry__Mqtt__CleanSession=true
Telemetry__Mqtt__QoS=0

# Replace host.docker.internal with localhost when running on the host machine.
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
