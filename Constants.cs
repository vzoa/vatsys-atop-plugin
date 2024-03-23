using vatsys.Plugin;

namespace AtopPlugin;

public static class CustomColors
{
    public static readonly CustomColour SepFlags = new(0, 196, 253);
    public static readonly CustomColour Pending = new(46, 139, 87);
    public static readonly CustomColour EastboundColour = new(240, 255, 255);
    public static readonly CustomColour WestboundColour = new(240, 231, 140);
    public static readonly CustomColour NonRvsm = new(242, 133, 0);
    public static readonly CustomColour Probe = new(0, 255, 0);
    public static readonly CustomColour NotCda = new(211, 28, 111);
    public static readonly CustomColour Advisory = new(255, 165, 0);
    public static readonly CustomColour Imminent = new(255, 0, 0);
    public static readonly CustomColour SpecialConditionCode = new(255, 255, 0);
    public static readonly CustomColour ApsBlue = new(141, 182, 205);
}

public static class Symbols
{
    public const string Empty = "";

    public const string NoCpdlcNoAdsb = "⎕";
    public const string CpdlcNoAdsb = "⧆";
    public const string CpdlcAndAdsb = "*"; //✱

    public const string D30 = "3";
    public const string D50 = "D";
    public const string T10 = "M";
    public const string Mnt = "R";
    public const string Rvsm = "W";
    public const string L50 = "R";
    public const string L23 = "4";

    public const string CommDownlink = "▼";
    public const string CommEmpty = "⬜";

    public const string RadarFlag = "★";
    public const string UntoggledFlag = "◦";
    public const string ScratchpadFlag = "&";
    public const string MntFlag = "M";
    public const string ComplexFlag = "*";
    public const string RestrictionsFlag = "x";

    public const string Climbing = "↑";
    public const string Descending = "↓";
    public const string DeviatingAbove = "+";
    public const string DeviatingBelow = "-";

    public const string Inhibited = "^";
    public const string EmptyAnnotations = ".";

    public const string StripRouteItem = "F";
    public const string StripRadarIndicator = "A";
}

public static class StripConstants
{
    public const string LabelItemAdsbCpdlc = "AURORA_ADSB_CPDLC"; //field c(4)
    public const string StripItemCallsign = "AURORA_CALLSIGN";
    public const string StripItemCtlsector = "AURORA_CDA";
    public const string StripItemNxtsector = "AURORA_NDA";
    public const string StripItemT10Flag = "AURORA_T10_FLAG";
    public const string StripItemMntFlag = "AURORA_MNT_FLAG";
    public const string StripItemDistFlag = "AURORA_DIST_FLAG";
    public const string StripItemRvsmFlag = "AURORA_RVSM_FLAG";
    public const string StripItemVmi = "AURORA_STRIP_VMI";
    public const string StripItemComplex = "AURORA_STRIP_COMPLEX";
    public const string StripItemClearedLevel = "AURORA_CLEARED_LEVEL";
    public const string StripItemRequestedLevel = "AURORA_REQUESTED_LEVEL";
    public const string StripItemManEst = "AURORA_MAN_EST";
    public const string StripItemPoint = "AURORA_POINT";
    public const string StripItemRoute = "AURORA_ROUTE_STRIP";
    public const string StripItemRadarInd = "AURORA_RADAR_IND";
    public const string StripItemAnnotInd = "AURORA_ANNOT_STRIP";
    public const string StripItemLateralFlag = "AURORA_LATERAL_FLAG";
    public const string StripItemRestr = "AURORA_RESTR_STRIP";
    public const string StripItemClrdRte = "AURORA_CLRD_RTE";
    public const string CparItemType = "CPAR_TYP";
    public const string CparItemRequired = "CPAR_REQUIRED";
    public const string CparItemIntruder = "CPAR_INT";
    public const string CparItemLos = "CPAR_LOS";
    public const string CparItemActual = "CPAR_ACTUAL";
    public const string CparItemPassing = "CPAR_PASSING";
    public const string CparItemConfSegStart1 = "CPAR_CONF_SEG_START_1";
    public const string CparItemConfSegStart2 = "CPAR_CONF_SEG_START_2";
    public const string CparItemConfSegEnd1 = "CPAR_CONF_SEG_END_1";
    public const string CparItemConfSegEnd2 = "CPAR_CONF_SEG_END_2";
    public const string CparItemStartime1 = "CPAR_START_TIME_1";
    public const string CparItemStartime2 = "CPAR_START_TIME_2";
    public const string CparItemEndtime1 = "CPAR_END_TIME_1";
    public const string CparItemEndtime2 = "CPAR_END_TIME_2";
    public const string CparItemAid2 = "CPAR_AID_2";
    public const string CparItemTyp2 = "CPAR_TYP_2";
    public const string CparItemSpd2 = "CPAR_SPD_2";
    public const string CparItemAlt2 = "CPAR_ALT_2";
}

public static class LabelConstants
{
    public const string LabelItemSelectHori = "SELECT_HORI";
    public const string LabelItemSelectVert = "SELECT_VERT";
    public const string LabelItemCommIcon = "AURORA_COMM_ICON"; //field a(2)
    public const string LabelItemAdsbCpdlc = "AURORA_ADSB_CPDLC"; //field c(4)
    public const string LabelItemAdsFlags = "AURORA_ADS_FLAGS"; //field c(4)
    public const string LabelItemMntFlags = "AURORA_MNT_FLAGS"; //field c(4)
    public const string LabelItemScc = "AURORA_SCC"; //field d(5)
    public const string LabelItemAnnotInd = "AURORA_ANNOT_IND"; //field e(1)
    public const string LabelItemRestr = "AURORA_RESTR"; //field f(1)
    public const string LabelItemLevel = "AURORA_LEVEL"; //field g(3)
    public const string LabelItemVmi = "AURORA_VMI"; //field h(1)
    public const string LabelItemClearedLevel = "AURORA_CLEARED_LEVEL"; //field i(7)
    public const string LabelItemHandoffInd = "AURORA_HO_IND"; //field j(4)
    public const string LabelItemRadarInd = "AURORA_RADAR_IND"; //field k(1)
    public const string LabelItemInhibitInd = "AURORA_INHIBIT_IND"; //field l(1)
    public const string LabelItemFiledSpeed = "AURORA_FILEDSPEED"; //field m(4)
    public const string LabelItem3DigitGroundspeed = "AURORA_GROUNDSPEED"; //field n(5)
    public const string LabelItemDestination = "AURORA_DESTINATION"; //field o(4)
}