namespace Oms.Temporal.Versioning;

using Temporalio.Workflows;
using Microsoft.Extensions.Logging;

/// <summary>
/// Workflow versioning helpers for backward compatibility
/// Allows deploying new code while old workflows finish with old behavior
/// </summary>
public static class WorkflowVersioning
{
    /// <summary>
    /// Example versioning gate for breaking changes.
    /// If version >= targetVersion, use new logic; otherwise use old behavior.
    /// </summary>
    public static int GetWorkflowVersion(
        string changeId,
        int minSupported,
        int maxSupported)
    {
        return Workflow.GetVersion(changeId, minSupported, maxSupported);
    }

    /// <summary>
    /// Check if new activity should be executed (backward compatible)
    /// Existing workflows will skip the new activity during replay
    /// </summary>
    public static bool ShouldExecuteNewActivity(
        string activityChangeId,
        int targetVersion,
        ILogger logger)
    {
        try
        {
            int version = Workflow.GetVersion(activityChangeId, 1, targetVersion);
            return version >= targetVersion;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error checking workflow version for {ActivityChangeId}", activityChangeId);
            return false;
        }
    }
}

/// <summary>
/// Workflow versioning patterns and best practices:
/// 
/// Pattern 1: Adding New Activity (Non-Breaking)
/// -----------------------------------------
/// Before:
///   await activity1();
///   await activity3();
/// 
/// After (with versioning):
///   int version = Workflow.GetVersion("AddActivity2", 1, 1);
///   await activity1();
///   if (version >= 1) {
///       await newActivity2(); // Skipped during replay of old workflows
///   }
///   await activity3();
/// 
/// 
/// Pattern 2: Changing Activity Sequence (Breaking)
/// -----------------------------------------
/// Before:
///   await activity1();
///   await activity2();
/// 
/// After (with new version):
///   int version = Workflow.GetVersion("ReorderActivities", 1, 2);
///   if (version >= 2) {
///       // New sequence for v2+
///       await activity2();
///       await activity1();
///   } else {
///       // Old sequence for v1 (during replay)
///       await activity1();
///       await activity2();
///   }
/// 
/// 
/// Pattern 3: Removing Activity (Breaking)
/// -----------------------------------------
/// Before:
///   await activity1();
///   await deprecatedActivity();
///   await activity3();
/// 
/// After:
///   int version = Workflow.GetVersion("RemoveDeprecatedActivity", 1, 2);
///   await activity1();
///   if (version < 2) {
///       // Execute old deprecated activity during replay
///       await deprecatedActivity();
///   }
///   // Skip for new executions
///   await activity3();
/// 
/// 
/// Pattern 4: Conditional Logic Changes
/// -----------------------------------------
/// Before:
///   if (order.Amount > 1000) { ... }
/// 
/// After:
///   int version = Workflow.GetVersion("ChangeRiskThreshold", 1, 2);
///   if (version >= 2) {
///       if (order.Amount > 5000) { ... } // New threshold
///   } else {
///       if (order.Amount > 1000) { ... } // Old threshold
///   }
/// 
/// 
/// IMPORTANT: Never remove Workflow.GetVersion calls
/// Once added, they must remain in code (even if maxSupported == minSupported)
/// This ensures historical workflows can replay correctly
/// </summary>
