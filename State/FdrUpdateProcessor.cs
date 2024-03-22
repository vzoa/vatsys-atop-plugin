using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AtopPlugin.Logic;
using vatsys;

namespace AtopPlugin.State;

public static class FdrUpdateProcessor
{
    private const int MillisUntilStartingProcessing = 5000;
    private const int MillisBetweenBatches = 250;
    
    private static readonly BatchCollectingQueue<FDP2.FDR> FdrUpdates = new();
    private static Timer _timer = new(ProcessUpdatesBatch);
    
    public static void ProcessFdrUpdate(FDP2.FDR updated)
    {
        FdrPropertyChangesListener.RegisterHandler(updated);
        FdrUpdates.Enqueue(updated);
    }

    public static void StartProcessing()
    {
        _timer = new Timer(ProcessUpdatesBatch);
        var result = _timer.Change(MillisUntilStartingProcessing, MillisBetweenBatches);
        if (!result) throw new Exception("Unable to start FDR processing timer");
    }

    public static void EndProcessing()
    {
        _timer.Dispose();
        FdrUpdates.DequeueAll();
    }

    private static void ProcessUpdatesBatch(object _)
    {
        var latestUpdatesPerAircraft = new Dictionary<string, FDP2.FDR>();
        
        FdrUpdates.DequeueAll().ForEach(fdr =>
        {
            latestUpdatesPerAircraft.Remove(fdr.Callsign);
            latestUpdatesPerAircraft.Add(fdr.Callsign, fdr);
        });
        
        latestUpdatesPerAircraft.Values.ToList().ForEach(ProcessSingleFdr);
    }

    private static void ProcessSingleFdr(FDP2.FDR updated)
    {
        AtopPluginStateManager.ProcessFdrUpdate(updated);
        AtopPluginStateManager.ProcessDisplayUpdate(updated);
        AtopPluginStateManager.RunConflictProbe(updated);

        // don't manage jurisdiction if not connected as ATC
        if (Network.Me.IsRealATC) JurisdictionManager.HandleFdrUpdate(updated);
    }
}