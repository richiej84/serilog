using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Messaging;
using Serilog.Core;
using Serilog.Core.Enrichers;
using Serilog.Events;

namespace Serilog.Context
{
    /// <summary>
    /// Identifies a unit of work that has its own contextual data, along with measurements and information about the work carried out. 
    /// </summary>
    public sealed class OperationContext : IDisposable
    {
        private const string BeginOperationMessage =
            "Beginning operation {TimedOperationId}: {TimedOperationDescription}";

        private const string OperationExccededTimeMessage =
            "Operation {TimedOperationId}: {TimedOperationDescription} exceeded the limit of {WarningLimit} by completing with status {Outcome} in {TimedOperationElapsed}  ({TimedOperationElapsedInMs} ms)";

        private const string OperationCompletedMessage =
            "Completed operation {TimedOperationId}: {TimedOperationDescription} with status {Outcome} in {TimedOperationElapsed} ({TimedOperationElapsedInMs} ms)";

        private readonly ILogger _logger;
        private readonly LogEventLevel _level;
        private readonly TimeSpan? _warnIfExceeds;
        private readonly object _identifier;
        private readonly string _description;
        private readonly bool _autoFailOnException;
        private readonly IDisposable _operationContextBookmark;
        private readonly IDisposable _contextualPropertiesBookmark;
        private readonly Stopwatch _sw;
        private OperationOutcome _outcome = OperationOutcome.Unknown;

        /// <summary>
        /// Initializes a new instance of the <see cref="OperationContext" /> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="identifier">The identifier used for the operation. If not specified, a random guid will be used.</param>
        /// <param name="description">A description for the operation.</param>
        /// <param name="level">The level used to write the operation details to the logger. By default this is the information level.</param>
        /// <param name="warnIfExceeds">Specifies a limit, if it takes more than this limit, the level will be set to warning. By default this is not used.</param>
        /// <param name="autoFailOnException">Specifies whether or not the operation should be marked with an outcome of <see cref="OperationOutcome.Fail"/> if an exception is detected.</param>
        /// <param name="propertyBag">A colletion of additional properties to associate with the current operation. This is typically an anonymous type.</param>
        internal OperationContext(ILogger logger,
                                  LogEventLevel level,
                                  TimeSpan? warnIfExceeds,
                                  object identifier,
                                  string description,
                                  bool autoFailOnException,
                                  object propertyBag)
        {
            _logger = logger;
            _level = level;
            _warnIfExceeds = warnIfExceeds;
            _identifier = identifier;
            _description = description;
            _autoFailOnException = autoFailOnException;

            _operationContextBookmark = OperationLogContext.PushOperationId(identifier);

            if (propertyBag != null)
            {
                // Save the first contextual property that we set. We then dispose of this bookmark, reverting the stack to what it was previously
                _contextualPropertiesBookmark = LogContext.PushProperties(new PropertyBagEnricher(propertyBag));
            }

            _logger.Write(_level, BeginOperationMessage, _identifier, _description);

            _sw = Stopwatch.StartNew();
        }

        /// <summary>
        /// Gets a value indicating the outcome of the operation.
        /// </summary>
        public OperationOutcome Outcome
        {
            get { return _outcome; }
        }

        /// <summary>
        /// Mark the operation as having succeeded.
        /// </summary>
        public void Success()
        {
            _outcome = OperationOutcome.Success;
        }

        /// <summary>
        /// Mark the operation as having failed.
        /// </summary>
        public void Fail()
        {
            _outcome = OperationOutcome.Fail;
        }

        /// <summary>
        /// Enriches a given log event with data from the current <see cref="OperationContext"/>.
        /// </summary>
        /// <param name="logEvent">The log event to enrich.</param>
        /// <param name="propertyFactory">Factory for creating new properties to add to the event.</param>
        public static void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            OperationLogContext.EnrichLogEvent(logEvent, propertyFactory);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                _sw.Stop();

                if (_autoFailOnException && HasExceptionBeenThrown()) _outcome = OperationOutcome.Fail;

                if (_warnIfExceeds.HasValue && _sw.Elapsed > _warnIfExceeds.Value)
                    _logger.Write(LogEventLevel.Warning, OperationExccededTimeMessage, _identifier, _description, _warnIfExceeds.Value, _outcome, _sw.Elapsed, _sw.ElapsedMilliseconds);
                else if (_outcome == OperationOutcome.Fail)
                    _logger.Write(LogEventLevel.Warning, OperationCompletedMessage, _identifier, _description, _outcome, _sw.Elapsed, _sw.ElapsedMilliseconds);
                else
                    _logger.Write(_level, OperationCompletedMessage, _identifier, _description, _outcome, _sw.Elapsed, _sw.ElapsedMilliseconds);
            }
            finally
            {
                _contextualPropertiesBookmark.Dispose();
                _operationContextBookmark.Dispose();
            }
        }

        private static bool HasExceptionBeenThrown()
        {
            return Marshal.GetExceptionPointers() != IntPtr.Zero ||
                   Marshal.GetExceptionCode() != 0;
        }

        /// <summary>
        /// A private log context, specifically for storing operation data.
        /// </summary>
        private static class OperationLogContext
        {
            private const string OperationIdName = "OperationId";
            private const string ParentOperationIdName = "ParentOperationId";
            private const string OperationStackName = "OperationStack";
            static readonly string DataSlotName = typeof(OperationLogContext).FullName;

            public static IDisposable PushOperationId(object value)
            {
                var stack = GetOrCreateStack();
                var bookmark = new ContextStackBookmark(stack);

                Values = stack.Push(value);

                return bookmark;
            }

            static ImmutableStack<object> GetOrCreateStack()
            {
                var values = Values;
                if (values == null)
                {
                    values = ImmutableStack<object>.Empty;
                    Values = values;
                }
                return values;
            }

            static ImmutableStack<object> Values
            {
                get { return (ImmutableStack<object>)CallContext.LogicalGetData(DataSlotName); }
                set { CallContext.LogicalSetData(DataSlotName, value); }
            }

            internal static void EnrichLogEvent(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
                var values = Values;
                if (values == null || values == ImmutableStack<object>.Empty)
                    return;

                var currentAndParentOps = values.Take(2).ToArray();

                new PropertyEnricher(OperationIdName, currentAndParentOps[0]).Enrich(logEvent, propertyFactory);

                if (currentAndParentOps.Length > 1)
                {
                    new PropertyEnricher(ParentOperationIdName, currentAndParentOps[1]).Enrich(logEvent, propertyFactory);
                }

                new PropertyEnricher(OperationStackName, values).Enrich(logEvent, propertyFactory);
            }

            sealed class ContextStackBookmark : IDisposable
            {
                readonly ImmutableStack<object> _bookmark;

                public ContextStackBookmark(ImmutableStack<object> bookmark)
                {
                    _bookmark = bookmark;
                }

                public void Dispose()
                {
                    Values = _bookmark;
                }
            }
        }
    }
}
