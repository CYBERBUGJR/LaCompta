using System;
using System.Collections.Generic;

namespace StardewValley
{
    public class Game1
    {
        public static Farmer player;
        public static string currentSeason = "";
        public static int year = 0;
        public static int dayOfMonth = 0;
        public static GraphicsDeviceManager graphics;
        public static Farm getFarm() => null;
        public static IEnumerable<Farmer> getOnlineFarmers() => Array.Empty<Farmer>();
        public static IDictionary<string, ObjectData> objectData => new Dictionary<string, ObjectData>();
        public static IDictionary<string, GameData.Crops.CropData> cropData => new Dictionary<string, GameData.Crops.CropData>();
        public static Microsoft.Xna.Framework.Graphics.Texture2D objectSpriteSheet;
    }
    public class Farmer
    {
        public long UniqueMultiplayerID;
        public string Name = "";
        public NetString farmName = new();
        public int Money;
    }
    public class NetString { public string Value = ""; }
    public class Farm
    {
        public IList<Item> getShippingBin(Farmer f) => new List<Item>();
        public NetCollection terrainFeatures = new();
    }
    public class NetCollection
    {
        public IEnumerable<KeyValuePair<object, object>> Pairs => Array.Empty<KeyValuePair<object, object>>();
    }
    public class Item : IDisposable
    {
        public string ItemId = "";
        public int Category;
        public int Stack;
        public string DisplayName = "";
        public int salePrice() => 0;
        public void Dispose() { }
    }
    public class Object : Item
    {
        public int sellToStorePrice() => 0;
        public static int VegetableCategory = -75;
        public static int FruitsCategory = -79;
        public static int flowersCategory = -80;
        public static int FishCategory = -4;
    }
    public class ObjectData { public int Price; }
    public class ItemRegistry { public static Item Create(string id, int qty = 1) => new Item(); }
    public class GraphicsDeviceManager { public Microsoft.Xna.Framework.Graphics.GraphicsDevice GraphicsDevice; }
    public class NetFieldBase<T, TSelf> { public T Value; }
    namespace TerrainFeatures
    {
        public class HoeDirt { public NetFieldBase<string, object> fertilizer = new(); public object crop; }
    }
}
