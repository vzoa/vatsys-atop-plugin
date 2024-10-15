using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtopPlugin.Models;

public class ConflictFlag
{
    private ConflictFlag(string value)
    {
        Value = value;
    }

    public string Value { get; private set; }

    public static ConflictFlag ClimbingProfile => new(Symbols.ClimbingProfile);
    public static ConflictFlag DescendingProfile => new(Symbols.DescendingProfile);
    public static ConflictFlag LevelProfile => new(Symbols.LevelProfile);
    public static ConflictFlag SameDirection => new(Symbols.SameDirection);
    public static ConflictFlag OppositeDirection => new(Symbols.OppositeDirection);
    public static ConflictFlag Crossing => new(Symbols.Crossing);
}