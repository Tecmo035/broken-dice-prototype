using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Buff sembollerini KODLA üretir; hiçbir görsel dosya kullanılmaz.
/// Her buff için küçük bir Texture2D'ye basit şekiller çizilir ve önbelleğe alınır.
///
/// Semboller birbirinden ayırt edilebilsin diye kasıtlı olarak kaba:
///   Çabuk El      → üç hız çizgisi
///   Güdüm         → kavisli ok
///   Patlama       → yıldız patlaması (sekiz ışın)
///   Sekme         → kırık ok
///   İkinci Şans   → dairesel ok
///   Hayalet Zar   → kesik çizgili kare
///   Zincir        → iç içe iki halka
///   Yıkım         → kalın çarpı
/// </summary>
public static class BuffIcons
{
    private const int Size = 32;

    private static readonly Dictionary<BuffId, Texture2D> cache = new Dictionary<BuffId, Texture2D>();
    private static Texture2D emptySlot;

    /// <summary>Buff'ın sembolü; ilk istekte üretilir, sonra önbellekten döner.</summary>
    public static Texture2D Get(BuffId id)
    {
        if (cache.TryGetValue(id, out Texture2D cached) && cached != null) return cached;

        Texture2D tex = Build(id);
        cache[id] = tex;
        return tex;
    }

    /// <summary>Boş slotun soluk çerçevesi.</summary>
    public static Texture2D EmptySlot()
    {
        if (emptySlot != null) return emptySlot;

        emptySlot = NewTexture();
        Frame(emptySlot, new Color(1f, 1f, 1f, 0.22f), 2);
        emptySlot.Apply();
        return emptySlot;
    }

    private static Texture2D Build(BuffId id)
    {
        Texture2D tex = NewTexture();
        Color c = Color.white;

        switch (id)
        {
            case BuffId.QuickHand:
                // Üç yatay hız çizgisi, soldan sağa uzayan.
                Line(tex, 6, 22, 20, 22, c, 3);
                Line(tex, 4, 16, 26, 16, c, 3);
                Line(tex, 6, 10, 20, 10, c, 3);
                break;

            case BuffId.Homing:
                // Kavisli ok: çeyrek yay ve ucunda başlık.
                Arc(tex, 8, 8, 16, 10f, 85f, c, 3);
                Line(tex, 24, 24, 18, 22, c, 3);
                Line(tex, 24, 24, 22, 17, c, 3);
                break;

            case BuffId.Blast:
                // Yıldız patlaması: merkezden sekiz ışın.
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f * Mathf.Deg2Rad;
                    int x = 16 + Mathf.RoundToInt(Mathf.Cos(a) * 12f);
                    int y = 16 + Mathf.RoundToInt(Mathf.Sin(a) * 12f);
                    Line(tex, 16, 16, x, y, c, 2);
                }
                Disc(tex, 16, 16, 4, c);
                break;

            case BuffId.Bounce:
                // Kırık ok: duvara çarpıp yön değiştiren çizgi.
                Line(tex, 4, 6, 16, 20, c, 3);
                Line(tex, 16, 20, 28, 8, c, 3);
                Line(tex, 28, 8, 22, 9, c, 3);
                Line(tex, 28, 8, 27, 15, c, 3);
                break;

            case BuffId.SecondChance:
                // Dairesel ok: neredeyse tam halka ve ucunda başlık.
                Arc(tex, 16, 16, 11, 40f, 330f, c, 3);
                Line(tex, 24, 22, 19, 21, c, 3);
                Line(tex, 24, 22, 24, 16, c, 3);
                break;

            case BuffId.Ghost:
                // Kesik çizgili kare: duvarı olmayan kare.
                DashedFrame(tex, 6, 6, 26, 26, c, 3, 4);
                break;

            case BuffId.Chain:
                // İç içe iki halka.
                Ring(tex, 12, 16, 7, c, 3);
                Ring(tex, 21, 16, 7, c, 3);
                break;

            default:
                // Yıkım: kalın çarpı.
                Line(tex, 6, 6, 26, 26, c, 5);
                Line(tex, 26, 6, 6, 26, c, 5);
                break;
        }

        tex.Apply();
        return tex;
    }

    // --- Doku ve çizim yardımcıları --------------------------------------

    private static Texture2D NewTexture()
    {
        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32 clear = new Color32(255, 255, 255, 0);
        Color32[] pixels = new Color32[Size * Size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        tex.SetPixels32(pixels);

        return tex;
    }

    private static void Plot(Texture2D tex, int x, int y, Color c, int thickness)
    {
        int r = Mathf.Max(1, thickness) / 2;

        for (int oy = -r; oy <= r; oy++)
        {
            for (int ox = -r; ox <= r; ox++)
            {
                int px = x + ox;
                int py = y + oy;
                if (px < 0 || py < 0 || px >= Size || py >= Size) continue;
                tex.SetPixel(px, py, c);
            }
        }
    }

    private static void Line(Texture2D tex, int x0, int y0, int x1, int y1, Color c, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int steps = Mathf.Max(1, Mathf.Max(dx, dy));

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            Plot(tex, x, y, c, thickness);
        }
    }

    private static void Arc(Texture2D tex, int cx, int cy, int radius, float fromDeg, float toDeg, Color c, int thickness)
    {
        int steps = 48;
        for (int i = 0; i <= steps; i++)
        {
            float a = Mathf.Lerp(fromDeg, toDeg, (float)i / steps) * Mathf.Deg2Rad;
            int x = cx + Mathf.RoundToInt(Mathf.Cos(a) * radius);
            int y = cy + Mathf.RoundToInt(Mathf.Sin(a) * radius);
            Plot(tex, x, y, c, thickness);
        }
    }

    private static void Ring(Texture2D tex, int cx, int cy, int radius, Color c, int thickness)
    {
        Arc(tex, cx, cy, radius, 0f, 360f, c, thickness);
    }

    private static void Disc(Texture2D tex, int cx, int cy, int radius, Color c)
    {
        for (int y = -radius; y <= radius; y++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                if (x * x + y * y > radius * radius) continue;
                Plot(tex, cx + x, cy + y, c, 1);
            }
        }
    }

    private static void Frame(Texture2D tex, Color c, int thickness)
    {
        Line(tex, 1, 1, Size - 2, 1, c, thickness);
        Line(tex, 1, Size - 2, Size - 2, Size - 2, c, thickness);
        Line(tex, 1, 1, 1, Size - 2, c, thickness);
        Line(tex, Size - 2, 1, Size - 2, Size - 2, c, thickness);
    }

    private static void DashedFrame(Texture2D tex, int x0, int y0, int x1, int y1, Color c, int thickness, int dash)
    {
        DashedLine(tex, x0, y0, x1, y0, c, thickness, dash);
        DashedLine(tex, x0, y1, x1, y1, c, thickness, dash);
        DashedLine(tex, x0, y0, x0, y1, c, thickness, dash);
        DashedLine(tex, x1, y0, x1, y1, c, thickness, dash);
    }

    private static void DashedLine(Texture2D tex, int x0, int y0, int x1, int y1, Color c, int thickness, int dash)
    {
        int dx = Mathf.Abs(x1 - x0);
        int dy = Mathf.Abs(y1 - y0);
        int steps = Mathf.Max(1, Mathf.Max(dx, dy));
        int period = Mathf.Max(2, dash * 2);

        for (int i = 0; i <= steps; i++)
        {
            if (i % period >= dash) continue;

            float t = (float)i / steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            Plot(tex, x, y, c, thickness);
        }
    }
}
