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
                if (_instance == null) Debug.LogError("Singleton instance has not been created yet!");
                return _instance;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            this.LoadInstance();
        }

        protected virtual void LoadInstance()
        {
            if (_instance == null)
            {
                _instance = this as T;
                if (transform.parent == null) DontDestroyOnLoad(gameObject);
                return;
            }

            if (_instance != this)
            {
                Debug.LogWarning($"Another instance of {typeof(T).Name} already exists! Destroying the new one.");
                Destroy(gameObject);
            }
        }
    }
}
