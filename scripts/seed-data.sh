#!/bin/bash
# Seed the LaCompta SQLite database with realistic multi-year game data
# for frontend development without launching the game.
# Usage: ./scripts/seed-data.sh [db_path]
#
# Generates 3 full years (12 seasons, 336 days) with realistic progression:
# - Year 1: Early game (parsnips, basic fishing, copper mines)
# - Year 2: Mid game (starfruit, artisan goods, deep mines)
# - Year 3: Late game (ancient fruit wine, iridium, legendary fish)

set -e

DB_PATH="${1:-$HOME/.steam/steam/steamapps/common/Stardew Valley/Mods/LaCompta/lacompta.db}"

echo "=== Seeding LaCompta DB (3 Years) ==="
echo "DB: $DB_PATH"

rm -f "$DB_PATH"

sqlite3 "$DB_PATH" <<'SCHEMA'
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
CREATE INDEX IF NOT EXISTS idx_daily_records_season_year ON daily_records(season, year, player_id);
CREATE INDEX IF NOT EXISTS idx_item_transactions_daily ON item_transactions(daily_record_id);
CREATE INDEX IF NOT EXISTS idx_fish_records_legendary ON fish_records(is_legendary);
SCHEMA

# Generate daily records with realistic progression using Python for better random data
python3 - "$DB_PATH" <<'PYTHON'
import sqlite3, sys, random
random.seed(42)

db = sqlite3.connect(sys.argv[1])
c = db.cursor()

PID = 'player1'
seasons = ['spring', 'summer', 'fall', 'winter']

# Income multipliers per year (early -> mid -> late game)
year_mult = {1: 1.0, 2: 3.5, 3: 8.0}

# Season base income profiles (farming, foraging, fishing, mining)
profiles = {
    'spring': {'farming': 1.0, 'foraging': 0.3, 'fishing': 0.4, 'mining': 0.3},
    'summer': {'farming': 1.5, 'foraging': 0.2, 'fishing': 0.5, 'mining': 0.5},
    'fall':   {'farming': 2.0, 'foraging': 0.4, 'fishing': 0.4, 'mining': 0.6},
    'winter': {'farming': 0.0, 'foraging': 0.2, 'fishing': 0.6, 'mining': 1.5},
}

# Day progression within season (ramp up as crops grow)
def day_curve(day, season):
    if season == 'winter':
        return 0.5 + 0.5 * (day / 28)  # steady in winter
    # Crops harvest in waves: days 5-8, 12-15, 19-22, 26-28
    base = 0.3 + 0.3 * (day / 28)
    if day in [5,6,7,8,12,13,14,15,19,20,21,22,26,27,28]:
        base += 0.8
    return base

record_id = 0
for year in [1, 2, 3]:
    ym = year_mult[year]
    for season in seasons:
        prof = profiles[season]
        daily_records = []
        for day in range(1, 29):
            dc = day_curve(day, season)
            noise = lambda: random.uniform(0.5, 1.5)

            base = 200 * ym * dc
            farming  = int(base * prof['farming'] * noise()) if prof['farming'] > 0 else 0
            foraging = int(base * prof['foraging'] * noise() * 0.5)
            fishing  = int(base * prof['fishing'] * noise() * 0.6)
            mining   = int(base * prof['mining'] * noise() * 0.7)
            other    = int(base * 0.1 * noise() * 0.3)

            # Expenses: buying seeds at season start, tool upgrades, misc
            expenses = 0
            if day <= 3:
                expenses = int(500 * ym * random.uniform(0.5, 1.5))
            elif day % 7 == 0:
                expenses = int(200 * ym * random.uniform(0.3, 1.0))

            c.execute('''INSERT INTO daily_records
                (season, year, day, farming_income, foraging_income, fishing_income, mining_income, other_income, total_expenses, player_id)
                VALUES (?,?,?,?,?,?,?,?,?,?)''',
                (season, year, day, farming, foraging, fishing, mining, other, expenses, PID))
            rid = c.lastrowid
            record_id = rid

            total = farming + foraging + fishing + mining + other
            daily_records.append((day, total, farming, foraging, fishing, mining, other, expenses, rid))

        # Season summary
        farm_tot = sum(r[2] for r in daily_records)
        fora_tot = sum(r[3] for r in daily_records)
        fish_tot = sum(r[4] for r in daily_records)
        mine_tot = sum(r[5] for r in daily_records)
        othr_tot = sum(r[6] for r in daily_records)
        exp_tot  = sum(r[7] for r in daily_records)
        best = max(daily_records, key=lambda r: r[1])

        c.execute('''INSERT INTO season_summaries
            (season, year, farming_total, foraging_total, fishing_total, mining_total, other_total, total_expenses, best_day, best_day_income, player_id)
            VALUES (?,?,?,?,?,?,?,?,?,?,?)''',
            (season, year, farm_tot, fora_tot, fish_tot, mine_tot, othr_tot, exp_tot, best[0], best[1], PID))

