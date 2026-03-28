#!/usr/bin/env python3
"""
LaCompta Dev Server — standalone HTTP server for frontend development.
Serves the dashboard and mock API endpoints without launching Stardew Valley.

Usage:
    ./scripts/dev-server.py                # Seed + serve on port 5555
    ./scripts/dev-server.py --port 8080    # Custom port
    ./scripts/dev-server.py --no-seed      # Skip re-seeding, use existing DB
    ./scripts/dev-server.py --watch        # Auto-reload HTML on file change
"""

import http.server
import json
import os
import sqlite3
import sys
import argparse
import random
import threading
import time
from urllib.parse import urlparse, parse_qs
from pathlib import Path

PROJECT_ROOT = Path(__file__).parent.parent
ASSETS_DIR = PROJECT_ROOT / "LaCompta" / "Web" / "Assets"
DEV_DB_PATH = PROJECT_ROOT / "scripts" / "dev-lacompta.db"

# ─── Database seeding ───

def seed_database(db_path):
    """Generate 3 years of realistic game data."""
    random.seed(42)
    if db_path.exists():
        db_path.unlink()

    db = sqlite3.connect(str(db_path))
    c = db.cursor()

    c.executescript("""
        CREATE TABLE IF NOT EXISTS daily_records (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            season TEXT NOT NULL, year INTEGER NOT NULL, day INTEGER NOT NULL,
            farming_income INTEGER NOT NULL DEFAULT 0, foraging_income INTEGER NOT NULL DEFAULT 0,
            fishing_income INTEGER NOT NULL DEFAULT 0, mining_income INTEGER NOT NULL DEFAULT 0,
            other_income INTEGER NOT NULL DEFAULT 0, total_expenses INTEGER NOT NULL DEFAULT 0,
            player_id TEXT NOT NULL DEFAULT '', UNIQUE(season, year, day, player_id)
        );
        CREATE TABLE IF NOT EXISTS item_transactions (
            id INTEGER PRIMARY KEY AUTOINCREMENT, daily_record_id INTEGER NOT NULL,
            item_name TEXT NOT NULL, item_id TEXT NOT NULL, category TEXT NOT NULL,
            quantity INTEGER NOT NULL, unit_price INTEGER NOT NULL, total_price INTEGER NOT NULL,
            cost_basis INTEGER NOT NULL DEFAULT 0, season TEXT NOT NULL, year INTEGER NOT NULL,
            day INTEGER NOT NULL, player_id TEXT NOT NULL DEFAULT '',
            FOREIGN KEY (daily_record_id) REFERENCES daily_records(id)
        );
        CREATE TABLE IF NOT EXISTS season_summaries (
            id INTEGER PRIMARY KEY AUTOINCREMENT, season TEXT NOT NULL, year INTEGER NOT NULL,
            farming_total INTEGER NOT NULL DEFAULT 0, foraging_total INTEGER NOT NULL DEFAULT 0,
            fishing_total INTEGER NOT NULL DEFAULT 0, mining_total INTEGER NOT NULL DEFAULT 0,
            other_total INTEGER NOT NULL DEFAULT 0, total_expenses INTEGER NOT NULL DEFAULT 0,
            best_day INTEGER NOT NULL DEFAULT 0, best_day_income INTEGER NOT NULL DEFAULT 0,
            player_id TEXT NOT NULL DEFAULT '', UNIQUE(season, year, player_id)
        );
        CREATE TABLE IF NOT EXISTS fish_records (
            id INTEGER PRIMARY KEY AUTOINCREMENT, fish_name TEXT NOT NULL, fish_id TEXT NOT NULL,
            is_legendary INTEGER NOT NULL DEFAULT 0, quantity INTEGER NOT NULL DEFAULT 1,
            total_revenue INTEGER NOT NULL DEFAULT 0, season TEXT NOT NULL, year INTEGER NOT NULL,
            day INTEGER NOT NULL, player_id TEXT NOT NULL DEFAULT '',
            UNIQUE(fish_id, season, year, day, player_id)
        );
    """)

    PID = "player1"
    seasons_list = ["spring", "summer", "fall", "winter"]
    year_mult = {1: 1.0, 2: 3.5, 3: 8.0}
    profiles = {
        "spring": {"farming": 1.0, "foraging": 0.3, "fishing": 0.4, "mining": 0.3},
        "summer": {"farming": 1.5, "foraging": 0.2, "fishing": 0.5, "mining": 0.5},
        "fall":   {"farming": 2.0, "foraging": 0.4, "fishing": 0.4, "mining": 0.6},
        "winter": {"farming": 0.0, "foraging": 0.2, "fishing": 0.6, "mining": 1.5},
    }

    def day_curve(day, season):
        if season == "winter":
            return 0.5 + 0.5 * (day / 28)
        base = 0.3 + 0.3 * (day / 28)
        if day in [5,6,7,8,12,13,14,15,19,20,21,22,26,27,28]:
            base += 0.8
        return base

    for year in [1, 2, 3]:
        ym = year_mult[year]
        for season in seasons_list:
            prof = profiles[season]
            daily_records = []
            for day in range(1, 29):
                dc = day_curve(day, season)
                noise = lambda: random.uniform(0.5, 1.5)
                base = 200 * ym * dc
                farming = int(base * prof["farming"] * noise()) if prof["farming"] > 0 else 0
                foraging = int(base * prof["foraging"] * noise() * 0.5)
                fishing = int(base * prof["fishing"] * noise() * 0.6)
                mining = int(base * prof["mining"] * noise() * 0.7)
                other = int(base * 0.1 * noise() * 0.3)
                expenses = 0
                if day <= 3:
                    expenses = int(500 * ym * random.uniform(0.5, 1.5))
                elif day % 7 == 0:
                    expenses = int(200 * ym * random.uniform(0.3, 1.0))

                c.execute("""INSERT INTO daily_records
                    (season, year, day, farming_income, foraging_income, fishing_income, mining_income, other_income, total_expenses, player_id)
                    VALUES (?,?,?,?,?,?,?,?,?,?)""",
                    (season, year, day, farming, foraging, fishing, mining, other, expenses, PID))
                rid = c.lastrowid
                total = farming + foraging + fishing + mining + other
                daily_records.append((day, total, farming, foraging, fishing, mining, other, expenses, rid))

            farm_tot = sum(r[2] for r in daily_records)
            fora_tot = sum(r[3] for r in daily_records)
            fish_tot = sum(r[4] for r in daily_records)
            mine_tot = sum(r[5] for r in daily_records)
            othr_tot = sum(r[6] for r in daily_records)
            exp_tot = sum(r[7] for r in daily_records)
            best = max(daily_records, key=lambda r: r[1])

            c.execute("""INSERT INTO season_summaries
                (season, year, farming_total, foraging_total, fishing_total, mining_total, other_total, total_expenses, best_day, best_day_income, player_id)
                VALUES (?,?,?,?,?,?,?,?,?,?,?)""",
                (season, year, farm_tot, fora_tot, fish_tot, mine_tot, othr_tot, exp_tot, best[0], best[1], PID))

    # Item transactions
    items = [
        ("spring",1,"Parsnip","24","Farming",40,35,20), ("spring",1,"Potato","192","Farming",25,80,50),
        ("spring",1,"Cauliflower","190","Farming",10,175,80), ("spring",1,"Leek","20","Foraging",20,60,0),
        ("spring",1,"Largemouth Bass","136","Fishing",12,100,0), ("spring",1,"Amethyst","66","Mining",8,100,0),
        ("summer",1,"Melon","254","Farming",30,250,80), ("summer",1,"Blueberry","258","Farming",120,50,80),
        ("summer",1,"Red Snapper","150","Fishing",15,50,0), ("summer",1,"Gold Ore","384","Mining",60,25,0),
        ("fall",1,"Pumpkin","276","Farming",35,320,100), ("fall",1,"Cranberries","282","Farming",200,75,240),
        ("fall",1,"Diamond","72","Mining",3,750,0), ("fall",1,"Walleye","140","Fishing",10,105,0),
        ("winter",1,"Sturgeon","698","Fishing",8,200,0), ("winter",1,"Iridium Ore","386","Mining",30,100,0),
        ("winter",1,"Diamond","72","Mining",8,750,0),
        ("spring",2,"Strawberry","400","Farming",150,120,100), ("spring",2,"Ancient Fruit","454","Farming",10,550,0),
        ("summer",2,"Starfruit","268","Farming",80,750,400), ("summer",2,"Ancient Fruit Wine","348","Farming",30,1650,0),
        ("fall",2,"Truffle Oil","432","Farming",20,1065,0), ("fall",2,"Cranberries","282","Farming",400,75,240),
        ("winter",2,"Ancient Fruit Wine","348","Farming",60,1650,0), ("winter",2,"Diamond","72","Mining",20,750,0),
        ("spring",3,"Ancient Fruit Wine","348","Farming",120,1650,0), ("spring",3,"Diamond","72","Mining",25,750,0),
        ("summer",3,"Starfruit Wine","348","Farming",150,2250,400), ("summer",3,"Iridium Ore","386","Mining",200,100,0),
        ("fall",3,"Ancient Fruit Wine","348","Farming",150,1650,0), ("fall",3,"Truffle Oil","432","Farming",60,1065,0),
        ("winter",3,"Ancient Fruit Wine","348","Farming",180,1650,0), ("winter",3,"Diamond","72","Mining",35,750,0),
    ]
    for item in items:
        season, year, name, iid, cat, qty, price, cost = item
        c.execute("SELECT id FROM daily_records WHERE season=? AND year=? AND day=14 AND player_id=?", (season, year, PID))
        row = c.fetchone()
        rid = row[0] if row else 1
        c.execute("""INSERT INTO item_transactions
            (daily_record_id, item_name, item_id, category, quantity, unit_price, total_price, cost_basis, season, year, day, player_id)
            VALUES (?,?,?,?,?,?,?,?,?,?,?,?)""",
            (rid, name, iid, cat, qty, price, qty * price, qty * cost, season, year, 14, PID))

    # Fish records
    fish = [
        ("Largemouth Bass","136",0,25,2500,"spring",1,10), ("Catfish","143",0,15,3000,"spring",1,16),
        ("Legend","163",1,1,5000,"spring",1,25), ("Red Snapper","150",0,20,1000,"summer",1,8),
        ("Crimsonfish","159",1,1,7500,"summer",1,18), ("Walleye","140",0,18,1890,"fall",1,12),
        ("Angler","160",1,1,9000,"fall",2,16), ("Glacierfish","775",1,1,10000,"winter",2,24),
        ("Mutant Carp","682",1,1,10000,"winter",2,26), ("Sturgeon","698",0,12,2400,"winter",1,15),
        ("Lava Eel","162",0,20,14000,"summer",3,18),
        ("Son of Crimsonfish","898",1,1,7500,"summer",3,22), ("Ms. Angler","899",1,1,9000,"fall",3,15),
        ("Legend II","900",1,1,12500,"spring",3,27), ("Radioactive Carp","901",1,1,10000,"winter",3,20),
        ("Glacierfish Jr.","902",1,1,10000,"winter",3,25),
    ]
    for f in fish:
        name, fid, legendary, qty, rev, season, year, day = f
        c.execute("""INSERT OR REPLACE INTO fish_records
            (fish_name, fish_id, is_legendary, quantity, total_revenue, season, year, day, player_id)
            VALUES (?,?,?,?,?,?,?,?,?)""",
            (name, fid, legendary, qty, rev, season, year, day, PID))

    db.commit()
    db.close()
    print(f"  Seeded {db_path} with 3 years of data")


