#!/bin/bash
# Standalone dev server for frontend development without launching the game.
# Seeds the DB and starts the mod's web server in a lightweight .NET host.
# Usage: ./scripts/dev-server.sh [port]

set -e

PORT="${1:-5555}"
DB_PATH="$HOME/.steam/steam/steamapps/common/Stardew Valley/Mods/LaCompta/lacompta.db"
PROJECT_DIR="/home/bcalvet/Work/LaCompta"

echo "=== LaCompta Dev Server ==="

# Seed data if DB doesn't exist or is empty
if [ ! -f "$DB_PATH" ] || [ "$(sqlite3 "$DB_PATH" 'SELECT count(*) FROM daily_records;' 2>/dev/null)" = "0" ]; then
    echo "[1/2] Seeding test data..."
    "$PROJECT_DIR/scripts/seed-data.sh" "$DB_PATH"
else
    echo "[1/2] DB already has data, skipping seed."
fi

# Start the game with SMAPI (only way to run the web server currently)
echo "[2/2] Starting game with SMAPI..."
echo "  Dashboard will be at http://localhost:$PORT"
echo "  Load a save to start the web server."
echo ""
"$HOME/.steam/steam/steamapps/common/Stardew Valley/StardewModdingAPI" &
GAME_PID=$!

echo "  Game PID: $GAME_PID"
echo "  Waiting for web server..."
sleep 5

# Poll until server responds
for i in $(seq 1 30); do
    if curl -s "http://localhost:$PORT/" > /dev/null 2>&1; then
        echo "  Server is up! Open http://localhost:$PORT/"
        echo "  Press Ctrl+C to stop."
        wait $GAME_PID
        exit 0
    fi
    sleep 2
done

echo "  Server didn't start within 60s. Load a save in the game."
wait $GAME_PID
