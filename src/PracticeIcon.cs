using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace TH06NCTrainer;

// Original vector-drawn practice badge. No game artwork is used in this icon.
internal static class PracticeIcon
{
    internal static Icon Create()
    {
        using var stream = new MemoryStream(Build());
        using var icon = new Icon(stream, new Size(32, 32));
        return (Icon)icon.Clone();
    }
    internal static void Write(string path) => File.WriteAllBytes(path, Build());
    private static byte[] Build()
    {
        int[] sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256];
        var images = new List<byte[]>();
        foreach (int size in sizes)
        {
            using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap)) Draw(graphics, size);
            using var png = new MemoryStream(); bitmap.Save(png, ImageFormat.Png);
            images.Add(png.ToArray());
        }
        using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
        int offset = 6 + sizes.Length * 16;
        for (int i = 0; i < sizes.Length; i++)
        {
            writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i])); writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
            writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
            writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
        }
        foreach (var image in images) writer.Write(image);
        return stream.ToArray();
    }
    private static void Draw(Graphics graphics, int size)
    {
        graphics.Clear(Color.Transparent); graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.ScaleTransform(size / 64F, size / 64F);
        using var tile = new GraphicsPath();
        tile.AddArc(2, 2, 16, 16, 180, 90); tile.AddArc(46, 2, 16, 16, 270, 90);
        tile.AddArc(46, 46, 16, 16, 0, 90); tile.AddArc(2, 46, 16, 16, 90, 90); tile.CloseFigure();
        using var red = new LinearGradientBrush(new Rectangle(0, 0, 64, 64), Color.FromArgb(181, 38, 65), Color.FromArgb(74, 12, 34), 90F);
        graphics.FillPath(red, tile);
        using var edge = new Pen(Color.FromArgb(245, 131, 149), 2); graphics.DrawPath(edge, tile);
        using var letter = new GraphicsPath(FillMode.Alternate);
        letter.AddPolygon([new Point(19, 13), new Point(34, 13), new Point(46, 20), new Point(46, 32), new Point(34, 39), new Point(28, 39), new Point(28, 52), new Point(19, 52)]);
        letter.AddPolygon([new Point(28, 22), new Point(33, 22), new Point(37, 25), new Point(37, 28), new Point(33, 31), new Point(28, 31)]);
        using var ivory = new SolidBrush(Color.FromArgb(255, 242, 229)); graphics.FillPath(ivory, letter);
    }
}
