using System;

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
