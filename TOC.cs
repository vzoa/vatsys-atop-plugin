#region Assembly vatSys, Version=0.4.8114.34539, Culture=neutral, PublicKeyToken=null

// E:\vatsys\bin\vatSys.exe
// Decompiled with ICSharpCode.Decompiler 7.1.0.6543

#endregion

using System;
using System.Collections.Generic;
using System.Linq;

namespace vatsys
{
    public class TOC
    {
        public FDP2.FDR fdr;

        public SectorsVolumes.Sector nextSector;

        public List<SectorsVolumes.Sector> sectors;

        public TOC(FDP2.FDR fdr)
        {
            this.fdr = fdr;
            var segment = (from s in fdr.ParsedRoute.ToList()
                where s.Type == FDP2.FDR.ExtractedRoute.Segment.SegmentTypes.ZPOINT && fdr.ControllingSector !=
                    SectorsVolumes.FindSector((SectorsVolumes.Volume)s.Tag)
                select s).FirstOrDefault(s => s.ETO > DateTime.UtcNow);
            SectorsVolumes.Volume volume = null;
            if (segment != null) volume = (SectorsVolumes.Volume)segment.Tag;

            if (volume != null)
                nextSector = SectorsVolumes.FindSector(volume);
            else
                nextSector = null;

            if (nextSector != null)
            {
                SectorsVolumes.Sector sector = null;
                foreach (var s2 in SectorsVolumes.SectorGroupings.Keys)
                    if (s2.SubSectors.Contains(nextSector) &&
                        (sector == null || sector.SubSectors.Count > s2.SubSectors.Count))
                        sector = s2;
                if (sector != null) nextSector = sector;
            }
        }

        public void HandoffNextSector()
        {
            if (nextSector != null) MMI.HandoffJurisdiction(fdr, nextSector);
        }
    }
}