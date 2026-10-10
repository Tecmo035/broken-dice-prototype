using UnityEngine;

/// <summary>
/// Sabit açılı ortografik takip kamerası. Hedefi yumuşak takip eder ve
/// isabetlerde takip konumunun üzerine geçici bir sarsıntı ekler.
/// </summary>
[RequireComponent(typeof(Camera))]
public class IsometricCamera : MonoBehaviour
{
    [Header("Hedef")]
    [SerializeField, Tooltip("Takip edilen nesne (genelde oyuncu).")]
    private Transform target;

    [Header("Açı ve Mesafe")]
    [SerializeField, Tooltip("Yukarıdan bakış açısı (derece).")]
    private float pitch = 50f;
    [SerializeField, Tooltip("Yatay dönüş açısı (derece).")]
    private float yaw = 45f;
    [SerializeField, Tooltip("Hedefe olan uzaklık.")]
    private float distance = 30f;
    [SerializeField, Tooltip("Ortografik görüş boyutu; küçük değer daha yakın demek.")]
    private float orthographicSize = 9f;

    [Header("Yumuşatma")]
    [SerializeField, Tooltip("Takip yumuşaklığı (saniye). 0 = anında.")]
    private float smoothTime = 0.12f;

    private Camera cam;
    private Vector3 focus;
    private Vector3 focusVelocity;
    private Vector3 shakeOffset;
    private float shakeAmplitude;
    private float shakeDuration;
    private float shakeTimeLeft;
    private bool focusInitialized;

    /// <summary>
    /// Kamera bileşeni. Editörde Awake çalışmadan da çağrılabildiği için
    /// referans gerektiğinde yeniden bulunur.
    /// </summary>
    public Camera Cam
    {
        get
        {
            if (cam == null) cam = GetComponent<Camera>();
            return cam;
        }
    }

    public Transform Target => target;

    /// <summary>Kameranın sabit bakış dönüşü.</summary>
    public Quaternion ViewRotation => Quaternion.Euler(pitch, yaw, 0f);

    private void Awake()
    {
        ApplySettings();
        if (target != null) SnapToTarget();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        if (!focusInitialized)
        {
            focus = target.position;
            focusInitialized = true;
        }

        focus = smoothTime <= 0f
            ? target.position
            : Vector3.SmoothDamp(focus, target.position, ref focusVelocity, smoothTime);

        UpdateShake();
        ApplyTransform();
    }

    /// <summary>Kamera ayarlarını uygular. Editör kurulum scriptinden de çağrılabilir.</summary>
    public void ApplySettings()
    {
        Camera c = Cam;
        if (c == null) return;

        c.orthographic = true;
        c.orthographicSize = orthographicSize;
        c.nearClipPlane = 0.1f;
        c.farClipPlane = 200f;
    }

    public void SetTarget(Transform newTarget, bool snap = true)
    {
        target = newTarget;
        if (snap) SnapToTarget();
    }

    /// <summary>Yumuşatmayı atlayarak hedefin üzerine anında oturur (ışınlanmadan sonra).</summary>
    public void SnapToTarget()
    {
        if (target == null) return;

        focus = target.position;
        focusVelocity = Vector3.zero;
        focusInitialized = true;
        shakeTimeLeft = 0f;
        shakeOffset = Vector3.zero;
        ApplyTransform();
    }

    /// <summary>
    /// Geçici kamera sarsıntısı. Şiddet süre boyunca azalır.
    /// Aynı anda birden fazla çağrı gelirse en güçlüsü kazanır.
    /// </summary>
    public void Shake(float amplitude, float duration)
    {
        if (amplitude <= 0f || duration <= 0f) return;

        float activeAmplitude = shakeTimeLeft > 0f ? shakeAmplitude : 0f;
        float activeDuration = shakeTimeLeft > 0f ? shakeDuration : 0f;

        shakeAmplitude = Mathf.Max(activeAmplitude, amplitude);
        shakeDuration = Mathf.Max(activeDuration, duration);
        shakeTimeLeft = Mathf.Max(shakeTimeLeft, duration);
    }

    private void UpdateShake()
    {
        if (shakeTimeLeft <= 0f)
        {
            shakeOffset = Vector3.zero;
            return;
        }

        // Zaman yavaşlaması (hit-stop) sırasında da sarsıntı aksın diye unscaled süre kullanılıyor.
        shakeTimeLeft -= Time.unscaledDeltaTime;

        float t = shakeDuration <= 0f ? 0f : Mathf.Clamp01(shakeTimeLeft / shakeDuration);
        float a = shakeAmplitude * t * t;

        shakeOffset = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)) * a;
    }

    private void ApplyTransform()
    {
        Quaternion rot = ViewRotation;
        Vector3 pos = focus - rot * Vector3.forward * distance + shakeOffset;
        transform.SetPositionAndRotation(pos, rot);

        Camera c = Cam;
        if (c != null)
        {
            c.orthographic = true;
            c.orthographicSize = orthographicSize;
        }
    }
}
