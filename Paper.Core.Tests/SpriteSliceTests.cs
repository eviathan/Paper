using Paper.Core.VirtualDom;
using Xunit;

namespace Paper.Core.Tests;

public class SpriteSliceTests
{
    [Fact]
    public void A_region_resolves_to_its_own_rectangle()
    {
        var slice = SpriteSlice.Region(32, 16, 16, 16);
        Assert.True(slice.Resolve(64, 64, out float x, out float y, out float w, out float h));
        Assert.Equal((32f, 16f, 16f, 16f), (x, y, w, h));
    }

    [Fact]
    public void A_cell_resolves_by_row_major_index_against_the_sheet_width()
    {
        var slice = SpriteSlice.Cell(5, 16, 16); // 4 columns on a 64px sheet → column 1, row 1
        Assert.True(slice.Resolve(64, 64, out float x, out float y, out float w, out float h));
        Assert.Equal((16f, 16f, 16f, 16f), (x, y, w, h));
    }

    [Fact]
    public void Slices_outside_the_sheet_or_empty_do_not_resolve()
    {
        Assert.False(SpriteSlice.Region(60, 0, 16, 16).Resolve(64, 64, out _, out _, out _, out _));
        Assert.False(SpriteSlice.Cell(16, 16, 16).Resolve(64, 64, out _, out _, out _, out _));
        Assert.False(SpriteSlice.Region(0, 0, 0, 16).IsValid);
    }

    [Fact]
    public void Slices_compare_by_value_so_unchanged_sprites_do_not_re_render()
    {
        Assert.Equal(SpriteSlice.Region(1, 2, 3, 4), SpriteSlice.Region(1, 2, 3, 4));
        Assert.NotEqual(SpriteSlice.Region(1, 2, 3, 4), SpriteSlice.Region(1, 2, 3, 5));
        Assert.NotEqual(SpriteSlice.Cell(0, 16, 16), SpriteSlice.Region(0, 0, 16, 16));
    }

    [Fact]
    public void Both_sprite_factories_carry_a_slice()
    {
        var byRegion = UI.Sprite("sheet.png", SpriteSlice.Region(0, 16, 16, 16));
        Assert.Equal(SpriteSlice.Region(0, 16, 16, 16), byRegion.Props.Slice);

        var byFrame = UI.Sprite("sheet.png", 3, 16, 16);
        Assert.Equal(SpriteSlice.Cell(3, 16, 16), byFrame.Props.Slice);
        Assert.Equal(3, byFrame.Props.FrameIndex); // the frame-index props still work
    }
}
