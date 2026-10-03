using UnityEngine;

namespace SaiGame.Services
{
    public abstract class SaiSingleton<T> : SaiBehaviour where T : SaiBehaviour
    {
        private static T _instance;

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
                    if (_instance is SaiSingleton<T> singleton && Application.isPlaying)
                        singleton.LoadInstance();
                }
                return _instance;
            }
        }

        protected override void Awake()
        {
            this.LoadInstance();
            if (_instance != this) return;

            base.Awake();
        }

        protected virtual void LoadInstance()
        {
            if (_instance != null && _instance != this)
            {
                enabled = false;

                // Keep the original instance safe if it shares this object or hierarchy.
                if (_instance.transform.IsChildOf(transform))
                {
                    Destroy(this);
                }
                else
                {
                    gameObject.SetActive(false);
                    Destroy(gameObject);
                }
                return;
            }

            _instance = this as T;
            if (!Application.isPlaying) return;

            if (transform.parent != null) transform.SetParent(null, true);
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
