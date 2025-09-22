import time
import paho.mqtt.client as mqtt
import sys

broker_host = "172.17.0.2"
broker_port = 1883
topic = "test/topic"

client = mqtt.Client()

try:
    client.connect(broker_host, broker_port, 60)
    print(f"Connected to {broker_host}:{broker_port}", flush=True)

    while True:
        message = "hello mosquitto"
        client.publish(topic, message)
        print(f"Published: {message} to {topic}", flush=True)
        sys.stdout.flush()
        time.sleep(2)
except Exception as e:
    print(f"Error: {e}", flush=True)
    sys.stdout.flush()