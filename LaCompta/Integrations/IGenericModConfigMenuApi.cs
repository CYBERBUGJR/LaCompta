using System;
using StardewModdingAPI;

namespace LaCompta.Integrations
{
    /// <summary>
    /// GMCM API interface — copied per standard SMAPI mod pattern.
    /// See: https://github.com/spacechase0/StardewValleyMods/tree/develop/GenericModConfigMenu
    /// </summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);
        void AddParagraph(IManifest mod, Func<string> text);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue,
            Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue,
            Func<string> name, Func<string> tooltip = null, int? min = null, int? max = null,
            int? interval = null, Func<int, string> formatValue = null, string fieldId = null);
        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue,
            Func<string> name, Func<string> tooltip = null, string[] allowedValues = null,
            Func<string, string> formatAllowedValue = null, string fieldId = null);
    }
}
