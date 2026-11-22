namespace GrugBot.Core.Relaying;

/// <summary>How the runtime lookup of thebasics' Proximity chat group went (gotcha #15).</summary>
public enum TheBasicsLookupOutcome
{
    /// <summary>thebasics isn't installed or isn't enabled: General is plain General.</summary>
    NotInstalled,

    /// <summary>thebasics is running and its Proximity chat group ID was read.</summary>
    Found,

    /// <summary>thebasics seems to be there, but its Proximity chat group couldn't be read.</summary>
    Failed,
}
