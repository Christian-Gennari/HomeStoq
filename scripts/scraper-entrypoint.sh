#!/bin/bash
set -e

# Clean up stale X lock files from previous runs (fixes "Server is already active" error)
rm -f /tmp/.X*-lock /tmp/.X11-unix/X*

echo "[INFO] Starting Xvfb on display :99 (1280x720x24)"
Xvfb :99 -screen 0 1280x720x24 -ac +extension GLX +render -noreset &
XVFB_PID=$!

# Wait for Xvfb to start
sleep 2

echo "[INFO] Starting x11vnc and noVNC (port 6080)"
# x11vnc allows remote viewing of the virtual display
x11vnc -display :99 -forever -nopw -listen localhost -xkb &

# noVNC provides a web interface for VNC
/usr/share/novnc/utils/novnc_proxy --vnc localhost:5900 --listen 6080 --web /usr/share/novnc > /dev/null 2>&1 &

echo "[INFO] Launching Google Keep Scraper worker..."
dotnet HomeStoq.Plugins.GoogleKeepScraper.dll

# Cleanup on exit
kill $XVFB_PID
