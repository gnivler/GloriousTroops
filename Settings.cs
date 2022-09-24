using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Settings.Base.Global;

// ReSharper disable UnusedAutoPropertyAccessor.Local
// ReSharper disable FieldCanBeMadeReadOnly.Local
// ReSharper disable ConvertToAutoProperty
// ReSharper disable InconsistentNaming    
// ReSharper disable PropertyCanBeMadeInitOnly.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local
// ReSharper disable PropertyCanBeMadeInitOnly.Local
// ReSharper disable UnusedMember.Global   
// ReSharper disable FieldCanBeMadeReadOnly.Global 
// ReSharper disable ConvertToConstant.Global

namespace UniqueTroopsGoneWild
{
    public class Settings : AttributeGlobalSettings<Settings>
    {
        public override string FormatType => "json";
        public override string FolderName => "UniqueTroopsGoneWild";
        private const string id = "UniqueTroopsGoneWild";
        private string displayName = $"UniqueTroopsGoneWild {typeof(Settings).Assembly.GetName().Version.ToString(3)}";
        public override string Id => id;
        public override string DisplayName => displayName;

        [SettingPropertyBool("Debug Logging", HintText = "Log to mod folder log.txt", Order = 1, RequireRestart = false)]
        public bool Debug { get; set; } = false;
        [SettingPropertyBool("Only Bandits", HintText = "Regular lord parties' troops will not upgrade their equipment.", Order = 0, RequireRestart = false)]
        public bool OnlyBandits { get; set; } = false;
    }
}
