using System;
using System.Collections.Generic;

namespace JueMingZ.Compat
{
    public static class TerrariaTypeCache
    {
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal);

        public static Type Find(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            var cleanName = StripAssemblyName(fullName);
            Type cached;
            lock (SyncRoot)
            {
                if (Types.TryGetValue(cleanName, out cached))
                {
                    return cached;
                }
            }

            var resolved = Resolve(fullName, cleanName);
            if (resolved == null)
            {
                return null;
            }

            lock (SyncRoot)
            {
                Types[cleanName] = resolved;
            }

            return resolved;
        }

        private static Type Resolve(string fullName, string cleanName)
        {
            Type resolved = null;
            try
            {
                resolved = Type.GetType(fullName, false);
            }
            catch
            {
            }

            if (resolved == null && !string.Equals(fullName, cleanName, StringComparison.Ordinal))
            {
                try
                {
                    resolved = Type.GetType(cleanName, false);
                }
                catch
                {
                }
            }

            if (resolved != null)
            {
                return resolved;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                try
                {
                    resolved = assemblies[index].GetType(cleanName, false);
                    if (resolved != null)
                    {
                        return resolved;
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static string StripAssemblyName(string fullName)
        {
            var commaIndex = fullName.IndexOf(',');
            return commaIndex <= 0 ? fullName.Trim() : fullName.Substring(0, commaIndex).Trim();
        }
    }
}