# Item transactions (representative items per season/year)
items_data = [
    # (season, year, item_name, item_id, category, qty, unit_price, cost_basis_per)
    # Year 1 - Early game
    ('spring', 1, 'Parsnip', '24', 'Farming', 40, 35, 20),
    ('spring', 1, 'Potato', '192', 'Farming', 25, 80, 50),
    ('spring', 1, 'Cauliflower', '190', 'Farming', 10, 175, 80),
    ('spring', 1, 'Daffodil', '18', 'Foraging', 15, 30, 0),
    ('spring', 1, 'Leek', '20', 'Foraging', 20, 60, 0),
    ('spring', 1, 'Largemouth Bass', '136', 'Fishing', 12, 100, 0),
    ('spring', 1, 'Catfish', '143', 'Fishing', 5, 200, 0),
    ('spring', 1, 'Copper Ore', '378', 'Mining', 80, 5, 0),
    ('spring', 1, 'Amethyst', '66', 'Mining', 8, 100, 0),
    ('summer', 1, 'Melon', '254', 'Farming', 30, 250, 80),
    ('summer', 1, 'Blueberry', '258', 'Farming', 120, 50, 80),
    ('summer', 1, 'Hot Pepper', '260', 'Farming', 45, 40, 40),
    ('summer', 1, 'Red Snapper', '150', 'Fishing', 15, 50, 0),
    ('summer', 1, 'Super Cucumber', '155', 'Fishing', 4, 250, 0),
    ('summer', 1, 'Gold Ore', '384', 'Mining', 60, 25, 0),
    ('summer', 1, 'Fire Quartz', '82', 'Mining', 10, 100, 0),
    ('fall', 1, 'Pumpkin', '276', 'Farming', 35, 320, 100),
    ('fall', 1, 'Cranberries', '282', 'Farming', 200, 75, 240),
    ('fall', 1, 'Grape', '398', 'Farming', 60, 80, 60),
    ('fall', 1, 'Wild Plum', '406', 'Foraging', 25, 80, 0),
    ('fall', 1, 'Walleye', '140', 'Fishing', 10, 105, 0),
    ('fall', 1, 'Iron Ore', '380', 'Mining', 100, 10, 0),
    ('fall', 1, 'Diamond', '72', 'Mining', 3, 750, 0),
    ('winter', 1, 'Crystal Fruit', '414', 'Foraging', 20, 150, 0),
    ('winter', 1, 'Snow Yam', '416', 'Foraging', 15, 100, 0),
    ('winter', 1, 'Sturgeon', '698', 'Fishing', 8, 200, 0),
    ('winter', 1, 'Lingcod', '707', 'Fishing', 6, 120, 0),
    ('winter', 1, 'Iridium Ore', '386', 'Mining', 30, 100, 0),
    ('winter', 1, 'Diamond', '72', 'Mining', 8, 750, 0),
    # Year 2 - Mid game (artisan goods, sprinklers, greenhouse)
    ('spring', 2, 'Strawberry', '400', 'Farming', 150, 120, 100),
    ('spring', 2, 'Cauliflower', '190', 'Farming', 60, 175, 80),
    ('spring', 2, 'Strawberry Wine', '348', 'Farming', 40, 360, 100),
    ('spring', 2, 'Ancient Fruit', '454', 'Farming', 10, 550, 0),
    ('spring', 2, 'Catfish', '143', 'Fishing', 15, 200, 0),
    ('spring', 2, 'Amethyst', '66', 'Mining', 20, 100, 0),
    ('summer', 2, 'Starfruit', '268', 'Farming', 80, 750, 400),
    ('summer', 2, 'Melon Wine', '348', 'Farming', 60, 750, 80),
    ('summer', 2, 'Blueberry', '258', 'Farming', 300, 50, 80),
    ('summer', 2, 'Ancient Fruit Wine', '348', 'Farming', 30, 1650, 0),
    ('summer', 2, 'Lava Eel', '162', 'Fishing', 5, 700, 0),
    ('summer', 2, 'Iridium Ore', '386', 'Mining', 80, 100, 0),
    ('fall', 2, 'Ancient Fruit', '454', 'Farming', 80, 550, 0),
    ('fall', 2, 'Cranberries', '282', 'Farming', 400, 75, 240),
    ('fall', 2, 'Pumpkin', '276', 'Farming', 80, 320, 100),
    ('fall', 2, 'Truffle', '430', 'Farming', 25, 625, 0),
    ('fall', 2, 'Truffle Oil', '432', 'Farming', 20, 1065, 0),
    ('fall', 2, 'Diamond', '72', 'Mining', 15, 750, 0),
    ('winter', 2, 'Ancient Fruit Wine', '348', 'Farming', 60, 1650, 0),
    ('winter', 2, 'Iridium Ore', '386', 'Mining', 150, 100, 0),
    ('winter', 2, 'Diamond', '72', 'Mining', 20, 750, 0),
    ('winter', 2, 'Sturgeon Roe', '812', 'Fishing', 30, 250, 0),
    # Year 3 - Late game (maximized production)
    ('spring', 3, 'Ancient Fruit Wine', '348', 'Farming', 120, 1650, 0),
    ('spring', 3, 'Strawberry', '400', 'Farming', 300, 120, 100),
    ('spring', 3, 'Iridium Sprinkler', '645', 'Other', 10, 1000, 0),
    ('spring', 3, 'Diamond', '72', 'Mining', 25, 750, 0),
    ('summer', 3, 'Starfruit Wine', '348', 'Farming', 150, 2250, 400),
    ('summer', 3, 'Ancient Fruit Wine', '348', 'Farming', 120, 1650, 0),
    ('summer', 3, 'Melon', '254', 'Farming', 200, 250, 80),
    ('summer', 3, 'Iridium Ore', '386', 'Mining', 200, 100, 0),
    ('fall', 3, 'Ancient Fruit Wine', '348', 'Farming', 150, 1650, 0),
    ('fall', 3, 'Pumpkin', '276', 'Farming', 200, 320, 100),
    ('fall', 3, 'Truffle Oil', '432', 'Farming', 60, 1065, 0),
    ('fall', 3, 'Cranberries', '282', 'Farming', 600, 75, 240),
    ('fall', 3, 'Diamond', '72', 'Mining', 30, 750, 0),
    ('winter', 3, 'Ancient Fruit Wine', '348', 'Farming', 180, 1650, 0),
    ('winter', 3, 'Iridium Ore', '386', 'Mining', 250, 100, 0),
    ('winter', 3, 'Diamond', '72', 'Mining', 35, 750, 0),
    ('winter', 3, 'Sturgeon Roe', '812', 'Fishing', 50, 250, 0),
]

