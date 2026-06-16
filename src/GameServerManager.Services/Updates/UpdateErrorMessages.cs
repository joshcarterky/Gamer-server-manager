namespace GameServerManager.Services.Updates;

public static class UpdateErrorMessages
{
    public static string For(string code, string? detail = null)
    {
        var message = code switch
        {
            "NoInternet" => "The update check could not reach GitHub. Check your internet connection, then try again.",
            "GitHubUnreachable" => "GitHub Releases could not be reached. GitHub may be down, blocked, or rate limited.",
            "InvalidMetadata" => "The release metadata could not be read. The release may be missing valid update assets.",
            "DownloadFailed" => "The update download failed. Try again, or download the installer manually from GitHub Releases.",
            "ChecksumFailed" => "The update checksum did not match. Do not install this file; download it again from GitHub Releases.",
            "InstallFailed" => "The update could not be installed. Close the app, run as administrator if needed, and try again.",
            "RestartFailed" => "The update was prepared, but the app could not restart automatically. Start it manually from the Start Menu.",
            "PermissionDenied" => "The app does not have permission to replace files in the install folder. Run the app as administrator or use the installer.",
            "AntivirusBlocked" => "Security software may have blocked the updater. Restore the file if trusted, then try again.",
            "PortableUnsupported" => "Portable builds do not install updates automatically yet. Download the latest portable ZIP from GitHub Releases.",
            "UserCanceled" => "The update was canceled. No app or server files were changed.",
            "NoBetaReleases" => "The beta channel has no releases right now. Switch back to Stable or check later.",
            "NoDownloadableAsset" => "No Windows installer or portable ZIP was attached to this GitHub release. Upload a setup EXE, MSI, or portable ZIP and try again.",
            "UpdateCheckFailed" => "The update check failed. Try again, or open GitHub Releases manually.",
            _ => "The update check failed. Try again, or download the latest release manually from GitHub Releases."
        };

        return string.IsNullOrWhiteSpace(detail) ? message : $"{message} Detail: {detail}";
    }
}
