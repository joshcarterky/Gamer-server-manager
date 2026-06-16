namespace GameServerManager.Core.Models;

/// <summary>
/// Defines a configurable server setting that can be edited by the user
/// </summary>
public class ServerSettingDefinition
{
    /// <summary>
    /// Unique identifier for this setting definition
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name/key of the setting (used as identifier)
    /// </summary>
    public string SettingKey { get; set; } = string.Empty;

    /// <summary>
    /// Display name shown in UI
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this setting does
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Type of input control for this setting
    /// </summary>
    public SettingControlType ControlType { get; set; } = SettingControlType.TextBox;

    /// <summary>
    /// Default value of the setting
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Current value (may differ from default)
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Minimum value for numeric settings
    /// </summary>
    public int? MinValue { get; set; }

    /// <summary>
    /// Maximum value for numeric settings
    /// </summary>
    public int? MaxValue { get; set; }

    /// <summary>
    /// Step increment for numeric settings
    /// </summary>
    public int? Step { get; set; } = 1;

    /// <summary>
    /// Available options for dropdown controls (key:display format)
    /// </summary>
    public List<string>? Options { get; set; }

    /// <summary>
    /// Whether this setting is required
    /// </summary>
    public bool IsRequired { get; set; } = false;

    /// <summary>
    /// Category/group this setting belongs to
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Whether the setting is hidden (advanced users only)
    /// </summary>
    public bool IsAdvanced { get; set; } = false;

    /// <summary>
    /// Regular expression pattern to validate the value
    /// </summary>
    public string? ValidationPattern { get; set; }

    /// <summary>
    /// Whether this setting requires a server restart
    /// </summary>
    public bool RequiresRestart { get; set; } = false;
}

/// <summary>
/// Types of input controls for settings
/// </summary>
public enum SettingControlType
{
    /// <summary>Text input field</summary>
    TextBox,

    /// <summary>Numeric input with min/max</summary>
    NumberBox,

    /// <summary>Toggle switch (true/false)</summary>
    Toggle,

    /// <summary>Dropdown selection</summary>
    Dropdown,

    /// <summary>Password field for sensitive data</summary>
    PasswordField,

    /// <summary>Folder picker dialog</summary>
    FolderPicker,

    /// <summary>File picker dialog</summary>
    FilePicker,

    /// <summary>List editor for adding/removing items</summary>
    ListEditor
}