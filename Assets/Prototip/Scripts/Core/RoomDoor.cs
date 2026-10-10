using System;
using UnityEngine;

/// <summary>
/// Odanın çıkış kapısı. Oda temizlenene kadar kapalıdır: boşluğu dolduran bir
/// blok durur ve çerçeve koyu kırmızı yanar. Açılınca blok kalkar, çerçeve
/// parlak yeşile döner ve oyuncu boşluğa girdiğinde geçiş haber verilir.
/// </summary>
public class RoomDoor : MonoBehaviour
{
    [Header("Parçalar")]
    [SerializeField, Tooltip("Kapalıyken boşluğu dolduran blok (collider'ı ile birlikte).")]
    private GameObject blocker;
    [SerializeField, Tooltip("Durum rengini gösterecek çerçeve görselleri.")]
    private Renderer[] frameRenderers;
    [SerializeField, Tooltip("Oyuncunun geçtiğini anlamak için eşik noktası.")]
    private Transform passPoint;

    [Header("Geçiş")]
    [SerializeField, Tooltip("Oyuncu eşik noktasına bu kadar yaklaşınca geçmiş sayılır (birim).")]
    private float passRadius = 2f;

    [Header("Renkler")]
    [SerializeField, Tooltip("Kapalı durum rengi.")]
    private Color closedColor = new Color(0.32f, 0.05f, 0.06f);
    [SerializeField, Tooltip("Kapalı durum parlaması.")]
    private Color closedEmission = new Color(0.22f, 0.02f, 0.03f);
    [SerializeField, Tooltip("Açık durum rengi.")]
    private Color openColor = new Color(0.35f, 0.95f, 0.5f);
    [SerializeField, Tooltip("Açık durum parlaması.")]
    private Color openEmission = new Color(0.3f, 1.3f, 0.5f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int LegacyColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private Material[] materials;
    private Transform playerTransform;
    private bool open;
    private bool passed;

    public bool IsOpen => open;

    /// <summary>Oyuncu kapıdan geçtiğinde bir kez tetiklenir.</summary>
    public event Action OnPassed;

    private void Awake()
    {
        CacheMaterials();
        SetOpen(false);
    }

    private void CacheMaterials()
    {
        if (frameRenderers == null) return;

        System.Collections.Generic.List<Material> list = new System.Collections.Generic.List<Material>();
        for (int i = 0; i < frameRenderers.Length; i++)
        {
            if (frameRenderers[i] == null) continue;

            // Her kapı kendi materyal örneğini kullansın ki diğerleri etkilenmesin.
            Material[] mats = frameRenderers[i].materials;
            for (int j = 0; j < mats.Length; j++)
            {
                if (mats[j] != null) list.Add(mats[j]);
            }
        }

        materials = list.ToArray();
    }

    /// <summary>Odaya girildiğinde oyuncu referansı buradan verilir.</summary>
    public void SetPlayer(Transform player)
    {
        playerTransform = player;
    }

    public void SetOpen(bool value)
    {
        open = value;
        passed = false;

        if (blocker != null) blocker.SetActive(!value);
        ApplyColor();
    }

    private void Update()
    {
        if (!open || passed || playerTransform == null || passPoint == null) return;

        Vector3 d = playerTransform.position - passPoint.position;
        d.y = 0f;

        if (d.sqrMagnitude > passRadius * passRadius) return;

        passed = true;
        OnPassed?.Invoke();
    }

    private void ApplyColor()
    {
        if (materials == null) return;

        Color c = open ? openColor : closedColor;
        Color e = open ? openEmission : closedEmission;

        for (int i = 0; i < materials.Length; i++)
        {
            Material m = materials[i];
            if (m == null) continue;

            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, c);
            if (m.HasProperty(LegacyColorId)) m.SetColor(LegacyColorId, c);
            if (m.HasProperty(EmissionColorId)) m.SetColor(EmissionColorId, e);
        }
    }
}
