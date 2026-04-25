// Stub assembly used ONLY for CI compilation. Not redistributed.
// Namespaces and member kinds (field vs property) MUST match the real SMAPI
// surface area used by LaCompta — otherwise the mod IL references types that
// don't exist in real StardewModdingAPI.dll and SMAPI flags it as
// "no longer compatible".

using System;

namespace StardewModdingAPI
{
    public interface IModHelper
    {
        string DirectoryPath { get; }
        Events.IModEvents Events { get; }
        ICommandHelper ConsoleCommands { get; }
        IModRegistry ModRegistry { get; }
        T ReadConfig<T>() where T : new();
        void WriteConfig<T>(T config);
    }
    public interface IMonitor { void Log(string msg, LogLevel level); }
    public interface IManifest { }
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
}

namespace StardewModdingAPI.Events
{
    public interface IModEvents
    {
        IGameLoopEvents GameLoop { get; }
        IMultiplayerEvents Multiplayer { get; }
    }
    public interface IGameLoopEvents
    {
        event EventHandler<GameLaunchedEventArgs> GameLaunched;
        event EventHandler<SaveLoadedEventArgs> SaveLoaded;
        event EventHandler<DayStartedEventArgs> DayStarted;
        event EventHandler<DayEndingEventArgs> DayEnding;
        event EventHandler<ReturnedToTitleEventArgs> ReturnedToTitle;
    }
    public interface IMultiplayerEvents { }
    public class GameLaunchedEventArgs : EventArgs { }
    public class SaveLoadedEventArgs : EventArgs { }
    public class DayStartedEventArgs : EventArgs { }
    public class DayEndingEventArgs : EventArgs { }
    public class ReturnedToTitleEventArgs : EventArgs { }
}

namespace StardewModdingAPI.Utilities
{
    public class PerScreen<T> { public T Value { get; set; } }
}
