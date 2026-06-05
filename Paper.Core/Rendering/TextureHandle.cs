namespace Paper.Core.Rendering
{
    /// <summary>
    /// Opaque handle to a GPU texture created and owned by an <see cref="ITextureFactory"/>.
    /// Do not construct or copy handles across different factory instances.
    ///
    /// The <see cref="GlHandle"/> property exposes the underlying OpenGL texture name for use by
    /// Paper rendering backends; application code should treat the struct as an opaque token.
    /// </summary>
    public readonly struct TextureHandle : IEquatable<TextureHandle>
    {
        /// <summary>
        /// The OpenGL texture object name.
        /// Intended for Paper rendering backend use only — application code should not read this.
        /// </summary>
        public uint GlHandle { get; }

        internal TextureHandle(uint glHandle) => GlHandle = glHandle;

        /// <summary>True when this handle refers to a real texture (not the default invalid value).</summary>
        public bool IsValid => GlHandle != 0;

        /// <summary>The invalid (null) handle — returned when texture creation fails.</summary>
        public static readonly TextureHandle Invalid = default;

        public bool Equals(TextureHandle other) => GlHandle == other.GlHandle;
        public override bool Equals(object? obj) => obj is TextureHandle t && Equals(t);
        public override int GetHashCode() => (int)GlHandle;
        public static bool operator ==(TextureHandle a, TextureHandle b) => a.GlHandle == b.GlHandle;
        public static bool operator !=(TextureHandle a, TextureHandle b) => a.GlHandle != b.GlHandle;
    }
}
