using System;
using Serilog.Context;
using Serilog.Events;

namespace Serilog
{
    /// <summary>
    /// Extends <see cref="ILogger"/> to add Full .NET Framework capabilities.
    /// </summary>
    public static class LoggerExtensions
    {
        /// <summary>
        /// Begins an operation that should be declared inside a using block or appropriately disposed of when completed.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="identifier">The identifier used for the operation. If not specified, a random guid will be used.</param>
        /// <param name="propertyBag">A colletion of additional properties to associate with the current operation. This is typically an anonymous type.</param>
        /// <param name="description">A description for this operation.</param>
        /// <param name="level">The level used to write the operation details to the log. By default this is the information level.</param>
        /// <param name="warnIfExceeds">Specifies a limit, if it takes more than this limit, the level will be set to warning. By default this is not used.</param>
        /// <param name="autoFailOnException">Specifies whether or not the operation should be marked with an outcome of <see cref="OperationOutcome.Fail"/> if an exception is detected.</param>
        /// <returns>A disposable object. Wrap this inside a using block so the dispose can be called to stop the timing.</returns>
        public static OperationContext BeginOperation(
            this ILogger logger,
            string description,
            string identifier = null,
            object propertyBag = null,
            LogEventLevel level = LogEventLevel.Information,
            TimeSpan? warnIfExceeds = null,
            bool autoFailOnException = true)
        {
            object operationIdentifier = identifier;
            if (string.IsNullOrEmpty(identifier))
                operationIdentifier = Guid.NewGuid();

            return new OperationContext(logger, level, warnIfExceeds, operationIdentifier, description, autoFailOnException, propertyBag);
        }
    }
}
