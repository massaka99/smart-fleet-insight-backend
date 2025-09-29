import json
import math
import os
import random
import time
import uuid
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Dict, List, Optional, Tuple

import paho.mqtt.client as mqtt
import requests

Coordinate = Tuple[float, float]


WAYPOINTS = [
    {"name": "Copenhagen", "lat": 55.6761, "lon": 12.5683},
    {"name": "Frederiksberg", "lat": 55.6780, "lon": 12.5137},
    {"name": "Aarhus", "lat": 56.1629, "lon": 10.2039},
    {"name": "Odense", "lat": 55.4038, "lon": 10.4024},
    {"name": "Aalborg", "lat": 57.0488, "lon": 9.9217},
    {"name": "Esbjerg", "lat": 55.4767, "lon": 8.4594},
    {"name": "Randers", "lat": 56.4607, "lon": 10.0364},
    {"name": "Horsens", "lat": 55.8607, "lon": 9.8503},
    {"name": "Kolding", "lat": 55.4917, "lon": 9.4722},
    {"name": "Vejle", "lat": 55.7113, "lon": 9.5364},
    {"name": "Roskilde", "lat": 55.6419, "lon": 12.0804},
    {"name": "Helsingor", "lat": 56.0361, "lon": 12.6136},
    {"name": "Herning", "lat": 56.1363, "lon": 8.9742},
    {"name": "Holstebro", "lat": 56.3601, "lon": 8.6161},
    {"name": "Hjorring", "lat": 57.4642, "lon": 9.9823},
    {"name": "Silkeborg", "lat": 56.1697, "lon": 9.5451},
    {"name": "Sonderborg", "lat": 54.9089, "lon": 9.7895},
    {"name": "Naestved", "lat": 55.2299, "lon": 11.7609},
    {"name": "Viborg", "lat": 56.4510, "lon": 9.4020},
    {"name": "Svendborg", "lat": 55.0598, "lon": 10.6068},
    {"name": "Fredericia", "lat": 55.5656, "lon": 9.7526},
    {"name": "Slagelse", "lat": 55.4028, "lon": 11.3546},
    {"name": "Hillerod", "lat": 55.9270, "lon": 12.3007},
    {"name": "Glostrup", "lat": 55.6667, "lon": 12.4000},
    {"name": "Taastrup", "lat": 55.6517, "lon": 12.2922},
    {"name": "Ballerup", "lat": 55.7310, "lon": 12.3633},
    {"name": "Lyngby", "lat": 55.7704, "lon": 12.5038},
    {"name": "Greve", "lat": 55.5833, "lon": 12.3000},
    {"name": "Koge", "lat": 55.4580, "lon": 12.1821},
    {"name": "Holbaek", "lat": 55.7207, "lon": 11.7121},
]
@dataclass
class Config:
    mqtt_host: str
    mqtt_port: int
    mqtt_topic: str
    mqtt_username: Optional[str]
    mqtt_password: Optional[str]
    osrm_url: str
    vehicle_count: int
    publish_interval: float
    min_speed_kmh: float
    max_speed_kmh: float
    min_route_stops: int
    max_route_stops: int

    @classmethod
    def from_env(cls) -> "Config":
        min_speed = float(os.getenv("MIN_SPEED_KMH", "45.0"))
        max_speed = float(os.getenv("MAX_SPEED_KMH", "85.0"))
        if max_speed <= min_speed:
            max_speed = min_speed + 5.0
        raw_min_stops = int(os.getenv("ROUTE_MIN_STOPS", "2"))
        raw_max_stops = int(os.getenv("ROUTE_MAX_STOPS", "4"))
        max_waypoints = len(WAYPOINTS)
        min_stops = max(2, min(raw_min_stops, max_waypoints))
        max_stops = max(min_stops, min(raw_max_stops, max_waypoints))
        return cls(
            mqtt_host=os.getenv("MQTT_HOST", "mosquitto"),
            mqtt_port=int(os.getenv("MQTT_PORT", "1883")),
            mqtt_topic=os.getenv("MQTT_TOPIC", "fleet/telemetry"),
            mqtt_username=os.getenv("MQTT_USERNAME"),
            mqtt_password=os.getenv("MQTT_PASSWORD"),
            osrm_url=os.getenv("OSRM_URL", "http://osrm:5000"),
            vehicle_count=int(os.getenv("VEHICLE_COUNT", "3")),
            publish_interval=float(os.getenv("PUBLISH_INTERVAL_SECONDS", "1.0")),
            min_speed_kmh=min_speed,
            max_speed_kmh=max_speed,
            min_route_stops=min_stops,
            max_route_stops=max_stops,
        )


