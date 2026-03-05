using UnityEngine;

namespace BlockSystem.Core
{
    // ══════════════════════════════════════════════════════════════════════
    //  CoroutineRunner  —  a lightweight singleton MonoBehaviour that exists
    //                      solely to host coroutines for non-MonoBehaviour code.
    //
    //  Why it exists:
    //    DelayBlock needs to start a Unity coroutine, but blocks are plain C#
    //    objects (not MonoBehaviours).  You can't call StartCoroutine without
    //    a MonoBehaviour.  Calling FindObjectOfType<MonoBehaviour>() would
    //    grab a random object that might be disabled or destroyed mid-game.
    //
    //    CoroutineRunner creates a dedicated hidden GameObject marked
    //    DontDestroyOnLoad, so coroutines survive scene changes and never
    //    get accidentally killed by gameplay code.
    //
    //  Usage:
    //    CoroutineRunner.Instance.StartCoroutine(MyCoroutine());
    //
    //  You never need to create this yourself.  The Instance getter
    //  auto-creates it on first access.
    // ══════════════════════════════════════════════════════════════════════
    public class CoroutineRunner : MonoBehaviour
    {
        static CoroutineRunner _instance;

        /// <summary>
        /// Returns the singleton.  Creates the hidden GameObject on first call.
        /// Safe to call from any thread that can touch Unity APIs (main thread).
        /// </summary>
        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[BlockSystem] CoroutineRunner");
                    go.hideFlags = HideFlags.HideAndDontSave;

                    // DontDestroyOnLoad only works in play mode.
                    // When running from the editor toolbar we skip it —
                    // HideAndDontSave already keeps the object alive.
                    if (Application.isPlaying)
                        DontDestroyOnLoad(go);

                    _instance = go.AddComponent<CoroutineRunner>();
                }

                return _instance;
            }
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
