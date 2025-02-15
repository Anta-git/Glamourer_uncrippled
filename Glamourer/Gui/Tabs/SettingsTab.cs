﻿using System;
using System.Runtime.CompilerServices;
using Dalamud.Interface;
using Glamourer.Gui.Tabs.DesignTab;
using Glamourer.Interop.Penumbra;
using Glamourer.Services;
using Glamourer.State;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;

namespace Glamourer.Gui.Tabs;

public class SettingsTab : ITab
{
    private readonly Configuration            _config;
    private readonly DesignFileSystemSelector _selector;
    private readonly StateListener            _stateListener;
    private readonly CodeService              _codeService;
    private readonly PenumbraAutoRedraw       _autoRedraw;

    public SettingsTab(Configuration config, DesignFileSystemSelector selector, StateListener stateListener,
        CodeService codeService, PenumbraAutoRedraw autoRedraw)
    {
        _config        = config;
        _selector      = selector;
        _stateListener = stateListener;
        _codeService   = codeService;
        _autoRedraw    = autoRedraw;
    }

    public ReadOnlySpan<byte> Label
        => "Settings"u8;

    private string _currentCode = string.Empty;

    public void DrawContent()
    {
        using var child = ImRaii.Child("MainWindowChild");
        if (!child)
            return;

        Checkbox("Enabled", "Enable main functionality of keeping and applying state.", _stateListener.Enabled, _stateListener.Enable);
        Checkbox("Enable Auto Designs", "Enable the application of designs associated to characters to be applied automatically.",
            _config.EnableAutoDesigns,  v => _config.EnableAutoDesigns = v);
        Checkbox("Skip Invalid Customizations",
            "Entirely skip customizations for any automatically applied design that does not have the same race, gender or face as the affected character currently has.",
            _config.SkipInvalidCustomizations, v => _config.SkipInvalidCustomizations = v);
        Checkbox("Restricted Gear Protection",
            "Use gender- and race-appropriate models when detecting certain items not available for a characters current gender and race.",
            _config.UseRestrictedGearProtection, v => _config.UseRestrictedGearProtection = v);
        Checkbox("Auto-Reload Gear",
            "Automatically reload equipment pieces on your own character when changing any mod options in Penumbra in their associated collection.",
            _config.AutoRedrawEquipOnChanges, _autoRedraw.SetState);

        Checkbox("Smaller Equip Display", "Use single-line display without icons and small dye buttons instead of double-line display.",
            _config.SmallEquip,           v => _config.SmallEquip = v);
        Checkbox("Hide Application Checkboxes",
            "Hide the application checkboxes in the Customization and Equipment panels of the design tab, and only show them under Application Rules.",
            _config.HideApplyCheckmarks, v => _config.HideApplyCheckmarks = v);
        if (Widget.DoubleModifierSelector("Design Deletion Modifier",
                "A modifier you need to hold while clicking the Delete Design button for it to take effect.", 100 * ImGuiHelpers.GlobalScale,
                _config.DeleteDesignModifier, v => _config.DeleteDesignModifier = v))
            _config.Save();
        DrawFolderSortType();
        Checkbox("Auto-Open Design Folders",
            "Have design folders open or closed as their default state after launching.", _config.OpenFoldersByDefault,
            v => _config.OpenFoldersByDefault = v);
        Checkbox("Debug Mode", "Show the debug tab. Only useful for debugging or advanced use.", _config.DebugMode, v => _config.DebugMode = v);
        DrawColorSettings();

        DrawCodes();

        MainWindow.DrawSupportButtons();
    }

    private void DrawCodes()
    {
        if (!ImGui.CollapsingHeader("Cheat Codes"))
            return;

        using (var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale, _currentCode.Length > 0))
        {
            var       color = _codeService.CheckCode(_currentCode) != null ? ColorId.ActorAvailable : ColorId.ActorUnavailable;
            using var c     = ImRaii.PushColor(ImGuiCol.Border, color.Value(), _currentCode.Length > 0);
            if (ImGui.InputTextWithHint("##Code", "Enter Cheat Code...", ref _currentCode, 512, ImGuiInputTextFlags.EnterReturnsTrue))
                if (_codeService.AddCode(_currentCode))
                    _currentCode = string.Empty;
        }

        if (_config.Codes.Count <= 0)
            return;

        for (var i = 0; i < _config.Codes.Count; ++i)
        {
            var (code, state) = _config.Codes[i];
            var action = _codeService.CheckCode(code);
            if (action == null)
                continue;

            if (ImGui.Checkbox(code, ref state))
            {
                action(state);
                _config.Codes[i] = (code, state);
                _config.Save();
            }
        }
    }

    /// <summary> Draw the entire Color subsection. </summary>
    private void DrawColorSettings()
    {
        if (!ImGui.CollapsingHeader("Colors"))
            return;

        foreach (var color in Enum.GetValues<ColorId>())
        {
            var (defaultColor, name, description) = color.Data();
            var currentColor = _config.Colors.TryGetValue(color, out var current) ? current : defaultColor;
            if (Widget.ColorPicker(name, description, currentColor, c => _config.Colors[color] = c, defaultColor))
                _config.Save();
        }

        ImGui.NewLine();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Checkbox(string label, string tooltip, bool current, Action<bool> setter)
    {
        using var id  = ImRaii.PushId(label);
        var       tmp = current;
        if (ImGui.Checkbox(string.Empty, ref tmp) && tmp != current)
        {
            setter(tmp);
            _config.Save();
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker(label, tooltip);
    }

    /// <summary> Different supported sort modes as a combo. </summary>
    private void DrawFolderSortType()
    {
        var sortMode = _config.SortMode;
        ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
        using (var combo = ImRaii.Combo("##sortMode", sortMode.Name))
        {
            if (combo)
                foreach (var val in Configuration.Constants.ValidSortModes)
                {
                    if (ImGui.Selectable(val.Name, val.GetType() == sortMode.GetType()) && val.GetType() != sortMode.GetType())
                    {
                        _config.SortMode = val;
                        _selector.SetFilterDirty();
                        _config.Save();
                    }

                    ImGuiUtil.HoverTooltip(val.Description);
                }
        }

        ImGuiUtil.LabeledHelpMarker("Sort Mode", "Choose the sort mode for the mod selector in the designs tab.");
    }
}