def haversine_m(lat1: float, lon1: float, lat2: float, lon2: float) -> float:
    """Return distance in metres between two WGS84 coordinates."""
    radius = 6_371_000.0
    phi1 = math.radians(lat1)
    phi2 = math.radians(lat2)
    d_phi = phi2 - phi1
    d_lambda = math.radians(lon2 - lon1)
    a = math.sin(d_phi / 2) ** 2 + math.cos(phi1) * math.cos(phi2) * math.sin(d_lambda / 2) ** 2
    c = 2 * math.atan2(math.sqrt(a), math.sqrt(1 - a))
    return radius * c


def bearing_deg(lat1: float, lon1: float, lat2: float, lon2: float) -> float:
    """Return bearing in degrees from point A to B."""
    phi1 = math.radians(lat1)
    phi2 = math.radians(lat2)
    d_lambda = math.radians(lon2 - lon1)
    y = math.sin(d_lambda) * math.cos(phi2)
    x = math.cos(phi1) * math.sin(phi2) - math.sin(phi1) * math.cos(phi2) * math.cos(d_lambda)
    bearing = math.degrees(math.atan2(y, x))
    return (bearing + 360.0) % 360.0


def utc_timestamp() -> str:
    """Return ISO8601 timestamp in UTC."""
    return datetime.now(timezone.utc).isoformat(timespec="seconds").replace("+00:00", "Z")


def choose_random_stops(config: Config) -> List[Dict[str, float]]:
    stop_count = random.randint(config.min_route_stops, config.max_route_stops)
    return random.sample(WAYPOINTS, stop_count)

def wait_for_osrm(config: Config, timeout_seconds: float = 30.0) -> bool:
    base_url = config.osrm_url.rstrip("/")
    probe_url = f"{base_url}/route/v1/driving/12.5683,55.6761;12.5800,55.6800"
    params = {"overview": "false", "steps": "false"}
    deadline = time.time() + timeout_seconds
    attempt = 0
    while time.time() < deadline:
        attempt += 1
        try:
            response = requests.get(probe_url, params=params, timeout=5)
            response.raise_for_status()
            data = response.json()
            if data.get("code") == "Ok":
                return True
        except requests.RequestException as exc:
            print(f"OSRM readiness check attempt {attempt} failed: {exc}", flush=True)
        time.sleep(1.0)
    return False


def fetch_route(osrm_url: str, stops: List[Dict[str, float]]) -> Dict[str, object]:
    if len(stops) < 2:
        raise ValueError("OSRM route requires at least two stops")
    base_url = osrm_url.rstrip("/")
    coordinates = ";".join(f"{stop['lon']},{stop['lat']}" for stop in stops)
    params = {
        "overview": "full",
        "geometries": "geojson",
        "steps": "true",
        "annotations": "duration,distance",
    }
    url = f"{base_url}/route/v1/driving/{coordinates}"
    response = requests.get(url, params=params, timeout=20)
    response.raise_for_status()
    data = response.json()
    if data.get("code") != "Ok" or not data.get("routes"):
        raise RuntimeError(f"OSRM route error: {data.get('message', 'no routes returned')}")
    route = data["routes"][0]
    geometry = route.get("geometry", {})
    coords = [(pt[1], pt[0]) for pt in geometry.get("coordinates", [])]
    if len(coords) < 2:
        raise RuntimeError("OSRM returned too few coordinates for a route")
    distance_m = float(route.get("distance", 0.0))
    duration_s = float(route.get("duration", 0.0))
    return {
        "coordinates": coords,
        "distance_m": distance_m,
        "duration_s": duration_s,
        "summary": " -> ".join(stop["name"] for stop in stops),
        "stops": stops,
    }


