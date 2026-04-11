using System.Reflection;
using Paper.CSX.Runtime;

namespace Paper.Rendering.Silk.NET
{
    public sealed partial class Canvas
    {
        /// <summary>
        /// Configure additional assemblies to be available during CSX hot-reload compilation.
        /// Call this before MountCSXHotReload to ensure game engine types are accessible.
        /// </summary>
        public void AddCSXReferences(params Assembly[] assemblies)
        {
            CSXRuntimeCompiler.AddReferences(assemblies);
        }
    }
}