using System;
using System.Linq;
using AtopPlugin.Display.Label;
using AtopPlugin.Helpers;
using AtopPlugin.Models;
using AtopPlugin.State;
using AtopPlugin.UI;
using vatsys;
using vatsys.Plugin;

namespace AtopPlugin.Display;

public static class LabelItemRenderer
{
    public static CustomLabelItem? RenderLabelItem(string itemType, Track track, FDP2.FDR? fdr,
        RDP.RadarTrack _)
    {
        var renderedItem = RenderLabelItemDelegate(itemType, track, fdr);
        return renderedItem != null ? ExcludeConflictColor(fdr!, track, renderedItem) : null;
    }

    private static CustomLabelItem ExcludeConflictColor(FDP2.FDR fdr, Track track, CustomLabelItem customLabelItem)
    {
        // If we already overrode it, keep it that way
        if (customLabelItem.ForeColourIdentity == Colours.Identities.Custom) return customLabelItem;

        customLabelItem.ForeColourIdentity = Colours.Identities.Custom;
        customLabelItem.CustomForeColour = TrackColorRenderer.GetDirectionColour(fdr, track) ?? CustomColors.ApsBlue;

        return customLabelItem;
    }

    private static CustomLabelItem? RenderLabelItemDelegate(string itemType, Track track, FDP2.FDR? fdr)
    {
        if (fdr?.GetAtopState() == null || fdr.GetDisplayState() == null) return null;

        var atopState = fdr.GetAtopState()!;
        var displayState = fdr.GetDisplayState()!;

        return itemType switch
        {
            LabelConstants.LabelItemSelectHori => track.IsSelected()
                ? new CustomLabelItem { Text = Symbols.Empty, Border = BorderFlags.Top | BorderFlags.Bottom }
                : null,

            LabelConstants.LabelItemSelectVert => track.IsSelected()
                ? new CustomLabelItem { Text = Symbols.Empty, Border = BorderFlags.Left | BorderFlags.Right }
                : null,

            LabelConstants.LabelItemCommIcon => atopState.DownlinkIndicator || CpdlcPluginBridge.HasOpenDownlinks(fdr.Callsign)
                ? new CustomLabelItem { Text = Symbols.CommDownlink, Border = BorderFlags.All, OnMouseClick = HandleCommIconClick }
                : new CustomLabelItem { Text = Symbols.CommEmpty, OnMouseClick = HandleCommIconClick },

            LabelConstants.LabelItemAdsbCpdlc => RenderAdsbCpdlcLabelItem(fdr, displayState),

            LabelConstants.LabelItemAdsFlags => new CustomLabelItem { Text = displayState.AdsFlag },

            LabelConstants.LabelItemMntFlags => displayState.IsMntFlagToggled
                ? new CustomLabelItem { Text = Symbols.MntFlag }
                : null,

            LabelConstants.LabelItemScc => atopState.HighestSccFlag != null
                ? new CustomLabelItem
                {
                    Text = atopState.HighestSccFlag!.Value, ForeColourIdentity = Colours.Identities.Custom,
                    CustomForeColour = CustomColors.SpecialConditionCode
                }
                : null,

            LabelConstants.LabelItemAnnotInd => displayState.HasAnnotations
                ? new CustomLabelItem { Text = Symbols.ScratchpadFlag }
                : new CustomLabelItem { Text = Symbols.UntoggledFlag },

            LabelConstants.LabelItemRestr => displayState.IsRestrictionsIndicatorToggled
                ? new CustomLabelItem { Text = Symbols.RestrictionsFlag }
                : null,

            LabelConstants.LabelItemLevel => displayState.AltitudeColor == null
                ? new CustomLabelItem
                {
                    Text = displayState.CurrentLevel.PadLeft(3),
                    Border = displayState.AltitudeBorderFlags,
                    BorderColourIdentity = Colours.Identities.Custom,
                    CustomBorderColour = CustomColors.NotCda
                }
                : new CustomLabelItem
                {
                    Text = displayState.CurrentLevel.PadLeft(3),
                    Border = displayState.AltitudeBorderFlags,
                    BorderColourIdentity = Colours.Identities.Custom,
                    CustomBorderColour = CustomColors.NotCda,
                    ForeColourIdentity = Colours.Identities.Custom,
                    CustomForeColour = displayState.AltitudeColor
                },

            LabelConstants.LabelItemVmi => displayState.AltitudeColor == null
                ? new CustomLabelItem
                {
                    Text = (displayState.AltitudeFlag?.Value ?? "").PadLeft(1),
                    Border = displayState.AltitudeBorderFlags,
                    BorderColourIdentity = Colours.Identities.Custom,
                    CustomBorderColour = CustomColors.NotCda
                }
                : new CustomLabelItem
                {
                    Text = (displayState.AltitudeFlag?.Value ?? "").PadLeft(1),
                    Border = displayState.AltitudeBorderFlags,
                    BorderColourIdentity = Colours.Identities.Custom,
                    CustomBorderColour = CustomColors.NotCda,
                    ForeColourIdentity = Colours.Identities.Custom,
                    CustomForeColour = displayState.AltitudeColor
                },

            LabelConstants.LabelItemClearedLevel => displayState.AltitudeColor == null
                ? new CustomLabelItem
                {
                    Text = displayState.ClearedLevel.PadLeft(3),
                    Border = displayState.AltitudeBorderFlags,
                    BorderColourIdentity = Colours.Identities.Custom,
                    CustomBorderColour = CustomColors.NotCda
                }
                : new CustomLabelItem
                {
                    Text = displayState.ClearedLevel.PadLeft(3),
                    Border = displayState.AltitudeBorderFlags,
                    BorderColourIdentity = Colours.Identities.Custom,
                    CustomBorderColour = CustomColors.NotCda,
                    ForeColourIdentity = Colours.Identities.Custom,
                    CustomForeColour = displayState.AltitudeColor
                },

            LabelConstants.LabelItemRadarInd => atopState.RadarToggleIndicator
                ? new CustomLabelItem { Text = Symbols.RadarFlag, OnMouseClick = RadarFlagToggleHandler.Handle }
                : new CustomLabelItem { Text = Symbols.UntoggledFlag, OnMouseClick = RadarFlagToggleHandler.Handle },

            LabelConstants.LabelItemInhibitInd => fdr.State == FDP2.FDR.FDRStates.STATE_INHIBITED
                ? new CustomLabelItem { Text = Symbols.Inhibited }
                : null,

            LabelConstants.LabelItemFiledSpeed => new CustomLabelItem { Text = displayState.FiledSpeed },

            LabelConstants.LabelItem3DigitGroundspeed => new CustomLabelItem { Text = displayState.GroundSpeed },

            LabelConstants.LabelItemDestination => new CustomLabelItem { Text = fdr.DesAirport },

            _ => LabelItemRegistry.GetLabelItem(itemType)?.Render(fdr, displayState, atopState)
        };
    }

