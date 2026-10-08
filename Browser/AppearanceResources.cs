// SPDX-License-Identifier: GPL-3.0-only
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;

namespace MyApps.Browser;

internal static class AppearanceResources
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<FrameworkElement, object> Installed = new();

    internal static void Apply(FrameworkElement root)
    {
        if (Installed.TryGetValue(root, out _)) return;
        // Shared translucent surfaces let the root Mica/Acrylic material remain visible.
        // Native ThemeResource bindings update descendants without rebuilding their state.
        var dictionary = (ResourceDictionary)XamlReader.Load("""
            <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                <ResourceDictionary.ThemeDictionaries>
                    <ResourceDictionary x:Key="Light">
                        <SolidColorBrush x:Key="ControlFillColorDefaultBrush" Color="#20FFFFFF" />
                        <SolidColorBrush x:Key="ControlFillColorSecondaryBrush" Color="#50000000" Opacity="0.25" />
                        <SolidColorBrush x:Key="CardBackgroundFillColorDefaultBrush" Color="#40FFFFFF" />
                        <SolidColorBrush x:Key="LayerFillColorDefaultBrush" Color="#20FFFFFF" />
                        <SolidColorBrush x:Key="CardStrokeColorDefaultBrush" Color="#28000000" />
                        <SolidColorBrush x:Key="ToggleButtonBackgroundChecked" Color="#20000000" />
                        <SolidColorBrush x:Key="ToggleButtonBackgroundCheckedPointerOver" Color="#30000000" />
                        <SolidColorBrush x:Key="ToggleButtonBackgroundCheckedPressed" Color="#40000000" />
                        <SolidColorBrush x:Key="ToggleButtonForegroundChecked" Color="#FF181818" />
                        <SolidColorBrush x:Key="ToggleButtonForegroundCheckedPointerOver" Color="#FF181818" />
                        <SolidColorBrush x:Key="ToggleButtonForegroundCheckedPressed" Color="#FF181818" />
                    </ResourceDictionary>
                    <ResourceDictionary x:Key="Dark">
                        <SolidColorBrush x:Key="ControlFillColorDefaultBrush" Color="#0CFFFFFF" />
                        <SolidColorBrush x:Key="ControlFillColorSecondaryBrush" Color="#24FFFFFF" />
                        <SolidColorBrush x:Key="CardBackgroundFillColorDefaultBrush" Color="#0AFFFFFF" />
                        <SolidColorBrush x:Key="LayerFillColorDefaultBrush" Color="#08FFFFFF" />
                        <SolidColorBrush x:Key="CardStrokeColorDefaultBrush" Color="#28FFFFFF" />
                        <SolidColorBrush x:Key="ToggleButtonBackgroundChecked" Color="#24FFFFFF" />
                        <SolidColorBrush x:Key="ToggleButtonBackgroundCheckedPointerOver" Color="#30FFFFFF" />
                        <SolidColorBrush x:Key="ToggleButtonBackgroundCheckedPressed" Color="#40FFFFFF" />
                        <SolidColorBrush x:Key="ToggleButtonForegroundChecked" Color="#FFFFFFFF" />
                        <SolidColorBrush x:Key="ToggleButtonForegroundCheckedPointerOver" Color="#FFFFFFFF" />
                        <SolidColorBrush x:Key="ToggleButtonForegroundCheckedPressed" Color="#FFFFFFFF" />
                    </ResourceDictionary>
                    <ResourceDictionary x:Key="HighContrast">
                        <SolidColorBrush x:Key="ControlFillColorDefaultBrush" Color="{ThemeResource SystemColorButtonFaceColor}" />
                        <SolidColorBrush x:Key="ControlFillColorSecondaryBrush" Color="{ThemeResource SystemColorButtonFaceColor}" />
                        <SolidColorBrush x:Key="CardBackgroundFillColorDefaultBrush" Color="{ThemeResource SystemColorWindowColor}" />
                        <SolidColorBrush x:Key="LayerFillColorDefaultBrush" Color="{ThemeResource SystemColorWindowColor}" />
                        <SolidColorBrush x:Key="CardStrokeColorDefaultBrush" Color="{ThemeResource SystemColorWindowTextColor}" />
                        <SolidColorBrush x:Key="ToggleButtonBackgroundChecked" Color="{ThemeResource SystemColorHighlightColor}" />
                        <SolidColorBrush x:Key="ToggleButtonBackgroundCheckedPointerOver" Color="{ThemeResource SystemColorHighlightColor}" />
                        <SolidColorBrush x:Key="ToggleButtonBackgroundCheckedPressed" Color="{ThemeResource SystemColorHighlightColor}" />
                        <SolidColorBrush x:Key="ToggleButtonForegroundChecked" Color="{ThemeResource SystemColorHighlightTextColor}" />
                        <SolidColorBrush x:Key="ToggleButtonForegroundCheckedPointerOver" Color="{ThemeResource SystemColorHighlightTextColor}" />
                        <SolidColorBrush x:Key="ToggleButtonForegroundCheckedPressed" Color="{ThemeResource SystemColorHighlightTextColor}" />
                    </ResourceDictionary>
                </ResourceDictionary.ThemeDictionaries>
            </ResourceDictionary>
            """);
        root.Resources.MergedDictionaries.Add(dictionary);
        Installed.Add(root, new object());
    }
}
