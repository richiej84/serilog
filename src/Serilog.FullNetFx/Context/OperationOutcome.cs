namespace Serilog.Context
{
    /// <summary>
    /// An enumeration of outcomes for an <see cref="OperationContext"/>.
    /// </summary>
    public enum OperationOutcome
    {
        /// <summary>
        /// The outcome of the operation is unknown.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The operation succeeded.
        /// </summary>
        Success = 1,

        /// <summary>
        /// The operation failed.
        /// </summary>
        Fail = 2
    }
}