using System.Collections.Generic;
using UnityEngine;

namespace LiminalLabs.GameEvents
{
    /// <summary>
    /// Makes catalogs resolvable through <see cref="GameEventRegistry"/> for as long as this
    /// component lives. Put it on a persistent object in the bootstrap scene with the
    /// project's catalog, and from then on a bridge resolves any id - a project asset's or a
    /// loaded level's scene event's - with one call, and never has to know which it was.
    ///
    /// A component rather than something a catalog does on load, because a ScriptableObject
    /// is only loaded when something references it, and "the catalog is active" should be a
    /// fact about the scene that can be seen, disabled and reasoned about.
    /// </summary>
    [AddComponentMenu("Liminal Labs/Game Events/Game Event Catalog Activator")]
    [DisallowMultipleComponent]
    public sealed class GameEventCatalogActivator : MonoBehaviour
    {
        [SerializeField, Tooltip("Catalogs to register while this component is enabled. One per project is the usual shape; a DLC ships its own.")]
        private List<GameEventCatalog> catalogs = new List<GameEventCatalog>();

        [SerializeField, Tooltip("Keep this object across scene loads, so the registry outlives the bootstrap scene.")]
        private bool dontDestroyOnLoad = true;

        /// <summary>The catalogs this activates.</summary>
        public IReadOnlyList<GameEventCatalog> Catalogs => catalogs;

        private void Awake()
        {
            if (dontDestroyOnLoad && transform.parent == null) DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            for (int i = 0; i < catalogs.Count; i++)
            {
                if (catalogs[i] != null) catalogs[i].Activate();
            }
        }

        private void OnDisable()
        {
            for (int i = 0; i < catalogs.Count; i++)
            {
                if (catalogs[i] != null) catalogs[i].Deactivate();
            }
        }
    }
}
