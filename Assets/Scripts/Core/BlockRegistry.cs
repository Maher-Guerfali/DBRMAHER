using System;
using System.Collections.Generic;
using System.Reflection;

namespace BlockSystem.Core
{
    // ══════════════════════════════════════════════════════════════════════
    //  BlockRegistry  —  automatic catalogue of every block type in the project.
    //
    //  You never need to manually register blocks here.
    //  When the registry is first accessed it scans every compiled assembly
    //  and adds any concrete class that inherits from Block.
    //
    //  Why it exists:
    //    When the JSON deserializer reads a saved graph it encounters a
    //    string like "DelayBlock" and needs to turn that back into an actual
    //    C# Type so it can call Activator.CreateInstance.  This registry
    //    provides that lookup by class name.
    //
    //    The graph editor's search window also reads BlockRegistry.All to
    //    build the list of blocks you can drag into the graph.
    //
    //  Adding a new block:
    //    Just create your class anywhere in the project.  Next time
    //    BlockRegistry.All is accessed it will find it automatically.
    // ══════════════════════════════════════════════════════════════════════
    public static class BlockRegistry
    {
        // Internal cache — null until first access, then stays populated
        // for the lifetime of the editor / play session.
        static Dictionary<string, Type> _types;

        // Read-only view of all discovered block types, keyed by class name.
        // Triggers a scan on first access.
        public static Dictionary<string, Type> All
        {
            get
            {
                if (_types == null)
                    Scan();
                return _types;
            }
        }

        // Walks every assembly loaded in the current AppDomain and records
        // any non-abstract class that inherits from Block.
        // Call this manually if you hot-reload assemblies during development.
        public static void Scan()
        {
            _types = new Dictionary<string, Type>();
            var baseType = typeof(Block);

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // Some assemblies fail to enumerate their types (e.g. dynamic
                // assemblies).  We silently skip those instead of crashing.
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (!t.IsAbstract && baseType.IsAssignableFrom(t))
                            _types[t.Name] = t;
                    }
                }
                catch (ReflectionTypeLoadException) { }
            }
        }

        // Look up a block type by its class name.
        // Returns null if the name is unknown — callers should handle that
        // gracefully (the deserializer logs a warning and skips the block).
        public static Type Get(string typeName)
        {
            All.TryGetValue(typeName, out var t);
            return t;
        }
    }
}
