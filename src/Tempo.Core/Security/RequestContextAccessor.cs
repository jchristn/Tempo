namespace Tempo.Core.Security
{
    using System;

    /// <summary>
    /// Helpers for placing and retrieving a <see cref="RequestContext"/> on an arbitrary metadata carrier.
    /// The Watson integration stores the context as <see cref="object"/> on <c>AuthResult.Metadata</c>;
    /// this class handles the cast boilerplate.
    /// </summary>
    public static class RequestContextAccessor
    {
        /// <summary>Key used when stashing a context on an IDictionary-style bag.</summary>
        public const string Key = "tempo.request.context";

        /// <summary>
        /// Retrieve the request context from an opaque metadata value.
        /// </summary>
        /// <param name="metadata">Metadata value, typically AuthResult.Metadata.</param>
        /// <returns>Resolved request context, or null.</returns>
        public static RequestContext? From(object? metadata)
        {
            if (metadata == null) return null;
            if (metadata is RequestContext ctx) return ctx;
            return null;
        }

        /// <summary>
        /// Retrieve the request context from metadata or throw when not present.
        /// </summary>
        /// <param name="metadata">Metadata value.</param>
        /// <returns>Resolved request context.</returns>
        /// <exception cref="InvalidOperationException">Thrown when no context is present.</exception>
        public static RequestContext Require(object? metadata)
        {
            RequestContext? ctx = From(metadata);
            if (ctx == null) throw new InvalidOperationException("RequestContext is not attached to this request.");
            return ctx;
        }
    }
}
