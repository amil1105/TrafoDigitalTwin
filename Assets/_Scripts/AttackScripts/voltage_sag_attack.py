#!/usr/bin/env python3
import time
from datetime import datetime

import paho.mqtt.client as mqtt


BROKER_HOST = "localhost"
BROKER_PORT = 1883

MESSAGES_SAG = [
    ("substation/attack/type", "voltage_sag"),
    ("substation/busbar/voltage/a", "24.8"),
    ("substation/busbar/voltage/b", "31.6"),
    ("substation/busbar/voltage/c", "35.2"),
    ("substation/busbar/voltage_alarm", "SAG"),
]

MESSAGES_OVER = [
    ("substation/attack/type", "voltage_over"),
    ("substation/busbar/voltage/a", "39.8"),
    ("substation/busbar/voltage/b", "35.4"),
    ("substation/busbar/voltage/c", "34.8"),
    ("substation/busbar/voltage_alarm", "CRITICAL"),
]

MESSAGES_RESTORE = [
    ("substation/busbar/voltage/a", "34.5"),
    ("substation/busbar/voltage/b", "34.5"),
    ("substation/busbar/voltage/c", "34.5"),
    ("substation/busbar/voltage_alarm", "OFF"),
    ("substation/effect/smoke", "OFF"),
    ("substation/attack/type", "none"),
]


def print_menu() -> None:
    print()
    print("Busbar Voltage Sag / Imbalance Scenario")
    print("1 - Start Voltage Sag + Phase Imbalance")
    print("2 - Start Over Voltage + Phase Imbalance")
    print("3 - Stop Scenario / Restore Normal")
    print("q - Quit")


def publish_messages(messages: list[tuple[str, str]]) -> None:
    client = mqtt.Client(
        callback_api_version=mqtt.CallbackAPIVersion.VERSION2,
        client_id="voltage-sag-attack-console",
    )

    print(f"Connecting to MQTT broker {BROKER_HOST}:{BROKER_PORT}...")
    client.connect(BROKER_HOST, BROKER_PORT, keepalive=60)
    client.loop_start()

    timestamp = datetime.now().isoformat(timespec="seconds")
    for topic, payload in messages:
        result = client.publish(topic, payload, qos=0)
        result.wait_for_publish()
        print(f"[{timestamp}] Published {topic} = {payload}")
        time.sleep(0.05)

    client.loop_stop()
    client.disconnect()


def start_voltage_sag() -> None:
    print("[Voltage Sag] Starting sag + imbalance scenario...")
    publish_messages(MESSAGES_SAG)
    print("[Voltage Sag] Attack messages sent.")


def start_over_voltage() -> None:
    print("[Voltage Sag] Starting over voltage + imbalance scenario...")
    publish_messages(MESSAGES_OVER)
    print("[Voltage Sag] Attack messages sent.")


def restore_normal() -> None:
    print("[Voltage Sag] Restoring normal voltage state...")
    publish_messages(MESSAGES_RESTORE)
    print("[Voltage Sag] Restore messages sent.")


def main() -> None:
    while True:
        print_menu()
        choice = input("Select command type: ").strip().lower()

        if choice in {"q", "quit", "exit"}:
            print("Exiting voltage sag console.")
            break

        try:
            if choice == "1":
                start_voltage_sag()
            elif choice == "2":
                start_over_voltage()
            elif choice == "3":
                restore_normal()
            else:
                print("Invalid selection. Use 1, 2, 3, or q.")
        except Exception as exc:
            print(f"Publish failed: {exc}")


if __name__ == "__main__":
    main()
