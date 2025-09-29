# smart-fleet-insight-backend

Backend stack for the fleet simulator.

## Prepare OSRM data (one-time)

Run the helper script from the repo root:

```bash
python scripts/prepare_osrm.py
```

It downloads the latest Denmark extract and runs `osrm-extract`/`osrm-partition`/`osrm-customize` inside the official OSRM Docker image. The generated `.osrm*` files land in `simulator/osrm/` (git-ignored but required by the stack).

Use `--url <pbf-url>` to target another region, and `--force-download` if you need to refresh the PBF.

## Run the stack

1. Build and start everything from the repo root:
   ```bash
   docker compose up --build
   ```
2. Follow the simulator output if you want to see vehicles move:
   ```bash
   docker compose logs -f simulator
   ```
3. Need the raw MQTT feed? Subscribe from your host:
   ```bash
   mosquitto_sub -h localhost -t fleet/telemetry -v
   ```
4. Done for now? Bring everything down again:
   ```bash
   docker compose down
   ```

## Tweak the simulator

All knobs live in `docker-compose.yml` under the `simulator` service. Adjust environment variables such as:

- `VEHICLE_COUNT` ? how many vehicles run.
- `ROUTE_MIN_STOPS` / `ROUTE_MAX_STOPS` ? how many cities each route includes (set both to `2` for point-to-point trips).
- `PUBLISH_INTERVAL_SECONDS`, `MIN_SPEED_KMH`, `MAX_SPEED_KMH`, `SIMULATOR_SEED`, `VEHICLE_ID_*`, etc.

After changing the code or dependencies (`simulator/simulator.py`, `simulator/requirements.txt`), rebuild with:

```bash
docker compose build simulator
```

## Notes on OSRM data

Bigger map extracts take longer to process and need more RAM/disk. Denmark warms up in seconds; processing Scandinavia or the whole planet can take minutes and tens of gigabytes. For multiple countries you can either preprocess a larger extract once or run separate OSRM services per region.