# ─── API handlers ───

def get_db():
    return sqlite3.connect(str(DEV_DB_PATH))

def rows_to_dicts(cursor, rows):
    cols = [d[0] for d in cursor.description]
    return [dict(zip(cols, row)) for row in rows]

def api_daily(params):
    season = params.get("season", ["spring"])[0]
    year = int(params.get("year", ["1"])[0])
    player_id = params.get("playerId", [""])[0]
    db = get_db()
    c = db.cursor()
    c.execute("""SELECT id, season, year, day, farming_income as farmingIncome, foraging_income as foragingIncome,
        fishing_income as fishingIncome, mining_income as miningIncome, other_income as otherIncome,
        total_expenses as totalExpenses,
        (farming_income + foraging_income + fishing_income + mining_income + other_income) as totalIncome,
        (farming_income + foraging_income + fishing_income + mining_income + other_income - total_expenses) as netProfit,
        player_id as playerId
        FROM daily_records WHERE season=? AND year=? AND (?='' OR player_id=?) ORDER BY day""",
        (season, year, player_id, player_id))
    return rows_to_dicts(c, c.fetchall())

def api_seasons(params):
    player_id = params.get("playerId", [""])[0]
    db = get_db()
    c = db.cursor()
    c.execute("""SELECT id, season, year, farming_total as farmingTotal, foraging_total as foragingTotal,
        fishing_total as fishingTotal, mining_total as miningTotal, other_total as otherTotal,
        total_expenses as totalExpenses, best_day as bestDay, best_day_income as bestDayIncome,
        player_id as playerId
        FROM season_summaries WHERE (?='' OR player_id=?)
        ORDER BY year, CASE season WHEN 'spring' THEN 1 WHEN 'summer' THEN 2 WHEN 'fall' THEN 3 WHEN 'winter' THEN 4 END""",
        (player_id, player_id))
    return rows_to_dicts(c, c.fetchall())

