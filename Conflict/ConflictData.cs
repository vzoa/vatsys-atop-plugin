using AtopPlugin.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using vatsys;

namespace AtopPlugin.Conflict
{
    public class ConflictData
    {

        public ConflictData() { }
        public ConflictData(ConflictSegment buffer1, ConflictSegment buffer2, ConflictStatus status, ConflictType? type, DateTime earlystart, DateTime latestart, DateTime end, FDP2.FDR intruder, FDP2.FDR active, int latsep, double longdistact, 
            int? longdistsep, TimeSpan longtimeact, TimeSpan longtimesep, bool longtype, bool timelongcross, bool timelongsame, TimeOfPassing top, double trkangle, int vertsep, int vertact) 
        {
            FirstConflictTime = buffer1;
            FirstConflictTime2 = buffer2;
            ConflictStatus = status;
            ConflictType = type;
            EarliestLos = earlystart;
            LatestLos = latestart;
            ConflictEnd = end;
            Intruder = intruder;
            Active = active;
            LatSep = latsep;
            LongDistact = longdistact;
            LongDistsep = longdistsep;
            LongTimeact = longtimeact;
            LongTimesep = longtimesep;
            LongType = longtype;
            TimeLongcross = timelongcross;
            TimeLongsame = timelongsame;
            Top = top;
            TrkAngle = trkangle;
            VerticalSep = vertsep;
            VerticalAct = vertact;
        }

        public ConflictSegment FirstConflictTime { get; set; }
        public ConflictSegment FirstConflictTime2 { get; set; }
        public ConflictStatus ConflictStatus { get; set; }

        public ConflictType? ConflictType { get; set; }

        public bool DistLongsame { get; set; }

        public DateTime EarliestLos { get; set; }

        public DateTime LatestLos { get; set; }
        public DateTime ConflictEnd { get; set; }

        public FDP2.FDR Intruder { get; set; }

        public FDP2.FDR Active { get; set; }

        public int LatSep { get; set; }

        public double LongDistact { get; set; }

        public int? LongDistsep { get; set; }

        public TimeSpan LongTimeact { get; set; }

        public TimeSpan LongTimesep { get; set; }

        public bool LongType { get; set; }

        public bool TimeLongcross { get; set; }

        public bool TimeLongopposite { get; set; }

        public bool TimeLongsame { get; set; }

        public TimeOfPassing? Top { get; set; }

        public double TrkAngle { get; set; }

        public int VerticalAct { get; set; }

        public int VerticalSep { get; set; }

    }
}
