using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EaWModLauncher;

/// <summary>
/// Lädt Logo, Emblem und Icon zur Laufzeit aus der lokalen Spielinstallation bzw. aus Steam.
/// Der Launcher selbst enthält keine Grafiken des Spiels.
/// </summary>
public static class GameAssets
{
    private const string SplashPath = @"DATA\ART\TEXTURES\SPLASH.DDS";
    private const string EmblemPath = @"DATA\ART\TEXTURES\I_UNDERWORLD_DECAL.DDS";

    /// <summary>Steams Icon-Datei für Empire at War: Gold Pack (clienticon aus den App-Infos).</summary>
    private const string SteamIconFile = "49998b4674a619ea5b3c57504c285aaa153e61ca.ico";

    /// <summary>Logo aus dem FoC-Ladebildschirm und Zann-Konsortium-Emblem. Fehlendes bleibt null.</summary>
    public static (ImageSource? Logo, ImageSource? Emblem) LoadArt(int appId)
    {
        try
        {
            var gameDir = SteamService.FindGameDir(appId);
            if (gameDir == null) return (null, null);
            var meg = Path.Combine(gameDir, "corruption", "Data", "textures.meg");
            if (!File.Exists(meg)) return (null, null);

            var files = ReadFromMeg(meg, SplashPath, EmblemPath);
            ImageSource? logo = null, emblem = null;
            if (files.TryGetValue(SplashPath, out var splash)) logo = Try(() => CutLogo(splash));
            if (files.TryGetValue(EmblemPath, out var decal)) emblem = Try(() => Trim(DecodeDds(decal)));
            return (logo, emblem);
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>Offizielles Steam-Icon des Spiels, ersatzweise das Icon aus swfoc.exe.</summary>
    public static ImageSource? LoadIcon(int appId)
    {
        try
        {
            var steamDir = SteamService.FindSteamDir();
            if (steamDir != null)
            {
                var ico = Path.Combine(steamDir, "steam", "games", SteamIconFile);
                if (File.Exists(ico))
                    return BitmapFrame.Create(new Uri(ico), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            }

            var gameDir = SteamService.FindGameDir(appId);
            if (gameDir != null)
                return IconFromExe(Path.Combine(gameDir, "corruption", "swfoc.exe"));
        }
        catch { }
        return null;
    }

    private static ImageSource? Try(Func<ImageSource?> load)
    {
        try { return load(); } catch { return null; }
    }

    // ---------- MEG-Archive (Petroglyph, Format v1) ----------

    private static Dictionary<string, byte[]> ReadFromMeg(string megPath, params string[] wanted)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var fs = File.OpenRead(megPath);
        using var br = new BinaryReader(fs);

        uint numNames = br.ReadUInt32(), numFiles = br.ReadUInt32();
        if (numNames > 1_000_000 || numFiles > 1_000_000) return result;

        var names = new string[numNames];
        for (int i = 0; i < numNames; i++)
            names[i] = Encoding.ASCII.GetString(br.ReadBytes(br.ReadUInt16()));

        var found = new List<(string Name, uint Size, uint Start)>();
        for (int i = 0; i < numFiles; i++)
        {
            br.ReadUInt32(); br.ReadUInt32();           // CRC, Index
            uint size = br.ReadUInt32(), start = br.ReadUInt32(), nameIndex = br.ReadUInt32();
            if (nameIndex < numNames && wanted.Contains(names[nameIndex], StringComparer.OrdinalIgnoreCase))
                found.Add((names[nameIndex], size, start));
        }

        foreach (var (name, size, start) in found)
        {
            if (result.ContainsKey(name) || start + (long)size > fs.Length) continue;
            fs.Position = start;
            result[name] = br.ReadBytes((int)size);
        }
        return result;
    }

    // ---------- DDS ----------

    private static BitmapSource DecodeDds(byte[] dds)
    {
        // Unkomprimiertes 32-Bit-BGRA (z. B. der FoC-Ladebildschirm) kann WIC nicht lesen – selbst dekodieren.
        if (dds.Length > 128 && Encoding.ASCII.GetString(dds, 0, 4) == "DDS ")
        {
            int height = BitConverter.ToInt32(dds, 12), width = BitConverter.ToInt32(dds, 16);
            uint flags = BitConverter.ToUInt32(dds, 80);
            int bpp = BitConverter.ToInt32(dds, 88);
            uint rMask = BitConverter.ToUInt32(dds, 92), aMask = BitConverter.ToUInt32(dds, 104);
            int stride = width * 4;
            if ((flags & 0x40) != 0 && bpp == 32 && rMask == 0x00FF0000 && dds.Length >= 128 + stride * height)
            {
                var pixels = new byte[stride * height];
                Array.Copy(dds, 128, pixels, 0, pixels.Length);
                var format = aMask != 0 ? PixelFormats.Bgra32 : PixelFormats.Bgr32;
                var bmp = BitmapSource.Create(width, height, 96, 96, format, null, pixels, stride);
                bmp.Freeze();
                return bmp;
            }
        }

        // Komprimiert (DXT1–5): Windows-eigener DDS-Decoder.
        using var ms = new MemoryStream(dds);
        var frame = BitmapDecoder.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        frame.Freeze();
        return frame;
    }

    private static byte[] ToBgra(BitmapSource src, out int width, out int height)
    {
        var bgra = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);
        width = bgra.PixelWidth;
        height = bgra.PixelHeight;
        var pixels = new byte[width * height * 4];
        bgra.CopyPixels(pixels, width * 4, 0);
        return pixels;
    }

    /// <summary>
    /// Schneidet das Logo („Star Wars – Empire at War – Forces of Corruption“) aus dem Ladebildschirm aus
    /// und macht den schwarzen Hintergrund transparent.
    /// </summary>
    private static ImageSource? CutLogo(byte[] splashDds)
    {
        var px = ToBgra(DecodeDds(splashDds), out int w, out int h);
        if (w != 1024 || h != 768) return null;

        // Zeilen 90–375 enthalten das Logo, darunter folgen "Expansion" und "Laden …".
        const int yMin = 90, yMax = 375;
        int minX = w, maxX = 0, minY = h, maxY = 0;
        for (int y = yMin; y <= yMax; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                if (Math.Max(px[i], Math.Max(px[i + 1], px[i + 2])) > 24)
                {
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
            }
        if (maxX <= minX) return null;
        minX = Math.Max(0, minX - 3); maxX = Math.Min(w - 1, maxX + 3);
        minY = Math.Max(yMin, minY - 3); maxY = Math.Min(yMax, maxY + 3);

        int ow = maxX - minX + 1, oh = maxY - minY + 1;
        var output = new byte[ow * oh * 4];
        for (int y = 0; y < oh; y++)
            for (int x = 0; x < ow; x++)
            {
                int s = ((minY + y) * w + minX + x) * 4, d = (y * ow + x) * 4;
                byte b = px[s], g = px[s + 1], r = px[s + 2];
                int a = Math.Max(r, Math.Max(g, b));
                if (a < 8) continue;
                // Vor Schwarz ist die Farbe vormultipliziert – zurückrechnen.
                output[d] = (byte)Math.Min(255, b * 255 / a);
                output[d + 1] = (byte)Math.Min(255, g * 255 / a);
                output[d + 2] = (byte)Math.Min(255, r * 255 / a);
                output[d + 3] = (byte)a;
            }

        var logo = BitmapSource.Create(ow, oh, 96, 96, PixelFormats.Bgra32, null, output, ow * 4);
        logo.Freeze();
        return logo;
    }

    /// <summary>Entfernt den transparenten Rand eines Bildes.</summary>
    private static ImageSource Trim(BitmapSource src)
    {
        var px = ToBgra(src, out int w, out int h);
        int minX = w, maxX = 0, minY = h, maxY = 0;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[(y * w + x) * 4 + 3] > 12)
                {
                    minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
                }
        if (maxX <= minX) return src;

        var cropped = new CroppedBitmap(new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0),
            new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));
        cropped.Freeze();
        return cropped;
    }

    // ---------- Icon aus einer .exe ----------

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, IntPtr[]? large, IntPtr[]? small, uint count);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    private static ImageSource? IconFromExe(string exe)
    {
        if (!File.Exists(exe)) return null;
        var large = new IntPtr[1];
        if (ExtractIconEx(exe, 0, large, null, 1) == 0 || large[0] == IntPtr.Zero) return null;
        try
        {
            var img = Imaging.CreateBitmapSourceFromHIcon(large[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            img.Freeze();
            return img;
        }
        finally
        {
            DestroyIcon(large[0]);
        }
    }
}
