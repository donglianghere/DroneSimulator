# -*- coding: utf-8 -*-
import sys
import json
import serial
import time

def open_serial(port, baudrate, parity, stopbits, timeout=1):
    return serial.Serial(
        port=port,
        baudrate=baudrate,
        parity=parity,
        stopbits=stopbits,
        timeout=timeout
    )

def main():
    if len(sys.argv) < 2:
        print(json.dumps({"error": "No command"}))
        return

    cmd = sys.argv[1]
    if cmd == "send":
        # 参数: port baudrate parity stopbits data
        if len(sys.argv) < 7:
            print(json.dumps({"error": "Insufficient arguments"}))
            return
        port = sys.argv[2]
        baudrate = int(sys.argv[3])
        parity = sys.argv[4]
        stopbits = float(sys.argv[5])
        data = sys.argv[6]

        # parity转换
        parity_map = {
            "N": serial.PARITY_NONE,
            "E": serial.PARITY_EVEN,
            "O": serial.PARITY_ODD,
            "M": serial.PARITY_MARK,
            "S": serial.PARITY_SPACE
        }
        parity_val = parity_map.get(parity.upper(), serial.PARITY_NONE)

        try:
            with open_serial(port, baudrate, parity_val, stopbits) as ser:
                ser.write(data.encode("ascii"))
                time.sleep(0.1)
                if ser.in_waiting:
                    recv = ser.read(ser.in_waiting).decode("ascii", errors="ignore")
                else:
                    recv = ""
                print(json.dumps({"result": "ok", "recv": recv}))
        except Exception as e:
            print(json.dumps({"error": str(e)}))
    else:
        print(json.dumps({"error": "Unknown command"}))

if __name__ == "__main__":
    main()