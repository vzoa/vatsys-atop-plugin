using vatsys.Plugin;

namespace AuroraLabelItemsPlugin
{
    public static class CustomColors
    {
        public static readonly CustomColour NonRvsm = new CustomColour(242, 133, 0);
        public static readonly CustomColour SepFlags = new CustomColour(0, 196, 253);
        public static readonly CustomColour Pending = new CustomColour(46, 139, 87);
        public static readonly CustomColour NotCda = new CustomColour(100, 0, 100);
        public static readonly CustomColour EastboundColour = new CustomColour(240, 255, 255);
        public static readonly CustomColour WestboundColour = new CustomColour(240, 231, 140);
        public static readonly CustomColour NonRVSM = new CustomColour(242, 133, 0);
        public static readonly CustomColour Probe = new CustomColour(0, 255, 0);
        public static readonly CustomColour NotCDA = new CustomColour(100, 0, 100);
        public static readonly CustomColour Advisory = new CustomColour(255, 165, 0);
        public static readonly CustomColour Imminent = new CustomColour(255, 0, 0);
        public static readonly CustomColour SpecialConditionCode = new CustomColour(255, 255, 0);
        public static readonly CustomColour ApsBlue = new CustomColour(141, 182, 205);
    }
    
    public static class StripConstants
    {
        public const string LABEL_ITEM_ADSB_CPDLC = "AURORA_ADSB_CPDLC"; //field c(4)
        public const string STRIP_ITEM_CALLSIGN = "AURORA_CALLSIGN";
        public const string STRIP_ITEM_CTLSECTOR = "AURORA_CDA";
        public const string STRIP_ITEM_NXTSECTOR = "AURORA_NDA";
        public const string STRIP_ITEM_T10_FLAG = "AURORA_T10_FLAG";
        public const string STRIP_ITEM_MNT_FLAG = "AURORA_MNT_FLAG";
        public const string STRIP_ITEM_DIST_FLAG = "AURORA_DIST_FLAG";
        public const string STRIP_ITEM_RVSM_FLAG = "AURORA_RVSM_FLAG";
        public const string STRIP_ITEM_VMI = "AURORA_STRIP_VMI";
        public const string STRIP_ITEM_COMPLEX = "AURORA_STRIP_COMPLEX";
        public const string STRIP_ITEM_CLEARED_LEVEL = "AURORA_CLEARED_LEVEL";
        public const string STRIP_ITEM_REQUESTED_LEVEL = "AURORA_REQUESTED_LEVEL";
        public const string STRIP_ITEM_MAN_EST = "AURORA_MAN_EST";
        public const string STRIP_ITEM_POINT = "AURORA_POINT";
        public const string STRIP_ITEM_ROUTE = "AURORA_ROUTE_STRIP";
        public const string STRIP_ITEM_RADAR_IND = "AURORA_RADAR_IND";
        public const string STRIP_ITEM_ANNOT_IND = "AURORA_ANNOT_STRIP";
        public const string STRIP_ITEM_LATERAL_FLAG = "AURORA_LATERAL_FLAG";
        public const string STRIP_ITEM_RESTR = "AURORA_RESTR_STRIP";
        public const string STRIP_ITEM_CLRD_RTE = "AURORA_CLRD_RTE";
        public const string CPAR_ITEM_TYPE = "CPAR_TYP";
        public const string CPAR_ITEM_REQUIRED = "CPAR_REQUIRED";
        public const string CPAR_ITEM_INTRUDER = "CPAR_INT";
        public const string CPAR_ITEM_LOS = "CPAR_LOS";
        public const string CPAR_ITEM_ACTUAL = "CPAR_ACTUAL";
        public const string CPAR_ITEM_PASSING = "CPAR_PASSING";
        public const string CPAR_ITEM_CONF_SEG_START_1 = "CPAR_CONF_SEG_START_1";
        public const string CPAR_ITEM_CONF_SEG_START_2 = "CPAR_CONF_SEG_START_2";
        public const string CPAR_ITEM_CONF_SEG_END_1 = "CPAR_CONF_SEG_END_1";
        public const string CPAR_ITEM_CONF_SEG_END_2 = "CPAR_CONF_SEG_END_2";
        public const string CPAR_ITEM_STARTIME_1 = "CPAR_START_TIME_1";
        public const string CPAR_ITEM_STARTIME_2 = "CPAR_START_TIME_2";
        public const string CPAR_ITEM_ENDTIME_1 = "CPAR_END_TIME_1";
        public const string CPAR_ITEM_ENDTIME_2 = "CPAR_END_TIME_2";
        public const string CPAR_ITEM_AID_2 = "CPAR_AID_2";
        public const string CPAR_ITEM_TYP_2 = "CPAR_TYP_2";
        public const string CPAR_ITEM_SPD_2 = "CPAR_SPD_2";
        public const string CPAR_ITEM_ALT_2 = "CPAR_ALT_2";
    }

    public static class LabelConstants
    {
        public const string LABEL_ITEM_SELECT_HORI = "SELECT_HORI";
        public const string LABEL_ITEM_SELECT_VERT = "SELECT_VERT";
        public const string LABEL_ITEM_COMM_ICON = "AURORA_COMM_ICON"; //field a(2)
        public const string LABEL_ITEM_ADSB_CPDLC = "AURORA_ADSB_CPDLC"; //field c(4)
        public const string LABEL_ITEM_ADS_FLAGS = "AURORA_ADS_FLAGS"; //field c(4)
        public const string LABEL_ITEM_MNT_FLAGS = "AURORA_MNT_FLAGS"; //field c(4)
        public const string LABEL_ITEM_SCC = "AURORA_SCC"; //field d(5)
        public const string LABEL_ITEM_ANNOT_IND = "AURORA_ANNOT_IND"; //field e(1)
        public const string LABEL_ITEM_RESTR = "AURORA_RESTR"; //field f(1)
        public const string LABEL_ITEM_LEVEL = "AURORA_LEVEL"; //field g(3)
        public const string LABEL_ITEM_VMI = "AURORA_VMI"; //field h(1)
        public const string LABEL_ITEM_CLEARED_LEVEL = "AURORA_CLEARED_LEVEL"; //field i(7)
        public const string LABEL_ITEM_HANDOFF_IND = "AURORA_HO_IND"; //field j(4)
        public const string LABEL_ITEM_RADAR_IND = "AURORA_RADAR_IND"; //field k(1)
        public const string LABEL_ITEM_INHIBIT_IND = "AURORA_INHIBIT_IND"; //field l(1)
        public const string LABEL_ITEM_FILED_SPEED = "AURORA_FILEDSPEED"; //field m(4)
        public const string LABEL_ITEM_3DIGIT_GROUNDSPEED = "AURORA_GROUNDSPEED"; //field n(5)
        public const string LABEL_ITEM_DESTINATION = "AURORA_DESTINATION"; //field o(4)
    }
}