def api_profitability(params):
    season = params.get("season", ["spring"])[0]
    year = int(params.get("year", ["1"])[0])
    limit = int(params.get("limit", ["20"])[0])
    player_id = params.get("playerId", [""])[0]
    db = get_db()
    c = db.cursor()
    c.execute("""SELECT item_name as itemName, item_id as itemId, category, quantity,
        unit_price as unitPrice, total_price as totalPrice, cost_basis as costBasis
        FROM item_transactions
        WHERE season=? AND year=? AND (?='' OR player_id=?)
        ORDER BY (total_price - cost_basis) DESC LIMIT ?""",
        (season, year, player_id, player_id, limit))
    return rows_to_dicts(c, c.fetchall())

def api_fish_legendary(params):
    player_id = params.get("playerId", [""])[0]
    db = get_db()
    c = db.cursor()
    c.execute("""SELECT fish_name as fishName, fish_id as fishId, is_legendary as isLegendary,
        quantity, total_revenue as totalRevenue, season, year, day, player_id as playerId
        FROM fish_records WHERE is_legendary=1 AND (?='' OR player_id=?)
        ORDER BY year, day""", (player_id, player_id))
    return rows_to_dicts(c, c.fetchall())

def api_fish(params):
    player_id = params.get("playerId", [""])[0]
    db = get_db()
    c = db.cursor()
    c.execute("""SELECT fish_name as fishName, fish_id as fishId, is_legendary as isLegendary,
        quantity, total_revenue as totalRevenue, season, year, day, player_id as playerId
        FROM fish_records WHERE (?='' OR player_id=?)
        ORDER BY year, day""", (player_id, player_id))
    return rows_to_dicts(c, c.fetchall())

