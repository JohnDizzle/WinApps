namespace MyApps.Models;

public sealed record SettingLocation(string Store, IReadOnlyList<string> Containers, string Key);
public sealed record AppSetting(string Container, string Name, string Type, string Value, SettingLocation? Location = null);
public sealed record AppSettingsSnapshot(IReadOnlyList<AppSetting> Values, IReadOnlyList<string> Notices);
public sealed record SettingWriteResult(bool Saved, string Message);
public sealed record SettingDraft(AppSetting Original, string Value);