for item in items_data:
    season, year, name, iid, cat, qty, price, cost = item
    # Find a matching daily record
    c.execute('SELECT id FROM daily_records WHERE season=? AND year=? AND day=14 AND player_id=?', (season, year, PID))
    row = c.fetchone()
    rid = row[0] if row else 1
    c.execute('''INSERT INTO item_transactions
        (daily_record_id, item_name, item_id, category, quantity, unit_price, total_price, cost_basis, season, year, day, player_id)
        VALUES (?,?,?,?,?,?,?,?,?,?,?,?)''',
        (rid, name, iid, cat, qty, price, qty * price, qty * cost, season, year, 14, PID))

# Fish records (spread across years, legendaries in specific seasons)
fish_data = [
    # Year 1
    ('Largemouth Bass', '136', 0, 25, 2500, 'spring', 1, 10),
    ('Catfish', '143', 0, 15, 3000, 'spring', 1, 16),
    ('Red Snapper', '150', 0, 20, 1000, 'summer', 1, 8),
    ('Super Cucumber', '155', 0, 8, 2000, 'summer', 1, 20),
    ('Walleye', '140', 0, 18, 1890, 'fall', 1, 12),
    ('Sturgeon', '698', 0, 12, 2400, 'winter', 1, 15),
    ('Lingcod', '707', 0, 10, 1200, 'winter', 1, 22),
    # Legendary Year 1
    ('Legend', '163', 1, 1, 5000, 'spring', 1, 25),
    ('Crimsonfish', '159', 1, 1, 7500, 'summer', 1, 18),
    # Year 2
    ('Largemouth Bass', '136', 0, 40, 4000, 'spring', 2, 8),
    ('Lava Eel', '162', 0, 10, 7000, 'summer', 2, 14),
    ('Midnight Carp', '269', 0, 15, 2250, 'fall', 2, 20),
    ('Ice Pip', '161', 0, 8, 4000, 'winter', 2, 10),
    # Legendary Year 2
    ('Angler', '160', 1, 1, 9000, 'fall', 2, 16),
    ('Glacierfish', '775', 1, 1, 10000, 'winter', 2, 24),
    ('Mutant Carp', '682', 1, 1, 10000, 'winter', 2, 26),
    # Year 3
    ('Octopus', '149', 0, 12, 1800, 'summer', 3, 6),
    ('Lava Eel', '162', 0, 20, 14000, 'summer', 3, 18),
    ('Midnight Carp', '269', 0, 25, 3750, 'fall', 3, 10),
    ('Sturgeon', '698', 0, 30, 6000, 'winter', 3, 12),
    # Legendary Year 3 (re-catchable in 1.6)
    ('Son of Crimsonfish', '898', 1, 1, 7500, 'summer', 3, 22),
    ('Ms. Angler', '899', 1, 1, 9000, 'fall', 3, 15),
    ('Legend II', '900', 1, 1, 12500, 'spring', 3, 27),
    ('Radioactive Carp', '901', 1, 1, 10000, 'winter', 3, 20),
    ('Glacierfish Jr.', '902', 1, 1, 10000, 'winter', 3, 25),
]

