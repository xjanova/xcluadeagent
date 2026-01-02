namespace XcluadeAgent.Core.Enums;

/// <summary>
/// AI assistance mode levels for XcluadeAgent
/// </summary>
public enum AiMode
{
    /// <summary>
    /// AI is completely disabled
    /// </summary>
    Off = 0,

    /// <summary>
    /// Level 1: Alert Only - AI analyzes errors and sends smart notifications
    /// Capabilities: Error analysis, grouping, smart alerts
    /// Risk: Very Low
    /// Cost: Minimal (uses local AI when possible)
    /// </summary>
    Alert = 1,

    /// <summary>
    /// Level 2: Suggest Fix - AI analyzes and suggests solutions
    /// Capabilities: Error explanation, fix suggestions, code examples
    /// Risk: Low
    /// Cost: Low (may use cloud AI for complex analysis)
    /// </summary>
    Suggest = 2,

    /// <summary>
    /// Level 3: Fix + Review - AI creates fixes that require human approval
    /// Capabilities: Creates patches, shows diffs, waits for approval
    /// Risk: Medium (requires human review)
    /// Cost: Medium
    /// </summary>
    Review = 3,

    /// <summary>
    /// Level 4: Auto-Fix Sandbox - AI fixes in staging environment first
    /// Capabilities: Applies fixes to staging, runs tests, promotes if passing
    /// Risk: Medium (tested before production)
    /// Cost: Medium-High
    /// </summary>
    Sandbox = 4,

    /// <summary>
    /// Level 5: Full Auto-Fix - AI fixes production directly
    /// WARNING: High risk! AI can modify production code without approval
    /// Capabilities: Direct production fixes with auto-rollback if failed
    /// Risk: HIGH - Use with extreme caution
    /// Cost: High
    /// </summary>
    Auto = 5
}

/// <summary>
/// Extension methods for AiMode
/// </summary>
public static class AiModeExtensions
{
    public static string GetDisplayName(this AiMode mode) => mode switch
    {
        AiMode.Off => "Off - AI Disabled",
        AiMode.Alert => "Level 1: Alert Only",
        AiMode.Suggest => "Level 2: Suggest Fix",
        AiMode.Review => "Level 3: Fix + Review",
        AiMode.Sandbox => "Level 4: Auto-Fix Sandbox",
        AiMode.Auto => "Level 5: Full Auto-Fix",
        _ => "Unknown"
    };

    public static string GetDescription(this AiMode mode) => mode switch
    {
        AiMode.Off => "AI features are completely disabled. Manual operations only.",
        AiMode.Alert => "AI analyzes errors and sends smart, categorized notifications. No code modifications.",
        AiMode.Suggest => "AI explains errors and suggests fixes with code examples. You decide whether to apply.",
        AiMode.Review => "AI creates fix patches and shows diffs. Requires your approval before applying.",
        AiMode.Sandbox => "AI applies fixes to staging first, runs tests, and promotes to production if passing.",
        AiMode.Auto => "⚠️ DANGER: AI modifies production code directly. Auto-rollback on failure. Use with extreme caution!",
        _ => "Unknown mode"
    };

    public static string GetRiskLevel(this AiMode mode) => mode switch
    {
        AiMode.Off => "None",
        AiMode.Alert => "Very Low",
        AiMode.Suggest => "Low",
        AiMode.Review => "Medium",
        AiMode.Sandbox => "Medium",
        AiMode.Auto => "HIGH",
        _ => "Unknown"
    };

    public static string GetRiskColor(this AiMode mode) => mode switch
    {
        AiMode.Off => "gray",
        AiMode.Alert => "green",
        AiMode.Suggest => "blue",
        AiMode.Review => "yellow",
        AiMode.Sandbox => "orange",
        AiMode.Auto => "red",
        _ => "gray"
    };

    public static bool RequiresApproval(this AiMode mode) => mode switch
    {
        AiMode.Review => true,
        _ => false
    };

    public static bool CanModifyCode(this AiMode mode) => mode switch
    {
        AiMode.Review => true,
        AiMode.Sandbox => true,
        AiMode.Auto => true,
        _ => false
    };
}