    private static void HandleCommIconClick(CustomLabelItemMouseClickEventArgs e)
    {
        try
        {
            if (e.Button != CustomLabelItemMouseButton.Left) return;

            var fdr = e.Track.GetFDR();
            var callsign = fdr?.Callsign;
            if (!string.IsNullOrEmpty(callsign) && fdr != null)
            {
                var downlink = CpdlcPluginBridge.GetOpenDownlinkDetails(callsign)
                    .OrderByDescending(d => d.Received)
                    .FirstOrDefault();

                if (IsAltitudeRequestDownlink(downlink))
                    AtopMenu.OpenAltitudeWindow(fdr, e.Track, openedFromCommIcon: true, replyDownlinkMessageId: downlink!.MessageId);
                else
                    AtopMenu.OpenClearanceWindow(callsign);
            }

            e.Handled = true;
        }
        catch (Exception ex)
        {
            Errors.Add(new Exception($"CommIconClick: {ex.Message}", ex));
        }
    }

    private static bool IsAltitudeRequestDownlink(AtopDownlinkInfo? downlink)
    {
        if (downlink == null || string.IsNullOrWhiteSpace(downlink.Content)) return false;

        var content = downlink.Content.ToUpperInvariant();
        var altitudeIndicators = new[]
        {
            "CLIMB",
            "DESCEND",
            "MAINTAIN",
            "ALTITUDE",
            "FLIGHT LEVEL",
            "FL",
            "BLOCK"
        };

        var speedIndicators = new[]
        {
            "MACH",
            "KNOT",
            "SPEED"
        };

        if (speedIndicators.Any(content.Contains) && !altitudeIndicators.Any(content.Contains))
            return false;

        return altitudeIndicators.Any(content.Contains);
    }

    private static CustomLabelItem? RenderAdsbCpdlcLabelItem(FDP2.FDR fdr, AtopAircraftDisplayState displayState)
    {
        if (!CpdlcPluginBridge.IsAvailable) return null;

        var connState = CpdlcPluginBridge.GetConnectionState(fdr.Callsign);
        if (connState is not (CpdlcPluginBridge.CpdlcConnectionState.CurrentDataAuthority or CpdlcPluginBridge.CpdlcConnectionState.NextDataAuthority))
            return null;

        if (connState == CpdlcPluginBridge.CpdlcConnectionState.NextDataAuthority)
        {
            return new CustomLabelItem
            {
                Text = displayState.CpdlcAdsbSymbol,
                ForeColourIdentity = Colours.Identities.Custom,
                CustomForeColour = CustomColors.NotCda
            };
        }

        return new CustomLabelItem { Text = displayState.CpdlcAdsbSymbol };
    }
}