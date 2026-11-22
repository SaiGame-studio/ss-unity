using UnityEngine;

namespace SaiGame.LevelPlaySample
{
    // Holds the UnityAds objects outside the SaiServer prefab. SaiServer survives scene loads,
    // so the holder does too; a holder whose AdsManager belongs to a duplicate SaiServer
    // (removed by SaiSingleton when its scene loads again) removes itself.
    [DisallowMultipleComponent]
    public sealed class AdsHolder : MonoBehaviour
    {
        [SerializeField, Tooltip("The AdsManager these ads belong to.")]
        private AdsManager owner;

        public AdsManager Owner => owner;

        private void Awake()
        {
            if (transform.parent != null)
                transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
        }

        // Runs after every Awake of the loaded scene, when a duplicate SaiServer is already deactivated.
        private void Start()
        {
            if (owner == null || !owner.gameObject.activeInHierarchy)
                Destroy(gameObject);
        }
    }
}