def build_track(
    route_coords: List[Coordinate],
    base_speed_kmh: float,
    publish_interval: float,
    osrm_distance_m: float,
) -> List[Dict[str, float]]:
    if len(route_coords) < 2:
        raise ValueError("Route must contain at least two coordinates")
    distances: List[float] = []
    computed_total = 0.0
    for idx in range(len(route_coords) - 1):
        lat1, lon1 = route_coords[idx]
        lat2, lon2 = route_coords[idx + 1]
        segment_distance = haversine_m(lat1, lon1, lat2, lon2)
        distances.append(segment_distance)
        computed_total += segment_distance
    if computed_total == 0.0:
        raise RuntimeError("Route has zero length after processing")
    total_distance = osrm_distance_m if osrm_distance_m > 0 else computed_total
    scale = total_distance / computed_total
    scaled_distances = [distance * scale for distance in distances]
    base_speed_m_s = max(1.0, base_speed_kmh * 1000.0 / 3600.0)
    track: List[Dict[str, float]] = []
    cumulative_distance = 0.0
    for idx in range(len(route_coords) - 1):
        start_lat, start_lon = route_coords[idx]
        end_lat, end_lon = route_coords[idx + 1]
        segment_distance = scaled_distances[idx]
        if segment_distance == 0.0:
            continue
        steps = max(1, math.ceil(segment_distance / (base_speed_m_s * publish_interval)))
        step_distance = segment_distance / steps
        bearing = bearing_deg(start_lat, start_lon, end_lat, end_lon)
        for step_index in range(steps):
            ratio = (step_index + 1) / steps
            lat = start_lat + (end_lat - start_lat) * ratio
            lon = start_lon + (end_lon - start_lon) * ratio
            distance_travelled = cumulative_distance + step_distance * (step_index + 1)
            progress = min(1.0, distance_travelled / total_distance)
            distance_remaining = max(0.0, total_distance - distance_travelled)
            speed_m_s = step_distance / publish_interval
            step_speed_kmh = max(5.0, (speed_m_s * 3.6) + random.uniform(-3.5, 3.5))
            track.append(
                {
                    "lat": lat,
                    "lon": lon,
                    "bearing": bearing,
                    "speed_kmh": step_speed_kmh,
                    "distance_travelled_m": distance_travelled,
                    "distance_remaining_m": distance_remaining,
                    "progress": progress,
                }
            )
        cumulative_distance += segment_distance
    if not track:
        raise RuntimeError("Unable to generate track samples for route")
    track[-1].update(
        {
            "lat": route_coords[-1][0],
            "lon": route_coords[-1][1],
            "speed_kmh": 0.0,
            "distance_travelled_m": total_distance,
            "distance_remaining_m": 0.0,
            "progress": 1.0,
        }
    )
    total_steps = len(track)
    for idx, entry in enumerate(track):
        remaining_steps = total_steps - idx - 1
        entry["eta_seconds"] = remaining_steps * publish_interval
    return track


class VehicleSimulator:
    def __init__(self, vehicle_id: str, config: Config):
        self.vehicle_id = vehicle_id
        self.config = config
        self.publish_interval = config.publish_interval
        self.route_meta: Dict[str, object] = {}
        self.track: List[Dict[str, float]] = []
        self._cursor = 0
        self._fuel_depletion = random.uniform(30.0, 60.0)
        self.reset()

    def reset(self) -> None:
        attempts = 0
        while attempts < 5:
            stops = choose_random_stops(self.config)
            try:
                route = fetch_route(self.config.osrm_url, stops)
            except Exception as exc:  # noqa: BLE001 - log and retry
                attempts += 1
                print(f"[{self.vehicle_id}] Route fetch failed (attempt {attempts}): {exc}", flush=True)
                time.sleep(1)
                continue
            base_speed = random.uniform(self.config.min_speed_kmh, self.config.max_speed_kmh)
            try:
                track = build_track(
                    route_coords=route["coordinates"],
                    base_speed_kmh=base_speed,
                    publish_interval=self.publish_interval,
                    osrm_distance_m=route["distance_m"],
                )
            except Exception as exc:  # noqa: BLE001 - log and retry
                attempts += 1
                print(f"[{self.vehicle_id}] Track build failed (attempt {attempts}): {exc}", flush=True)
                time.sleep(1)
                continue
            self.route_meta = {
                "route_id": uuid.uuid4().hex[:8],
                "summary": route["summary"],
                "distance_m": route["distance_m"],
                "duration_s": len(track) * self.publish_interval,
                "base_speed_kmh": base_speed,
                "stops": route["stops"],
            }
            self.track = track
            self._cursor = 0
            self._fuel_depletion = random.uniform(30.0, 60.0)
            print(
                f"[{self.vehicle_id}] New route {self.route_meta['route_id']} ({self.route_meta['summary']}) "
                f"{self.route_meta['distance_m'] / 1000:.1f} km expected duration "
                f"{self.route_meta['duration_s'] / 60:.1f} min at {base_speed:.0f} km/h",
                flush=True,
            )
            return
        raise RuntimeError(f"{self.vehicle_id}: unable to prepare route after multiple attempts")

    def advance(self) -> Optional[Dict[str, object]]:
        if self._cursor >= len(self.track):
            return None
        sample = self.track[self._cursor]
        self._cursor += 1
        status = "arrived" if self._cursor >= len(self.track) else "en_route"
        progress = sample["progress"]
        payload = {
            "telemetry_id": uuid.uuid4().hex,
            "vehicle_id": self.vehicle_id,
            "route_id": self.route_meta["route_id"],
            "route_summary": self.route_meta["summary"],
            "route_distance_km": round(self.route_meta["distance_m"] / 1000.0, 3),
            "base_speed_kmh": round(self.route_meta["base_speed_kmh"], 1),
            "timestamp_utc": utc_timestamp(),
            "status": status,
            "position": {
                "lat": round(sample["lat"], 6),
                "lon": round(sample["lon"], 6),
            },
            "speed_kmh": round(sample["speed_kmh"], 1),
            "heading_deg": round(sample["bearing"], 1),
            "distance_travelled_m": round(sample["distance_travelled_m"], 1),
            "distance_remaining_m": round(sample["distance_remaining_m"], 1),
            "progress": round(progress, 4),
            "eta_seconds": round(sample["eta_seconds"], 1),
            "fuel_level_percent": round(max(5.0, 100.0 - self._fuel_depletion * progress), 1),
            "stops": self.route_meta["stops"],
        }
        return payload


