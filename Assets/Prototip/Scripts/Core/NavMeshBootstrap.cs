using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// Oyun başında NavMesh'i bir kez pişirir. Ortam kökünde duran tek NavMeshSurface
/// yalnızca kendi alt nesnelerini (zemin, duvarlar, sütunlar) toplar; oyuncu,
/// düşmanlar, kapılar ve eşyalar ayrı köklerde olduğu için pişirmeye girmez.
/// Awake'te çalışır, böylece Start'ta doğan düşmanlar hazır bir NavMesh bulur.
/// </summary>
public class NavMeshBootstrap : MonoBehaviour
{
    [SerializeField, Tooltip("Pişirilecek NavMesh yüzeyi. Boş ise bu nesneden alınır.")]
    private NavMeshSurface surface;

    private void Awake()
    {
        if (surface == null) surface = GetComponent<NavMeshSurface>();

        if (surface == null)
        {
            Debug.LogWarning("[Zar Prototip] NavMeshSurface bulunamadı, düşmanlar hareket edemez.");
            return;
        }

        surface.BuildNavMesh();
    }
}
