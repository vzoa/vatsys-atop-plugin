namespace AtopPlugin.Models;

public record struct CalculatedFlightData(
    bool Rnp4,
    bool Rnp10,
    bool Cpdlc,
    bool Adsc,
    bool Pbcs
);