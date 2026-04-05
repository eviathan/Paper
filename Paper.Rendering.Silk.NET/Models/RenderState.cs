namespace Paper.Rendering.Silk.NET.Models
{
    public class RenderState
    {
        public bool LayoutDirty;
        public bool NeedsLayout;
        public double AnimationDeadline;
        public int LastStyleRegistryVersion = -1;
        public bool ExternalRenderRequested;
    }
}
