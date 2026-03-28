#!/bin/bash
# Quick test cycle: kill game, rebuild, launch, tail log
# Usage:
#   ./scripts/test-mod.sh          # Launch and tail log
#   ./scripts/test-mod.sh --test   # Launch, wait for save load, run integration tests
#   ./scripts/test-mod.sh --log    # Just tail the latest log (game already running)
set -e

GAME_DIR="$HOME/.steam/steam/steamapps/common/Stardew Valley"
LOG_FILE="$HOME/.config/StardewValley/ErrorLogs/SMAPI-latest.txt"
MOD_DIR="$GAME_DIR/Mods/LaCompta"
FIFO="/tmp/smapi_input_$$"

# --log mode: just tail existing log
if [ "$1" = "--log" ]; then
    echo "=== Tailing LaCompta log ==="
    tail -f "$LOG_FILE" | grep --line-buffered -i "LaCompta"
    exit 0
fi

echo "=== LaCompta Test Cycle ==="

# 1. Kill any running Stardew Valley / SMAPI
echo "[1/5] Stopping game..."
pkill -f "StardewValley" 2>/dev/null || true
pkill -f "StardewModdingAPI" 2>/dev/null || true
sleep 1

# 2. Build
echo "[2/5] Building..."
source ~/.zshrc 2>/dev/null || true
cd /home/bcalvet/Work/LaCompta
dotnet build 2>&1 | grep -E "error|Error|Build succeeded|Build FAILED"

# 3. Verify deploy
echo "[3/5] Verifying deployment..."
if [ -f "$MOD_DIR/LaCompta.dll" ]; then
    echo "  LaCompta.dll deployed OK"
else
    echo "  ERROR: LaCompta.dll not found in $MOD_DIR"
    exit 1
fi

if [ -f "$MOD_DIR/libe_sqlite3.so" ]; then
    echo "  libe_sqlite3.so deployed OK"
else
    echo "  WARNING: libe_sqlite3.so missing, copying..."
    cp "LaCompta/bin/Debug/net6.0/libe_sqlite3.so" "$MOD_DIR/" 2>/dev/null || true
fi

# 4. Launch game via SMAPI
if [ "$1" = "--test" ]; then
    echo "[4/5] Launching SMAPI with stdin pipe for automated tests..."
    mkfifo "$FIFO" 2>/dev/null || true

    # Launch SMAPI reading from FIFO (keeps stdin open)
    (cat "$FIFO"; sleep infinity) | "$GAME_DIR/StardewModdingAPI" &
    GAME_PID=$!

    echo "[5/5] Waiting for save to load... (load a save manually, or it auto-loads last save)"
    echo "  Then tests will run automatically."
    echo "  Watching log for 'LaCompta initialized'..."

    # Wait for the mod to initialize (save loaded)
    while ! grep -q "LaCompta initialized" "$LOG_FILE" 2>/dev/null; do
        sleep 2
    done
    sleep 2  # Give a moment for world to stabilize

    echo "  Save loaded! Running integration tests..."
    echo "lacompta_test" > "$FIFO"

    # Wait for test results
    sleep 5
    echo ""
    echo "=== Test Results ==="
    grep -A 1 "LaCompta.*\(PASS\|FAIL\|Results\)" "$LOG_FILE" | tail -20

    # Cleanup
    rm -f "$FIFO"
    echo ""
    echo "Game still running. Type 'lacompta_status' in SMAPI console for DB stats."
else
    echo "[4/5] Launching Stardew Valley via SMAPI..."
    "$GAME_DIR/StardewModdingAPI" &
    GAME_PID=$!

    # 5. Tail log for LaCompta entries
    echo "[5/5] Tailing log (Ctrl+C to stop)..."
    sleep 3
    tail -f "$LOG_FILE" | grep --line-buffered -i "LaCompta"
fi