def api_summary(params):
    player_id = params.get("playerId", [""])[0]
    db = get_db()
    c = db.cursor()
    c.execute("""SELECT
        count(*) as totalSeasons,
        sum(farming_total + foraging_total + fishing_total + mining_total + other_total) as totalIncome,
        sum(total_expenses) as totalExpenses
        FROM season_summaries WHERE (?='' OR player_id=?)""", (player_id, player_id))
    row = c.fetchone()
    c.execute("SELECT count(*) FROM fish_records WHERE is_legendary=1 AND (?='' OR player_id=?)", (player_id, player_id))
    legendary = c.fetchone()[0]
    c.execute("""SELECT
        sum(farming_total) as farming, sum(foraging_total) as foraging,
        sum(fishing_total) as fishing, sum(mining_total) as mining, sum(other_total) as other
        FROM season_summaries WHERE (?='' OR player_id=?)""", (player_id, player_id))
    cats = c.fetchone()
    return {
        "totalSeasons": row[0] or 0,
        "totalIncome": row[1] or 0,
        "totalExpenses": row[2] or 0,
        "legendaryFishCount": legendary,
        "categories": {
            "farming": cats[0] or 0, "foraging": cats[1] or 0,
            "fishing": cats[2] or 0, "mining": cats[3] or 0, "other": cats[4] or 0
        }
    }

