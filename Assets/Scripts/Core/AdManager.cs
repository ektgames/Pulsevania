using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using UnityEngine;

namespace Pulsevania.Core
{
    public class AdManager : MonoBehaviour
    {
        public static AdManager Instance { get; private set; }

        [Header("Ad Unit IDs (Real iOS)")]
        [SerializeField] private string interstitialAdUnitId = "ca-app-pub-3721107869675046/3886012910";
        [SerializeField] private string rewardedAdUnitId = "ca-app-pub-3721107869675046/9922983828";
        [SerializeField] private string rewardedInterstitialAdUnitId = "ca-app-pub-3721107869675046/7488392175";

        [Header("Ad Unit IDs (Google iOS Test)")]
        [SerializeField] private string testInterstitialAdUnitId = "ca-app-pub-3940256099942544/4411468910";
        [SerializeField] private string testRewardedAdUnitId = "ca-app-pub-3940256099942544/1712485313";
        [SerializeField] private string testRewardedInterstitialAdUnitId = "ca-app-pub-3940256099942544/6978759866";

        [Header("Settings")]
        [Tooltip("True ise Google'ın resmi iOS test reklam kimlikleri kullanılır. Unity Editor'de de test modu aktif olacaktır.")]
        public bool useTestAds = true;
        [Tooltip("True ise tüm AdMob reklamları devre dışı bırakılır, reklam butonlarına basıldığında reklam izlenmeden direkt ödül verilir.")]
        public bool disableAllAds = true;

        private InterstitialAd interstitialAd;
        private RewardedAd rewardedAd;
        private RewardedInterstitialAd rewardedInterstitialAd;

        // Main Thread Queue for AdMob Callbacks
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

        private void Start()
        {
            if (disableAllAds)
            {
                Debug.Log("[AdManager] Ads are currently disabled (disableAllAds = true). Ad buttons will grant rewards immediately without showing ads.");
                return;
            }

            Debug.Log("[AdManager] Google Mobile Ads initializing...");
            MobileAds.Initialize((InitializationStatus initStatus) =>
            {
                Enqueue(() =>
                {
                    Debug.Log("[AdManager] Google Mobile Ads Initialized.");
                    LoadInterstitialAd();
                    LoadRewardedAd();
                    LoadRewardedInterstitialAd();
                });
            });
        }

