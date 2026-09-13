namespace Paper.Core.Rendering
{
    /// <summary>
    /// Creates and manages GPU textures for use inside <c>Canvas2D</c> draw callbacks.
    ///
    /// Obtain the factory from <c>Canvas.TextureFactory</c> after the window is initialised.
    /// All methods must be called on the UI / render thread.
    ///
    /// Typical DAW usage pattern:
    /// <code>
    ///   // Once: allocate a texture for a waveform clip
    ///   var tex = canvas.TextureFactory.CreateRgba(width, height, pixels);
    ///
    ///   // When clip data changes: update the dirty columns
    ///   canvas.TextureFactory.UpdateRegion(tex, dirtyX, 0, dirtyW, height, newPixels);
    ///
    ///   // Each frame inside Canvas2D draw callback:
    ///   ctx.DrawTexture(tex, 0, 0, ctx.Width, ctx.Height);
    ///
    ///   // On clip removal:
    ///   canvas.TextureFactory.Dispose(tex);
    /// </code>
    /// </summary>
    public interface ITextureFactory
    {
        /// <summary>
        /// Create a new RGBA texture initialised with <paramref name="pixels"/>.
        /// The pixel data must be tightly packed: width × height × 4 bytes (R, G, B, A).
        /// Returns <see cref="TextureHandle.Invalid"/> on failure.
        /// </summary>
        TextureHandle CreateRgba(int width, int height, ReadOnlySpan<byte> pixels);

        /// <summary>
        /// Create an empty RGBA texture of the given dimensions.  Pixel content is undefined
        /// until populated with <see cref="UpdateRegion"/>.
        /// Returns <see cref="TextureHandle.Invalid"/> on failure.
        /// </summary>
        TextureHandle CreateRgba(int width, int height);

        /// <summary>
        /// Upload new pixel data into a rectangular sub-region of an existing texture.
        /// <paramref name="pixels"/> must contain w × h × 4 bytes (R, G, B, A).
        /// This is the efficient path for streaming waveform or meter data — update only the
        /// columns or rows that changed rather than re-uploading the whole texture.
        /// </summary>
        void UpdateRegion(TextureHandle texture, int x, int y, int w, int h,
                          ReadOnlySpan<byte> pixels);

        /// <summary>Delete the GPU texture backing <paramref name="texture"/>.</summary>
        void Dispose(TextureHandle texture);
    }
}
