using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GameServerManager.Core.Models;

public class AppSettings : INotifyPropertyChanged
{
    private string _applicationName = "Game Server Manager";
    private string _language = "English (US)";
    private string _timeZone = "(UTC-05:00) Eastern Time (US & Canada)";
    private string _dateFormat = "MMM dd, yyyy";
    private string _theme = "Dark";
    private string _accentColor = "Blue";
    private string _density = "Comfortable";
    private int _sidebarWidth = 218;
    private int _cornerRadius = 12;
    private int _backgroundIntensity = 100;
    private bool _glassPanels = true;
    private bool _compactHeader;
    private string _startupBehavior = "Open Dashboard";
    private bool _autoSaveSettings = true;
    private bool _minimizeToTray = true;
    private bool _hardwareAcceleration = true;
    private bool _performanceMode;
    private string _loggingLevel = "Information";
    private int _defaultCpuAllocationPercent = 200;
    private int _defaultRamMb = 4096;
    private int _defaultDiskGb = 50;
    private string _defaultBackupSchedule = "Daily at 02:00";
    private string _defaultRestartPolicy = "Restart on Failure";
    private string _defaultIpBinding = "0.0.0.0";
    private int _portRangeStart = 25565;
    private int _portRangeEnd = 25650;
    private bool _reverseProxyEnabled = true;
    private string _sslTlsMode = "Let's Encrypt (Auto)";
    private int _connectionLimit = 1000;
    private bool _twoFactorRequired = true;
    private int _sessionTimeoutHours = 24;
    private int _loginProtectionAttempts = 5;
    private string _apiAccessControl = "Restricted";
    private string _encryptionMode = "AES-256-GCM";
    private bool _auditLoggingEnabled = true;
    private bool _curseForgeEnabled;
    private string _curseForgeApiKey = string.Empty;
    private int _curseForgeGameId;
    private int _curseForgeTimeoutSeconds = 10;
    private string _version = "3.0.0";
    private DateTime _lastModifiedUtc = DateTime.UtcNow;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string ApplicationName { get => _applicationName; set => SetProperty(ref _applicationName, value); }
    public string Language { get => _language; set => SetProperty(ref _language, value); }
    public string TimeZone { get => _timeZone; set => SetProperty(ref _timeZone, value); }
    public string DateFormat { get => _dateFormat; set => SetProperty(ref _dateFormat, value); }
    public string Theme { get => _theme; set => SetProperty(ref _theme, value); }
    public string AccentColor { get => _accentColor; set => SetProperty(ref _accentColor, value); }
    public string Density { get => _density; set => SetProperty(ref _density, value); }
    public int SidebarWidth { get => _sidebarWidth; set => SetProperty(ref _sidebarWidth, value); }
    public int CornerRadius { get => _cornerRadius; set => SetProperty(ref _cornerRadius, value); }
    public int BackgroundIntensity { get => _backgroundIntensity; set => SetProperty(ref _backgroundIntensity, value); }
    public bool GlassPanels { get => _glassPanels; set => SetProperty(ref _glassPanels, value); }
    public bool CompactHeader { get => _compactHeader; set => SetProperty(ref _compactHeader, value); }
    public string StartupBehavior { get => _startupBehavior; set => SetProperty(ref _startupBehavior, value); }

    public bool AutoSaveSettings { get => _autoSaveSettings; set => SetProperty(ref _autoSaveSettings, value); }
    public bool MinimizeToTray { get => _minimizeToTray; set => SetProperty(ref _minimizeToTray, value); }
    public bool HardwareAcceleration { get => _hardwareAcceleration; set => SetProperty(ref _hardwareAcceleration, value); }
    public bool PerformanceMode { get => _performanceMode; set => SetProperty(ref _performanceMode, value); }
    public string LoggingLevel { get => _loggingLevel; set => SetProperty(ref _loggingLevel, value); }

    public int DefaultCpuAllocationPercent { get => _defaultCpuAllocationPercent; set => SetProperty(ref _defaultCpuAllocationPercent, value); }
    public int DefaultRamMb { get => _defaultRamMb; set => SetProperty(ref _defaultRamMb, value); }
    public int DefaultDiskGb { get => _defaultDiskGb; set => SetProperty(ref _defaultDiskGb, value); }
    public string DefaultBackupSchedule { get => _defaultBackupSchedule; set => SetProperty(ref _defaultBackupSchedule, value); }
    public string DefaultRestartPolicy { get => _defaultRestartPolicy; set => SetProperty(ref _defaultRestartPolicy, value); }

    public string DefaultIpBinding { get => _defaultIpBinding; set => SetProperty(ref _defaultIpBinding, value); }
    public int PortRangeStart { get => _portRangeStart; set => SetProperty(ref _portRangeStart, value); }
    public int PortRangeEnd { get => _portRangeEnd; set => SetProperty(ref _portRangeEnd, value); }
    public bool ReverseProxyEnabled { get => _reverseProxyEnabled; set => SetProperty(ref _reverseProxyEnabled, value); }
    public string SslTlsMode { get => _sslTlsMode; set => SetProperty(ref _sslTlsMode, value); }
    public int ConnectionLimit { get => _connectionLimit; set => SetProperty(ref _connectionLimit, value); }

    public bool TwoFactorRequired { get => _twoFactorRequired; set => SetProperty(ref _twoFactorRequired, value); }
    public int SessionTimeoutHours { get => _sessionTimeoutHours; set => SetProperty(ref _sessionTimeoutHours, value); }
    public int LoginProtectionAttempts { get => _loginProtectionAttempts; set => SetProperty(ref _loginProtectionAttempts, value); }
    public string ApiAccessControl { get => _apiAccessControl; set => SetProperty(ref _apiAccessControl, value); }
    public string EncryptionMode { get => _encryptionMode; set => SetProperty(ref _encryptionMode, value); }
    public bool AuditLoggingEnabled { get => _auditLoggingEnabled; set => SetProperty(ref _auditLoggingEnabled, value); }

    public bool CurseForgeEnabled { get => _curseForgeEnabled; set => SetProperty(ref _curseForgeEnabled, value); }
    public string CurseForgeApiKey { get => _curseForgeApiKey; set => SetProperty(ref _curseForgeApiKey, value); }
    public int CurseForgeGameId { get => _curseForgeGameId; set => SetProperty(ref _curseForgeGameId, value); }
    public int CurseForgeTimeoutSeconds { get => _curseForgeTimeoutSeconds; set => SetProperty(ref _curseForgeTimeoutSeconds, value); }

    public string Version { get => _version; set => SetProperty(ref _version, value); }
    public DateTime LastModifiedUtc { get => _lastModifiedUtc; set => SetProperty(ref _lastModifiedUtc, value); }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
