using UnityEngine;

/// <summary>
/// Düşmanın üstündeki küçük can çubuğu. Hasar alınca dolgu düşer, kaybedilen
/// kısım kısa süre açık renkte kalıp silinir. Her zaman kameraya bakar.
///
/// Dolgu, sol kenarda duran bir pivotun x ölçeğiyle büyütülüp küçültülüyor;
/// böylece soldan sağa dolan bir çubuk elde ediliyor.
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Parçalar")]
    [SerializeField, Tooltip("Dolgunun x ölçeği değiştirilen pivot.")]
    private Transform fillPivot;
    [SerializeField, Tooltip("Kaybedilen kısmı gösteren hayalet pivot.")]
    private Transform ghostPivot;
    [SerializeField, Tooltip("Çubuğun tamamını gizlemek için kök.")]
    private Transform visual;

    [Header("Davranış")]
    [SerializeField, Tooltip("Kaybedilen kısmın ekranda kalma süresi (saniye).")]
    private float ghostHold = 0.35f;
    [SerializeField, Tooltip("Kaybedilen kısmın silinme süresi (saniye).")]
    private float ghostShrink = 0.25f;
    [SerializeField, Tooltip("Can tamsa çubuğu gizle.")]
    private bool hideWhenFull = true;

    private EnemyHealth health;
    private Transform cameraTransform;

    private float shownFraction = 1f;
    private float ghostFraction = 1f;
    private float ghostTimer;

    private void Awake()
    {
        if (visual == null) visual = transform;
        if (health == null) health = GetComponentInParent<EnemyHealth>();
    }

    /// <summary>Boss gibi özel durumlar için can kaynağı dışarıdan verilebilir.</summary>
    public void SetSource(EnemyHealth source)
    {
        health = source;
    }

    private void LateUpdate()
    {
        if (health == null)
        {
            if (visual != null) visual.gameObject.SetActive(false);
            return;
        }

        float max = Mathf.Max(1, health.Max);
        float fraction = Mathf.Clamp01(health.Current / max);

        // visual kökün kendisi olursa SetActive bu bileşeni de kapatır ve
        // LateUpdate bir daha çalışmaz; bu yüzden yalnızca alt nesne gizlenir.
        bool visible = !hideWhenFull || fraction < 0.999f;
        if (visual != null && visual != transform && visual.gameObject.activeSelf != visible)
        {
            visual.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            shownFraction = fraction;
            ghostFraction = fraction;
            return;
        }

        // Can düştüyse hayalet kısım eski değerde kalır.
        if (fraction < shownFraction - 0.0001f)
        {
            ghostFraction = shownFraction;
            ghostTimer = 0f;
        }

        shownFraction = fraction;

        ghostTimer += Time.unscaledDeltaTime;
        if (ghostTimer > ghostHold)
        {
            float t = Mathf.Clamp01((ghostTimer - ghostHold) / Mathf.Max(0.01f, ghostShrink));
            ghostFraction = Mathf.Lerp(ghostFraction, shownFraction, t);
        }

        Apply(fillPivot, shownFraction);
        Apply(ghostPivot, Mathf.Max(ghostFraction, shownFraction));

        FaceCamera();
    }

    private static void Apply(Transform pivot, float fraction)
    {
        if (pivot == null) return;

        Vector3 scale = pivot.localScale;
        scale.x = Mathf.Max(0.0001f, fraction);
        pivot.localScale = scale;
    }

    private void FaceCamera()
    {
        if (cameraTransform == null)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            cameraTransform = cam.transform;
        }

        transform.rotation = cameraTransform.rotation;
    }
}
