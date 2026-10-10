using System;
using UnityEngine;

/// <summary>
/// Oyunun duraklatılmasının tek sahibi. Sandık paneli gibi ekranlar burayı
/// kullanır; Time.timeScale'e başka hiçbir yer doğrudan 0 yazmaz.
///
/// Hit-stop (kritikteki kısa zaman yavaşlaması) bu bayrağı okur: duraklatma
/// sırasında hiç başlamaz, başlamışsa bitişinde timeScale'i geri yazmaz.
/// Bu olmadan panel açıkken oyun kendiliğinden devam ederdi.
/// </summary>
public static class GamePause
{
    /// <summary>Oyun şu anda duraklatılmış mı?</summary>
    public static bool Paused { get; private set; }

    /// <summary>Duraklatma durumunun son değiştiği kare (Time.frameCount).</summary>
    public static int LastChangeFrame { get; private set; } = -1;

    /// <summary>
    /// Duraklatma durumu bu karede mi değişti?
    ///
    /// Aynı E tuşunu iki ayrı bileşen okuyor: PlayerInteractor paneli açar,
    /// ArmoryPanel paneli kapatır. MonoBehaviour Update sırası garanti
    /// olmadığından, sonra çalışan taraf aynı basımı ikinci kez görür ve
    /// paneli açıldığı karede kapatır (ya da kapandığı karede yeniden açar).
    /// Tuşu okuyan her iki taraf da bu bayrak açıkken o kareyi atlar.
    /// </summary>
    public static bool ChangedThisFrame => LastChangeFrame == Time.frameCount;

    /// <summary>Duraklatma durumunu değiştirdiğinde tetiklenir.</summary>
    public static event Action<bool> OnChanged;

    public static void Set(bool paused)
    {
        if (Paused == paused) return;

        Paused = paused;
        LastChangeFrame = Time.frameCount;
        Time.timeScale = paused ? 0f : 1f;
        OnChanged?.Invoke(paused);
    }

    /// <summary>
    /// Koşu geçişlerinde çağrılır: duraklatma ve hit-stop ne durumda olursa olsun
    /// oyunu normal hıza döndürür.
    /// </summary>
    public static void ForceResume()
    {
        bool wasPaused = Paused;
        Paused = false;
        Time.timeScale = 1f;

        if (!wasPaused) return;

        LastChangeFrame = Time.frameCount;
        OnChanged?.Invoke(false);
    }
}
