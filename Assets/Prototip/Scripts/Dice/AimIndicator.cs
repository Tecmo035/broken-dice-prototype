using UnityEngine;

/// <summary>
/// Menzil göstergesi: oyuncudan nişan yönüne, menzil uzunluğunda tek bir çizgi.
/// Zar sayısı kaç olursa olsun bir tane çizgi olduğu için zarın kendisinde değil,
/// ayrı bir nesnede duruyor. Elde hiç zar yoksa (oyuncu silahsızken) kaybolur.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class AimIndicator : MonoBehaviour
{
    [Header("Referanslar")]
    [SerializeField, Tooltip("Nişan yönünü veren oyuncu.")]
    private PlayerController player;
    [SerializeField, Tooltip("Elde zar olup olmadığını ve menzili veren havuz.")]
    private DiceManager diceManager;

    [Header("Görünüm")]
    [SerializeField, Tooltip("Çizginin yerden yüksekliği (birim).")]
    private float height = 0.15f;
    [SerializeField, Tooltip("Havuz bulunamazsa kullanılacak yedek menzil (birim).")]
    private float fallbackRange = 12f;

    private LineRenderer line;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
    }

    private void Start()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (diceManager == null) diceManager = FindFirstObjectByType<DiceManager>();
    }

    private void LateUpdate()
    {
        if (line == null) return;

        if (player == null || !ShouldShow())
        {
            line.enabled = false;
            return;
        }

        float range = diceManager != null ? diceManager.Range : fallbackRange;

        Vector3 start = player.transform.position + Vector3.up * height;
        Vector3 end = start + player.AimDirection * range;

        line.enabled = true;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private bool ShouldShow()
    {
        if (GamePause.Paused) return false;

        // Havuz yoksa gösterge yine çizilsin ki kurulum hatası fark edilsin.
        return diceManager == null || diceManager.CanThrow;
    }
}
