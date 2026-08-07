using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pulsevania.Core
{
    public class AdManager : MonoBehaviour
    {
        public static AdManager Instance { get; private set; }

        [Header("Settings")]
        [Tooltip("True ise tüm reklamlar devre dışı bırakılır, reklam butonlarına basıldığında reklam izlenmeden direkt ödül verilir.")]
        public bool disableAllAds = true;

        // Main Thread Queue for Callbacks
        private readonly List<Action> _executionQueue = new List<Action>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnLoad()
        {
            if (Instance == null)
            {
                GameObject adManagerGo = new GameObject("AdManager");
                Instance = adManagerGo.AddComponent<AdManager>();
                DontDestroyOnLoad(adManagerGo);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            lock (_executionQueue)
            {
                if (_executionQueue.Count > 0)
                {
                    for (int i = 0; i < _executionQueue.Count; i++)
                    {
                        _executionQueue[i]?.Invoke();
                    }
                    _executionQueue.Clear();
                }
            }
        }

        public void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Add(action);
            }
        }

        #region Interstitial Ad (Bölüm Geçiş Reklamı - Instant Reward / No-Ad)

        public void LoadInterstitialAd()
        {
            // No-op (AdMob SDK removed)
        }

        public void ShowInterstitialAd(Action onAdClosed)
        {
            Debug.Log("[AdManager] ShowInterstitialAd called. Ads are removed, resuming immediately.");
            onAdClosed?.Invoke();
        }

        #endregion

        #region Rewarded Ad (Savepoint'te Doğma - Instant Reward / No-Ad)

        public void LoadRewardedAd()
        {
            // No-op (AdMob SDK removed)
        }

        public void ShowRewardedAd(Action onRewardEarned, Action onAdClosed)
        {
            Debug.Log("[AdManager] ShowRewardedAd called. Granting reward immediately without ad.");
            onRewardEarned?.Invoke();
        }

        #endregion

        #region Rewarded Interstitial Ad (Market / Gold Kazanma - Instant Reward / No-Ad)

        public void LoadRewardedInterstitialAd()
        {
            // No-op (AdMob SDK removed)
        }

        public void ShowRewardedInterstitialAd(Action onRewardEarned, Action onAdClosed)
        {
            Debug.Log("[AdManager] ShowRewardedInterstitialAd called. Granting reward immediately without ad.");
            onRewardEarned?.Invoke();
        }

        #endregion
    }
}