def setup_mqtt_client(config: Config) -> mqtt.Client:
    client = mqtt.Client(client_id=f"simulator-{uuid.uuid4().hex[:8]}")
    if config.mqtt_username:
        client.username_pw_set(config.mqtt_username, config.mqtt_password)
    attempt = 0
    while attempt < 5:
        try:
            client.connect(config.mqtt_host, config.mqtt_port, keepalive=60)
            client.loop_start()
            return client
        except Exception as exc:  # noqa: BLE001 - log and retry
            attempt += 1
            print(
                f"MQTT connection attempt {attempt} to {config.mqtt_host}:{config.mqtt_port} failed: {exc}",
                flush=True,
            )
            time.sleep(2)
    raise RuntimeError("Unable to establish MQTT connection after multiple attempts")


def main() -> None:
    config = Config.from_env()
    seed_value = os.getenv("SIMULATOR_SEED")
    if seed_value:
        try:
            random.seed(int(seed_value))
        except ValueError:
            random.seed(seed_value)
    if not wait_for_osrm(config):
        print(f"OSRM service at {config.osrm_url} did not become ready; exiting.", flush=True)
        return
    try:
        client = setup_mqtt_client(config)
    except Exception as exc:  # noqa: BLE001 - already logged
        print(f"Failed to connect to MQTT broker: {exc}", flush=True)
        return
    vehicles: List[VehicleSimulator] = []
    for idx in range(config.vehicle_count):
        custom_id = os.getenv(f"VEHICLE_ID_{idx + 1}")
        vehicle_id = custom_id or f"VEH-{idx + 1:03d}"
        try:
            vehicles.append(VehicleSimulator(vehicle_id, config))
        except Exception as exc:  # noqa: BLE001 - log and skip
            print(f"Failed to initialise {vehicle_id}: {exc}", flush=True)
    if not vehicles:
        print("No vehicles initialised; exiting.", flush=True)
        client.loop_stop()
        client.disconnect()
        return
    print(
        f"Publishing telemetry for {len(vehicles)} vehicle(s) to topic '{config.mqtt_topic}' "
        f"on {config.mqtt_host}:{config.mqtt_port}",
        flush=True,
    )
    try:
        while True:
            loop_start = time.time()
            for vehicle in vehicles:
                payload = vehicle.advance()
                if payload is None:
                    vehicle.reset()
                    payload = vehicle.advance()
                    if payload is None:
                        continue
                message = json.dumps(payload, separators=(",", ":"), ensure_ascii=True)
                client.publish(config.mqtt_topic, payload=message, qos=0, retain=False)
                position = payload["position"]
                print(
                    f"[{vehicle.vehicle_id}] lat={position['lat']:.5f} lon={position['lon']:.5f} "
                    f"speed={payload['speed_kmh']:.1f}km/h fuel={payload['fuel_level_percent']:.1f}% "
                    f"progress={payload['progress'] * 100:.1f}%",
                    flush=True,
                )
            elapsed = time.time() - loop_start
            sleep_time = max(0.0, config.publish_interval - elapsed)
            if sleep_time > 0:
                time.sleep(sleep_time)
    except KeyboardInterrupt:
        print("Simulation interrupted by user.", flush=True)
    finally:
        client.loop_stop()
        client.disconnect()


if __name__ == "__main__":
    main()








