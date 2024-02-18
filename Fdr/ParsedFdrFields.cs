namespace AuroraLabelItemsPlugin.Fdr;

public record struct ParsedFdrFields(
    bool Rnp4,
    bool Rnp10,
    bool Cpdlc,
    bool Adsc,
    bool Pbcs
);