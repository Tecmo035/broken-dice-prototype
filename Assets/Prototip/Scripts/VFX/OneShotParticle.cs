using UnityEngine;

/// <summary>
/// Tek seferlik parçacık patlaması. Doğunca oynar, süresi bitince kendini yok eder.
/// Parçacık sistemleri kodla kurulduğu için renk dışarıdan verilebilir.
/// </summary>
public class OneShotParticle : MonoBehaviour
{
    [SerializeField, Tooltip("Nesnenin kendini yok edeceği süre (saniye).")]
    private float lifetime = 2f;

    [SerializeField, Tooltip("Oynatılacak parçacık sistemleri.")]
    private ParticleSystem[] systems;

    private void Awake()
    {
        if (systems == null || systems.Length == 0) systems = GetComponentsInChildren<ParticleSystem>(true);
    }

    private void Start()
    {
        Play();
        Destroy(gameObject, lifetime);
    }

    public void Play()
    {
        if (systems == null) return;
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] != null) systems[i].Play(true);
        }
    }

    /// <summary>Patlamanın rengini çalıştırma anında değiştirir.</summary>
    public void SetColor(Color color)
    {
        if (systems == null) return;
        for (int i = 0; i < systems.Length; i++)
        {
            if (systems[i] == null) continue;
            ParticleSystem.MainModule main = systems[i].main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
        }
    }
}
