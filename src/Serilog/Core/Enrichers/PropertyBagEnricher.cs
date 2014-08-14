using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Serilog.Events;

namespace Serilog.Core.Enrichers
{
    /// <summary>
    /// Enriches log events with a collection of properties
    /// </summary>
    public class PropertyBagEnricher : ILogEventEnricher
    {
        private readonly IDictionary<string, object> _properties;
        private readonly bool _destructureObjects;

        /// <summary>
        /// Creates a new <see cref="PropertyBagEnricher"/>.
        /// </summary>
        /// <param name="properties">A collection of properties.</param>
        /// <param name="destructureObjects">If true, and the value is a non-primitive, non-array type,
        /// then the value will be converted to a structure; otherwise, unknown types will
        /// be converted to scalars, which are generally stored as strings.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public PropertyBagEnricher(IDictionary<string, object> properties, bool destructureObjects = false)
        {
            if (properties == null) throw new ArgumentNullException("properties");
            _properties = properties;
            _destructureObjects = destructureObjects;
        }

        /// <summary>
        /// Creates a new <see cref="PropertyBagEnricher"/>.
        /// </summary>
        /// <param name="propertyBag">An anonymous type containing properties.</param>
        /// <param name="destructureObjects">If true, and the value is a non-primitive, non-array type,
        /// then the value will be converted to a structure; otherwise, unknown types will
        /// be converted to scalars, which are generally stored as strings.</param>
        public PropertyBagEnricher(object propertyBag, bool destructureObjects = false)
            : this(ParsePropertyBag(propertyBag), destructureObjects)
        {
        }

        static Dictionary<string, object> ParsePropertyBag(object propertyBag)
        {
            if (propertyBag == null)
                return new Dictionary<string, object>();

            // Refelct the object and get the properties, converting them into a dictionary of key/value pairs
#if FULLNET
            var propInfos = propertyBag.GetType().GetProperties();
#else
            var propInfos = propertyBag.GetType().GetRuntimeProperties();
#endif
            return propInfos.ToDictionary(t => t.Name, t => t.GetValue(propertyBag, null));
        }

        #region Implementation of ILogEventEnricher

        /// <summary>
        /// Enrich the log event.
        /// </summary>
        /// <param name="logEvent">The log event to enrich.</param>
        /// <param name="propertyFactory">Factory for creating new properties to add to the event.</param>
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            if (logEvent == null) throw new ArgumentNullException("logEvent");
            if (propertyFactory == null) throw new ArgumentNullException("propertyFactory");
            foreach (var property in _properties)
            {
                logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(property.Key,
                                                                            property.Value,
                                                                            _destructureObjects));
            }
        }

        #endregion
    }
}
