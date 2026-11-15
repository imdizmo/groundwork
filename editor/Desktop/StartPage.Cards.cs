using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Groundwork.App;

namespace Groundwork.Desktop;

public sealed partial class StartPage
{
    /// <summary>The ground pictures' box: the picture fits in it, its shape kept.</summary>
    private const int ThumbWidth = 208, ThumbHeight = 150;

    /// <summary>
    /// A map's card: the ground's picture, the name, the size and objects, when it was last saved; read off the window's
    /// thread once the card shows. A map whose folder is gone is greyed and says so.
    /// </summary>
    internal sealed class Card
    {
        /// <summary>The map's folder.</summary>
        public string Folder { get; }

        /// <summary>The map's name (the folder's, when map.json cannot be read).</summary>
        public string Name { get; }

        /// <summary>Whether the folder holds a map.</summary>
        public bool Found { get; }

        /// <summary>The card as shown.</summary>
        public Button Button { get; }

        /// <summary>The line of facts (size, objects, Unreal), for the tests.</summary>
        internal TextBlock Facts { get; }

        public Card(StartPage page, string folder)
        {
            Folder = folder;
            Found = MapProject.IsMap(folder);
            Name = Found ? NameOf(folder) : Path.GetFileName(folder);
            var image = new Image { Stretch = Stretch.Uniform };
            var picture = new Border { Height = ThumbHeight, CornerRadius = new CornerRadius(4), ClipToBounds = true, Child = image };
            Facts = new TextBlock { FontSize = 11, Foreground = Tokens.Dim, Text = Found ? Lang.T("start.card.reading") : Lang.T("start.card.missing"), TextWrapping = TextWrapping.Wrap };
            var when = new TextBlock { FontSize = 11, Foreground = Tokens.Dim, Text = Found ? "" : Ui.Tilde(folder), TextTrimming = TextTrimming.CharacterEllipsis };
            Button = new Button
            {
                Width = CardWidth,
                Padding = new Thickness(12, 12, 12, 10),
                Margin = new Thickness(0, 0, 10, 10),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Opacity = Found ? 1 : 0.55,
                Content = new StackPanel
                {
                    Spacing = 4,
                    Children =
                    {
                        picture,
                        new TextBlock { Text = Name, FontSize = 14, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 4, 0, 0) },
                        Facts,
                        when,
                    },
                },
            }.Tip(Found ? "start.openMap" : "start.missing");
            Button.Click += (_, _) =>
            {
                if (Found)
                {
                    page.MapRequested?.Invoke(folder);
                }
            };
            var menu = new ContextMenu();
            if (Found)
            {
                menu.Items.Add(Item("start.menu.open", () => page.MapRequested?.Invoke(folder)));
            }
            if (Directory.Exists(folder))
            {
                menu.Items.Add(Item("start.menu.show", () => _ = page.OpenUrl(new Uri(folder))));
            }
            if (Found)
            {
                menu.Items.Add(Item("start.menu.duplicate", () => page.DuplicateRequested?.Invoke(folder)));
            }
            menu.Items.Add(Item("start.menu.remove", () => page.RemoveRequested?.Invoke(folder)));
            Button.ContextMenu = menu;
            if (Found)
            {
                _ = FillAsync(folder, image, Facts, when);
            }
        }

        private static MenuItem Item(string key, Action act)
        {
            var item = new MenuItem { Header = Lang.T(key) };
            item.Click += (_, _) => act();
            return item;
        }