        private void Update()
        {
            // Run queued AdMob callbacks on main Unity thread
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

        private string GetInterstitialAdUnitId()
        {
            return useTestAds ? testInterstitialAdUnitId : interstitialAdUnitId;
        }

        private string GetRewardedAdUnitId()
        {
            return useTestAds ? testRewardedAdUnitId : rewardedAdUnitId;
        }

        private string GetRewardedInterstitialAdUnitId()
        {
            return useTestAds ? testRewardedInterstitialAdUnitId : rewardedInterstitialAdUnitId;
        }

        #region Interstitial Ad (Bölüm Geçiş Reklamı)

        public void LoadInterstitialAd()
        {
            if (interstitialAd != null)
            {
                interstitialAd.Destroy();
                interstitialAd = null;
            }

            Debug.Log("[AdManager] Loading Interstitial Ad...");
            var adRequest = new AdRequest();

            InterstitialAd.Load(GetInterstitialAdUnitId(), adRequest, (InterstitialAd ad, LoadAdError error) =>
            {
                Enqueue(() =>
                {
                    if (error != null || ad == null)
                    {
                        Debug.LogError("[AdManager] Interstitial ad failed to load: " + error);
                        return;
                    }

                    Debug.Log("[AdManager] Interstitial ad loaded successfully.");
                    interstitialAd = ad;
                });
            });
        }

        public void ShowInterstitialAd(Action onAdClosed)
        {
            if (disableAllAds)
            {
                Debug.Log("[AdManager] Ads disabled (disableAllAds = true). Skipping Interstitial Ad.");
                onAdClosed?.Invoke();
                return;
            }

            if (interstitialAd != null && interstitialAd.CanShowAd())
            {
                Debug.Log("[AdManager] Showing Interstitial Ad.");

                interstitialAd.OnAdFullScreenContentClosed += () =>
                {
                    Enqueue(() =>
                    {
                        Debug.Log("[AdManager] Interstitial ad content closed.");
                        LoadInterstitialAd();
                        onAdClosed?.Invoke();
                    });
                };

                interstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
                {
                    Enqueue(() =>
                    {
                        Debug.LogError("[AdManager] Interstitial ad failed to show: " + error);
                        LoadInterstitialAd();
                        onAdClosed?.Invoke();
                    });
                };

                interstitialAd.Show();
            }
            else
            {
                Debug.LogWarning("[AdManager] Interstitial ad not ready. Skipping/Resuming directly.");
                onAdClosed?.Invoke();
                LoadInterstitialAd();
            }
        }

        #endregion

        #region Rewarded Ad (Savepoint'te Doğma)

        public void LoadRewardedAd()
        {
            if (rewardedAd != null)
            {
                rewardedAd.Destroy();
                rewardedAd = null;
            }

            Debug.Log("[AdManager] Loading Rewarded Ad...");
            var adRequest = new AdRequest();

            RewardedAd.Load(GetRewardedAdUnitId(), adRequest, (RewardedAd ad, LoadAdError error) =>
            {
                Enqueue(() =>
                {
                    if (error != null || ad == null)
                    {
                        Debug.LogError("[AdManager] Rewarded ad failed to load: " + error);
                        return;
                    }

                    Debug.Log("[AdManager] Rewarded ad loaded successfully.");
                    rewardedAd = ad;
                });
            });
        }

        public void ShowRewardedAd(Action onRewardEarned, Action onAdClosed)
        {
            if (disableAllAds)
            {
                Debug.Log("[AdManager] Ads disabled (disableAllAds = true). Granting Rewarded Ad reward immediately.");
                onRewardEarned?.Invoke();
                return;
            }

#if UNITY_EDITOR
            Debug.Log("[AdManager] Editor simulated rewarded ad started. Waiting 3 seconds...");
            StartCoroutine(EditorAdSimulationRoutine(onRewardEarned));
            return;
#endif
            if (rewardedAd != null && rewardedAd.CanShowAd())
            {
                Debug.Log("[AdManager] Showing Rewarded Ad.");
                bool earnedReward = false;

                rewardedAd.OnAdFullScreenContentClosed += () =>
                {
                    Enqueue(() =>
                    {
                        Debug.Log("[AdManager] Rewarded ad closed.");
                        LoadRewardedAd();
                        if (earnedReward)
                        {
                            onRewardEarned?.Invoke();
                        }
                        else
                        {
                            onAdClosed?.Invoke();
                        }
                    });
                };

                rewardedAd.OnAdFullScreenContentFailed += (AdError error) =>
                {
                    Enqueue(() =>
                    {
                        Debug.LogError("[AdManager] Rewarded ad failed to show: " + error);
                        LoadRewardedAd();
                        onAdClosed?.Invoke();
                    });
                };

                rewardedAd.Show((Reward reward) =>
                {
                    Enqueue(() =>
                    {
                        Debug.Log("[AdManager] Rewarded ad reward earned: " + reward.Type + " Amount: " + reward.Amount);
                        earnedReward = true;
                    });
                });
            }
            else
            {
                Debug.LogWarning("[AdManager] Rewarded ad not ready.");
                onAdClosed?.Invoke();
                LoadRewardedAd();
            }
        }

        #endregion

        #region Rewarded Interstitial Ad (Market / Gold Kazanma)

        public void LoadRewardedInterstitialAd()
        {
            if (rewardedInterstitialAd != null)
            {
                rewardedInterstitialAd.Destroy();
                rewardedInterstitialAd = null;
            }

            Debug.Log("[AdManager] Loading Rewarded Interstitial Ad...");
            var adRequest = new AdRequest();

            RewardedInterstitialAd.Load(GetRewardedInterstitialAdUnitId(), adRequest, (RewardedInterstitialAd ad, LoadAdError error) =>
            {
                Enqueue(() =>
                {
                    if (error != null || ad == null)
                    {
                        Debug.LogError("[AdManager] Rewarded Interstitial ad failed to load: " + error);
                        return;
                    }

                    Debug.Log("[AdManager] Rewarded Interstitial ad loaded successfully.");
                    rewardedInterstitialAd = ad;
                });
            });
        }

        public void ShowRewardedInterstitialAd(Action onRewardEarned, Action onAdClosed)
        {
            if (disableAllAds)
            {
                Debug.Log("[AdManager] Ads disabled (disableAllAds = true). Granting Rewarded Interstitial Ad reward immediately.");
                onRewardEarned?.Invoke();
                return;
            }

            if (rewardedInterstitialAd != null && rewardedInterstitialAd.CanShowAd())
            {
                Debug.Log("[AdManager] Showing Rewarded Interstitial Ad.");
                bool earnedReward = false;

                rewardedInterstitialAd.OnAdFullScreenContentClosed += () =>
                {
                    Enqueue(() =>
                    {
                        Debug.Log("[AdManager] Rewarded Interstitial closed.");
                        LoadRewardedInterstitialAd();
                        if (earnedReward)
                        {
                            onRewardEarned?.Invoke();
                        }
                        else
                        {
                            onAdClosed?.Invoke();
                        }
                    });
                };

                rewardedInterstitialAd.OnAdFullScreenContentFailed += (AdError error) =>
                {
                    Enqueue(() =>
                    {
                        Debug.LogError("[AdManager] Rewarded Interstitial failed to show: " + error);
                        LoadRewardedInterstitialAd();
                        onAdClosed?.Invoke();
                    });
                };

                rewardedInterstitialAd.Show((Reward reward) =>
                {
                    Enqueue(() =>
                    {
                        Debug.Log("[AdManager] Rewarded Interstitial reward earned: " + reward.Type + " Amount: " + reward.Amount);
                        earnedReward = true;
                    });
                });
            }
            else
            {
                Debug.LogWarning("[AdManager] Rewarded Interstitial ad not ready.");
                onAdClosed?.Invoke();
                LoadRewardedInterstitialAd();
            }
        }

#if UNITY_EDITOR
        private System.Collections.IEnumerator EditorAdSimulationRoutine(Action onRewardEarned)
        {
            float elapsed = 0f;
            bool isTR = PlayerPrefs.GetString("GameLanguage", "Turkish") == "Turkish";
            
            while (elapsed < 3f)
            {
                int remaining = Mathf.CeilToInt(3f - elapsed);
                GameObject player = GameObject.FindWithTag("Player");
                Vector3 spawnPos = player != null ? player.transform.position + Vector3.up * 2f : Vector3.zero;
                
                if (DamageTextPool.Instance != null)
                {
                    DamageTextPool.Instance.SpawnText(spawnPos, isTR ? $"[REKLAM SİMÜLASYONU] Geri Sayım: {remaining} sn" : $"[AD SIMULATION] Countdown: {remaining}s", Color.yellow);
                }
                
                yield return new WaitForSecondsRealtime(1f);
                elapsed += 1f;
            }
            
            Debug.Log("[AdManager] Editor simulated rewarded ad completed.");
            onRewardEarned?.Invoke();
        }
#endif

        #endregion
    }
}
