// Minimal type stubs for CI builds without Stardew Valley / SMAPI installed.
// Only provides enough types for LaCompta to compile.

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
    public class Item : System.IDisposable
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
    namespace GameData.Crops
    {
        public class CropData { public string HarvestItemId = ""; }
    }
}

namespace StardewModdingAPI
{
    public interface IModHelper
    {
        string DirectoryPath { get; }
        IModEvents Events { get; }
        ICommandHelper ConsoleCommands { get; }
        IModRegistry ModRegistry { get; }
        T ReadConfig<T>() where T : new();
        void WriteConfig<T>(T config);
    }
    public interface IMonitor { void Log(string msg, LogLevel level); }
    public interface IManifest { }
    public interface IModEvents { IGameLoopEvents GameLoop { get; } IMultiplayerEvents Multiplayer { get; } }
    public interface IGameLoopEvents
    {
        event EventHandler<Events.GameLaunchedEventArgs> GameLaunched;
        event EventHandler<Events.SaveLoadedEventArgs> SaveLoaded;
        event EventHandler<Events.DayStartedEventArgs> DayStarted;
        event EventHandler<Events.DayEndingEventArgs> DayEnding;
        event EventHandler<Events.ReturnedToTitleEventArgs> ReturnedToTitle;
    }
    public interface IMultiplayerEvents { }
    public interface ICommandHelper { void Add(string name, string doc, Action<string, string[]> callback); }
    public interface IModRegistry { T GetApi<T>(string modId) where T : class; }
    public enum LogLevel { Trace, Debug, Info, Warn, Error, Alert }
    public abstract class Mod
    {
        public IModHelper Helper { get; protected set; }
        public IMonitor Monitor { get; protected set; }
        public IManifest ModManifest { get; protected set; }
        public abstract void Entry(IModHelper helper);
    }
    public static class Context
    {
        public static bool IsWorldReady => false;
        public static bool IsMainPlayer => false;
        public static bool IsMultiplayer => false;
    }
    namespace Events
    {
        public class GameLaunchedEventArgs : EventArgs { }
        public class SaveLoadedEventArgs : EventArgs { }
        public class DayStartedEventArgs : EventArgs { }
        public class DayEndingEventArgs : EventArgs { }
        public class ReturnedToTitleEventArgs : EventArgs { }
    }
    namespace Utilities
    {
        public class PerScreen<T> { public T Value { get; set; } }
    }
}

namespace Microsoft.Xna.Framework
{
    public struct Rectangle { public Rectangle(int x, int y, int w, int h) { } }
    public struct Color
    {
        public static Color FromNonPremultiplied(int r, int g, int b, int a) => new();
    }
    namespace Graphics
    {
        public class Texture2D : System.IDisposable
        {
            public int Width => 0;
            public int Height => 0;
            public Texture2D(GraphicsDevice d, int w, int h) { }
            public void GetData<T>(int level, Rectangle? rect, T[] data, int start, int count) where T : struct { }
            public void SetData<T>(T[] data) where T : struct { }
            public void SaveAsPng(System.IO.Stream s, int w, int h) { }
            public void Dispose() { }
        }
        public class GraphicsDevice { }
    }
}
