namespace Dewiride.Erp.BuildingBlocks.Authentication.DataProtection;

public static class DataProtectionKeyRing
{
    // The Data Protection discriminator: every cookie and token cache entry is protected under it, so changing it makes all
    // of them unreadable. It is deliberately not Erp:Platform:Host:ApplicationName, whose display value differs per label.
    public const string ApplicationName = "Dewiride.Erp";

    // %LOCALAPPDATA%\ERP-AI-Pro\DataProtection-Keys on Windows and /home/app/.local/share/ERP-AI-Pro/DataProtection-Keys in
    // the container, never the framework's default folder, which every ASP.NET Core app of a Windows user shares. Without
    // DoNotVerify the folder path is empty until the folder exists, which would make this path relative.
    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), "ERP-AI-Pro", "DataProtection-Keys");
}
