import signal
import sys


def stop(number, frame):
    print("stopped", flush=True)
    sys.exit(0)


signal.signal(signal.SIGINT, stop)
signal.signal(signal.SIGTERM, stop)
print(sys.argv[1], flush=True)
print("diagnostic", file=sys.stderr, flush=True)
while True:
    signal.pause()