def api_farminfo():
    return {
        "farmName": "Dev Farm",
        "playerName": "Dev Player",
        "season": "winter",
        "year": 3,
        "day": 28
    }


# ─── HTTP Server ───

ROUTES = {
    "/api/daily": api_daily,
    "/api/seasons": api_seasons,
    "/api/profitability": api_profitability,
    "/api/fish/legendary": api_fish_legendary,
    "/api/fish": api_fish,
    "/api/summary": api_summary,
}

MIME_TYPES = {
    ".html": "text/html", ".css": "text/css", ".js": "application/javascript",
    ".png": "image/png", ".jpg": "image/jpeg", ".svg": "image/svg+xml",
    ".ico": "image/x-icon", ".json": "application/json",
}

class DevHandler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        parsed = urlparse(self.path)
        path = parsed.path
        params = parse_qs(parsed.query)

        # CORS
        self.send_header_cors = True

        # API routes
        if path == "/api/farminfo":
            self._json_response(api_farminfo())
            return

        if path in ROUTES:
            self._json_response(ROUTES[path](params))
            return

        # Static files
        if path == "/":
            path = "/dashboard.html"

        file_path = ASSETS_DIR / path.lstrip("/")
        if file_path.exists() and file_path.is_file():
            ext = file_path.suffix.lower()
            mime = MIME_TYPES.get(ext, "application/octet-stream")
            data = file_path.read_bytes()
            self.send_response(200)
            self.send_header("Content-Type", mime)
            self.send_header("Content-Length", str(len(data)))
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            self.wfile.write(data)
            return

        self._json_response({"error": "Not found"}, 404)

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()

    def _json_response(self, data, status=200):
        body = json.dumps(data, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format, *args):
        # Colorize API vs static
        path = args[0].split(" ")[1] if args else ""
        if "/api/" in path:
            print(f"  \033[36mAPI\033[0m  {args[0]}")
        else:
            print(f"  \033[90mGET\033[0m  {args[0]}")


def main():
    parser = argparse.ArgumentParser(description="LaCompta Dev Server")
    parser.add_argument("--port", type=int, default=5555, help="Port (default: 5555)")
    parser.add_argument("--no-seed", action="store_true", help="Skip re-seeding the database")
    args = parser.parse_args()

    print("=" * 50)
    print("  LaCompta Dev Server")
    print("=" * 50)

    if not args.no_seed:
        print("[1/2] Seeding database...")
        seed_database(DEV_DB_PATH)
    else:
        print("[1/2] Skipping seed (--no-seed)")
        if not DEV_DB_PATH.exists():
            print("  WARNING: No database found, seeding anyway...")
            seed_database(DEV_DB_PATH)

    print(f"[2/2] Starting server on port {args.port}...")
    print()
    print(f"  Dashboard:  http://localhost:{args.port}/")
    print(f"  API:        http://localhost:{args.port}/api/summary")
    print(f"  Assets:     {ASSETS_DIR}")
    print(f"  Database:   {DEV_DB_PATH}")
    print()
    print("  Press Ctrl+C to stop.")
    print("-" * 50)

    server = http.server.HTTPServer(("localhost", args.port), DevHandler)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n  Server stopped.")
        server.server_close()


if __name__ == "__main__":
    main()
