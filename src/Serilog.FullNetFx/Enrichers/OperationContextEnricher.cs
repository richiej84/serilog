using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.Enrichers
{
    internal sealed class OperationContextEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            OperationContext.Enrich(logEvent, propertyFactory);
        }
    }
}
