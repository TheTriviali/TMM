using System.Text.Json.Serialization;

namespace TMM
{
    /// <summary>Type of condition to evaluate for routing rules.</summary>
    public enum ConditionType
    {
        /// <summary>Match by file extension (e.g., ".asi", ".dll").</summary>
        FileExtension,

        /// <summary>Check if a folder exists in the mod structure.</summary>
        HasFolder,

        /// <summary>Check the count of folders at a given level.</summary>
        FolderCount,

        /// <summary>Check the count of files in the mod.</summary>
        FileCount,

        /// <summary>Substring match in the file path.</summary>
        PathContains,

        /// <summary>Match a specific filename.</summary>
        FilenameMatches,
    }

    /// <summary>Operator to apply when evaluating conditions.</summary>
    public enum ConditionOperator
    {
        /// <summary>Exact match (e.g., extension is ".asi").</summary>
        Is,

        /// <summary>Negation match (e.g., extension is not ".dll").</summary>
        IsNot,

        /// <summary>Substring contains.</summary>
        Contains,

        /// <summary>Does not contain substring.</summary>
        DoesNotContain,

        /// <summary>String starts with value.</summary>
        StartsWith,

        /// <summary>String ends with value.</summary>
        EndsWith,

        /// <summary>Regular expression pattern match.</summary>
        MatchesRegex,

        /// <summary>Numeric equality (e.g., folder count = 0).</summary>
        Equals,

        /// <summary>Numeric greater-than (e.g., folder count > 1).</summary>
        GreaterThan,

        /// <summary>Numeric less-than (e.g., folder count < 2).</summary>
        LessThan,
    }

    /// <summary>Logic operator for combining multiple conditions.</summary>
    public enum LogicOperator
    {
        /// <summary>All conditions must match.</summary>
        AND,

        /// <summary>At least one condition must match.</summary>
        OR,
    }

    /// <summary>
    /// Represents a single condition evaluated at deploy time.
    /// Conditions are chained together in routing rules using AND/OR logic.
    /// Serializable to/from JSON for .tmmgame profile storage.
    /// </summary>
    public class Condition
    {
        /// <summary>Type of condition to evaluate.</summary>
        public ConditionType Type { get; set; } = ConditionType.FileExtension;

        /// <summary>Operator to apply when evaluating the condition.</summary>
        public ConditionOperator Operator { get; set; } = ConditionOperator.Is;

        /// <summary>
        /// Operand value for the condition.
        /// Examples: ".asi", "modloader", "0", "data/", specific filename, or regex pattern.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Logic operator connecting this condition to the next condition (if any).
        /// When evaluating a condition list, determines how the next condition combines with this one.
        /// </summary>
        [JsonPropertyName("logic")]
        public LogicOperator Logic { get; set; } = LogicOperator.AND;
    }
}
