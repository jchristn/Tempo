namespace Test.Shared
{
    using System;

    /// <summary>
    /// Lightweight assertion helpers. Touchstone descriptors indicate failure by throwing,
    /// so these helpers throw on violations.
    /// </summary>
    public static class Assert2
    {
        public static void True(bool condition, string message)
        {
            if (!condition) throw new Exception("assertion failed: " + message);
        }

        public static void False(bool condition, string message)
        {
            if (condition) throw new Exception("assertion failed: " + message);
        }

        public static void NotNull(object? value, string message)
        {
            if (value == null) throw new Exception("expected non-null: " + message);
        }

        public static void IsNull(object? value, string message)
        {
            if (value != null) throw new Exception("expected null: " + message);
        }

        public static void Equal<T>(T expected, T actual, string message)
        {
            if (!Equals(expected, actual)) throw new Exception("expected " + expected + " but got " + actual + ": " + message);
        }

        public static void NotEqual<T>(T expected, T actual, string message)
        {
            if (Equals(expected, actual)) throw new Exception("expected a value other than " + expected + ": " + message);
        }

        public static void StartsWith(string expected, string actual, string message)
        {
            if (actual == null || !actual.StartsWith(expected, StringComparison.Ordinal))
                throw new Exception("expected prefix '" + expected + "' but got '" + actual + "': " + message);
        }

        public static void Throws<TException>(Action action, string message) where TException : Exception
        {
            try { action(); }
            catch (TException) { return; }
            catch (Exception ex)
            {
                throw new Exception("expected " + typeof(TException).Name + " but got " + ex.GetType().Name + ": " + message);
            }
            throw new Exception("expected " + typeof(TException).Name + " but no exception was thrown: " + message);
        }
    }
}
