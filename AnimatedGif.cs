using System.Buffers.Binary;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Drawing = System.Drawing;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using WpfImage = System.Windows.Controls.Image;

namespace HopeChestCore;

internal sealed class AnimatedGif : IDisposable
{
    private readonly WpfImage _target;
    private readonly DispatcherTimer _timer = new();
    private readonly BitmapSource[] _frames;
    private readonly TimeSpan[] _delays;
    private int _frame;

    public AnimatedGif(WpfImage target, Stream stream)
    {
        _target = target;
        using var input = stream;
        using var gif = Drawing.Image.FromStream(input);
        _frames = LoadFrames(gif);
        _delays = new TimeSpan[_frames.Length];
        var delays = gif.PropertyItems.FirstOrDefault(property => property.Id == 0x5100)?.Value ?? [];
        for (var i = 0; i < _delays.Length; i++)
        {
            var hundredths = (i + 1) * sizeof(int) <= delays.Length
                ? BinaryPrimitives.ReadInt32LittleEndian(delays.AsSpan(i * sizeof(int), sizeof(int))) : 10;
            _delays[i] = TimeSpan.FromMilliseconds(hundredths > 1 ? hundredths * 10.0 : 100);
        }

        _timer.Tick += Advance;
        ShowFrame();
    }

    public void SetActive(bool active)
    {
        if (active && _frames.Length > 1)
            _timer.Start();
        else
            _timer.Stop();
    }

    private void Advance(object? sender, EventArgs e)
    {
        _frame = (_frame + 1) % _frames.Length;
        ShowFrame();
    }

    private void ShowFrame()
    {
        _target.Source = _frames[_frame];
        _timer.Interval = _delays[_frame];
    }

    private static BitmapSource[] LoadFrames(Drawing.Image gif)
    {
        const int size = 148;
        const int padding = 3;
        var scale = (double)size / Math.Max(gif.Width, gif.Height);
        var width = Math.Max(1, (int)Math.Round(gif.Width * scale));
        var height = Math.Max(1, (int)Math.Round(gif.Height * scale));
        var rectangle = new Drawing.Rectangle(0, 0, width, height);
        var frames = new BitmapSource[gif.GetFrameCount(FrameDimension.Time)];
        var visible = Drawing.Rectangle.Empty;
        using var thumbnail = new Drawing.Bitmap(width, height, DrawingPixelFormat.Format32bppArgb);
        for (var i = 0; i < frames.Length; i++)
        {
            gif.SelectActiveFrame(FrameDimension.Time, i);
            using (var canvas = Drawing.Graphics.FromImage(thumbnail))
            {
                canvas.CompositingMode = Drawing.Drawing2D.CompositingMode.SourceCopy;
                canvas.InterpolationMode = Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                canvas.DrawImage(gif, rectangle);
            }

            var data = thumbnail.LockBits(rectangle, ImageLockMode.ReadOnly, DrawingPixelFormat.Format32bppArgb);
            var pixels = new byte[width * height * 4];
            try
            {
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            }
            finally
            {
                thumbnail.UnlockBits(data);
            }

            var bounds = RemoveBackdrop(pixels, width, height);
            if (!bounds.IsEmpty)
                visible = visible.IsEmpty ? bounds : Drawing.Rectangle.Union(visible, bounds);

            frames[i] = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        }

        // Use one crop and center for the entire loop so the character can move naturally.
        if (visible.IsEmpty)
            visible = rectangle;
        var crop = new Int32Rect(visible.X, visible.Y, visible.Width, visible.Height);
        var side = Math.Max(crop.Width, crop.Height) + padding * 2;
        var stride = side * 4;
        var offset = (side - crop.Height) / 2 * stride + (side - crop.Width) / 2 * 4;
        for (var i = 0; i < frames.Length; i++)
        {
            var pixels = new byte[side * stride];
            frames[i].CopyPixels(crop, pixels, stride, offset);
            frames[i] = BitmapSource.Create(side, side, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            frames[i].Freeze();
        }
        return frames;
    }

    private static Drawing.Rectangle RemoveBackdrop(byte[] pixels, int width, int height)
    {
        var queue = new int[width * height];
        var visited = new bool[queue.Length];
        var head = 0;
        var tail = 0;

        // Only key out light, near-neutral pixels connected to the outside of the frame.
        // The similar colors in the character's clothing remain inside its outline.
        void Visit(int position)
        {
            if (visited[position])
                return;
            visited[position] = true;
            var offset = position * 4;
            var min = Math.Min(pixels[offset], Math.Min(pixels[offset + 1], pixels[offset + 2]));
            var max = Math.Max(pixels[offset], Math.Max(pixels[offset + 1], pixels[offset + 2]));
            if (pixels[offset + 3] == 0 || min >= 224 && max - min <= 27)
            {
                Array.Clear(pixels, offset, 4);
                queue[tail++] = position;
            }
        }

        for (var x = 0; x < width; x++)
        {
            Visit(x);
            Visit((height - 1) * width + x);
        }
        for (var y = 1; y < height - 1; y++)
        {
            Visit(y * width);
            Visit((y + 1) * width - 1);
        }
        while (head < tail)
        {
            var position = queue[head++];
            var x = position % width;
            if (x > 0) Visit(position - 1);
            if (x < width - 1) Visit(position + 1);
            if (position >= width) Visit(position - width);
            if (position < width * (height - 1)) Visit(position + width);
        }

        var left = width;
        var top = height;
        var right = -1;
        var bottom = -1;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (pixels[(y * width + x) * 4 + 3] == 0)
                    continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }
        return right < left ? Drawing.Rectangle.Empty : Drawing.Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
    }

    public void Dispose() => _timer.Stop();
}