for f in fish_data:
    name, fid, legendary, qty, rev, season, year, day = f
    c.execute('''INSERT OR REPLACE INTO fish_records
        (fish_name, fish_id, is_legendary, quantity, total_revenue, season, year, day, player_id)
        VALUES (?,?,?,?,?,?,?,?,?)''',
        (name, fid, legendary, qty, rev, season, year, day, PID))

db.commit()
db.close()
print("  Generated data successfully!")
PYTHON

# Verify
echo ""
echo "=== Seed complete ==="
sqlite3 "$DB_PATH" "SELECT 'Daily records:', count(*) FROM daily_records;"
sqlite3 "$DB_PATH" "SELECT 'Season summaries:', count(*) FROM season_summaries;"
sqlite3 "$DB_PATH" "SELECT 'Item transactions:', count(*) FROM item_transactions;"
sqlite3 "$DB_PATH" "SELECT 'Fish records:', count(*) FROM fish_records;"
sqlite3 "$DB_PATH" "SELECT 'Legendary fish:', count(*) FROM fish_records WHERE is_legendary=1;"
echo ""
sqlite3 "$DB_PATH" "SELECT season, year, count(*) as days FROM daily_records GROUP BY season, year ORDER BY year, CASE season WHEN 'spring' THEN 1 WHEN 'summer' THEN 2 WHEN 'fall' THEN 3 WHEN 'winter' THEN 4 END;"