        private static string NameOf(string folder)
        {
            try
            {
                return MapProject.Open(folder).Name;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            {
                return Path.GetFileName(folder);
            }
        }

        /// <summary>The facts and the picture, read on a worker and put on the card.</summary>
        private static async Task FillAsync(string folder, Image image, TextBlock facts, TextBlock when)
        {
            MapFacts read;
            try
            {
                read = await Task.Run(() => MapFacts.Of(folder));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            {
                Log.Caught(ex, $"reading the start page's facts of {folder}");
                facts.Text = Lang.T("start.card.unreadable");
                return;
            }
            facts.Text = Lang.T("start.card.facts", read.Size, read.Objects.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)) + (read.Unreal ? " · " + Lang.T("start.card.unreal") : "");
            when.Text = Lang.T("start.card.saved", When(read.Saved));
            if (read.Thumb is { } thumb)
            {
                image.Source = thumb.Bitmap ??= ToBitmap(thumb);
            }
        }

        /// <summary>A time as said on a card: today at 14:02, yesterday, 3 days ago, else the date.</summary>
        internal static string When(DateTime t)
        {
            var today = DateTime.Today;
            return t.Date == today ? Lang.T("start.when.today", t.ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture))
                : t.Date == today.AddDays(-1) ? Lang.T("start.when.yesterday")
                : (today - t.Date).TotalDays < 7 ? Lang.T("start.when.days", (int)(today - t.Date).TotalDays)
                : t.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static WriteableBitmap ToBitmap(Thumb t)
        {
            var bitmap = new WriteableBitmap(new PixelSize(t.Width, t.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
            using var fb = bitmap.Lock();
            for (int y = 0; y < t.Height; y++)
            {
                Marshal.Copy(t.Pixels, y * t.Width, fb.Address + (y * fb.RowBytes), t.Width);
            }
            return bitmap;
        }
    }

    /// <summary>A ground picture's pixels (BGRA), and the bitmap made of them once shown.</summary>
    internal sealed class Thumb(int width, int height, int[] pixels)
    {
        public int Width { get; } = width;
        public int Height { get; } = height;
        public int[] Pixels { get; } = pixels;
        public WriteableBitmap? Bitmap { get; set; }
    }

    /// <summary>What a card says of a map, read from its files.</summary>
    /// <param name="Size">"252 × 252 m".</param>
    /// <param name="Objects">How many objects it holds.</param>
    /// <param name="Saved">When one of its files was last written.</param>
    /// <param name="Unreal">Whether it has an Unreal project.</param>
    /// <param name="Thumb">The ground's picture (null when the heights could not be read).</param>
    internal sealed record MapFacts(string Size, int Objects, DateTime Saved, bool Unreal, Thumb? Thumb)
    {
        // ponytail: one small cache, the facts read again when a file in the folder changes; 64 maps at most (a
        // picture is 100 KB), the oldest dropped
        private static readonly Dictionary<string, (DateTime Saved, MapFacts Facts)> Kept = [];
        private const int KeepAtMost = 64;

        /// <summary>The map's facts, from the cache while nothing in its folder was written since.</summary>
        public static MapFacts Of(string folder)
        {
            DateTime saved = Directory.EnumerateFiles(folder).Select(File.GetLastWriteTime).DefaultIfEmpty(DateTime.MinValue).Max();
            lock (Kept)
            {
                if (Kept.TryGetValue(folder, out var kept) && kept.Saved == saved)
                {
                    return kept.Facts;
                }
            }
            var m = MapProject.Open(folder);
            var raster = m.ReadRaster();
            var facts = new MapFacts($"{raster.Cols - 1} × {raster.Rows - 1} m", CountObjects(m.ObjectsFile), saved, m.HasUnrealLevel, ReadThumb(m.HeightsFile, m.SeaLevel));
            lock (Kept)
            {
                if (Kept.Count >= KeepAtMost && !Kept.ContainsKey(folder))
                {
                    Kept.Remove(Kept.MinBy(kv => kv.Value.Saved).Key);
                }
                Kept[folder] = (saved, facts);
            }
            return facts;
        }

        /// <summary>How many objects objects.json holds: the entries of its array.</summary>
        private static int CountObjects(string file)
        {
            if (!File.Exists(file))
            {
                return 0;
            }
            var reader = new Utf8JsonReader(File.ReadAllBytes(file));
            int n = 0;
            while (reader.Read())
            {
                if (reader.CurrentDepth == 1 && reader.TokenType is JsonTokenType.StartArray or JsonTokenType.StartObject)
                {
                    n++;
                }
            }
            return n;
        }

        /// <summary>
        /// The ground's picture: the heights sampled into the box (only the rows needed are read), the sea blue by
        /// depth, the land green to brown to grey to white from its lowest ground to its top, lit from the north-west.
        /// </summary>
        private static Thumb? ReadThumb(string heights, float sea)
        {
            if (!File.Exists(heights))
            {
                return null;
            }
            var (rows, cols, at) = MapProject.NpyShape(heights);
            double scale = Math.Min(ThumbWidth / (double)cols, ThumbHeight / (double)rows);
            int w = Math.Max(2, (int)Math.Round(cols * scale)), h = Math.Max(2, (int)Math.Round(rows * scale));
            float[] s = new float[w * h], row = new float[cols];
            byte[] bytes = new byte[cols * 4];
            using (var f = File.OpenRead(heights))
            {
                for (int y = 0; y < h; y++)
                {
                    long gz = (long)y * (rows - 1) / (h - 1);
                    f.Seek(at + (gz * cols * 4), SeekOrigin.Begin);
                    f.ReadExactly(bytes);
                    Buffer.BlockCopy(bytes, 0, row, 0, bytes.Length);
                    for (int x = 0; x < w; x++)
                    {
                        s[(y * w) + x] = row[(int)((long)x * (cols - 1) / (w - 1))];
                    }
                }
            }
            // The land's colours run from its lowest ground (the shore, or the lowest point of a map without sea) to its top.
            float low = float.MaxValue, high = float.MinValue;
            foreach (float v in s)
            {
                (low, high) = (MathF.Min(low, v), MathF.Max(high, v));
            }
            low = MathF.Max(low, sea);
            high = MathF.Max(high, low + 1);
            float metresAPixel = (float)(cols / (double)w);
            int[] px = new int[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int p = (y * w) + x;
                    float v = s[p], r, g, b;
                    if (v < sea)
                    {
                        float deep = Math.Clamp((sea - v) / 8f, 0, 1);
                        (r, g, b) = (0.2f - (0.1f * deep), 0.42f - (0.15f * deep), 0.5f - (0.1f * deep));
                    }
                    else
                    {
                        (r, g, b) = Land((v - low) / (high - low));
                        float dx = s[(y * w) + Math.Max(0, x - 1)] - s[(y * w) + Math.Min(w - 1, x + 1)];
                        float dz = s[(Math.Min(h - 1, y + 1) * w) + x] - s[(Math.Max(0, y - 1) * w) + x];
                        float shade = Math.Clamp(1 + ((dx + dz) / metresAPixel * 0.35f), 0.55f, 1.35f);
                        (r, g, b) = (r * shade, g * shade, b * shade);
                    }
                    px[p] = unchecked((int)0xFF000000) | (Byte(r) << 16) | (Byte(g) << 8) | Byte(b);
                }
            }
            return new Thumb(w, h, px);
        }

        /// <summary>The land's colour at a height (0 the shore, 1 the top): green, olive, brown, grey, white.</summary>
        private static (float R, float G, float B) Land(float t)
        {
            (float, float, float)[] stops = [(0.30f, 0.46f, 0.22f), (0.52f, 0.52f, 0.26f), (0.50f, 0.40f, 0.28f), (0.52f, 0.50f, 0.48f), (0.92f, 0.92f, 0.94f)];
            float at = Math.Clamp(t, 0, 0.999f) * (stops.Length - 1);
            int i = (int)at;
            float k = at - i;
            var (r0, g0, b0) = stops[i];
            var (r1, g1, b1) = stops[i + 1];
            return (r0 + ((r1 - r0) * k), g0 + ((g1 - g0) * k), b0 + ((b1 - b0) * k));
        }

        private static int Byte(float v) => (int)Math.Clamp(v * 255, 0, 255);
    }
}
