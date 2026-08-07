using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Pulsevania.Core
{
    public class DamageText : MonoBehaviour
    {
        private TextMesh textMesh;
        private float fadeSpeed = 2f;
        private float floatSpeed = 1.5f;
        private float lifeTime = 0.8f;
        private Color startColor;
        private System.Action<DamageText> returnAction;

        private void Awake()
        {
            textMesh = GetComponent<TextMesh>();
            if (textMesh == null)
            {
                textMesh = gameObject.AddComponent<TextMesh>();
                textMesh.anchor = TextAnchor.MiddleCenter;
                textMesh.alignment = TextAlignment.Center;
                textMesh.fontSize = 24;
                textMesh.characterSize = 0.1f;
            }
        }

        public void Initialize(string text, Color color, System.Action<DamageText> onComplete)
        {
            if (textMesh == null)
            {
                textMesh = GetComponent<TextMesh>();
                if (textMesh == null)
                {
                    textMesh = gameObject.AddComponent<TextMesh>();
                    textMesh.anchor = TextAnchor.MiddleCenter;
                    textMesh.alignment = TextAlignment.Center;
                    textMesh.fontSize = 24;
                    textMesh.characterSize = 0.1f;
                }
            }
            textMesh.text = text;
            textMesh.color = color;
            startColor = color;
            returnAction = onComplete;
            StopAllCoroutines();
            StartCoroutine(AnimateRoutine());
        }

        private IEnumerator AnimateRoutine()
        {
            float elapsed = 0f;
            Color curColor = startColor;

            while (elapsed < lifeTime)
            {
                transform.Translate(Vector3.up * floatSpeed * Time.deltaTime);

                float alpha = Mathf.Lerp(1f, 0f, elapsed / lifeTime);
                curColor.a = alpha;
                textMesh.color = curColor;

                elapsed += Time.deltaTime;
                yield return null;
            }

            returnAction?.Invoke(this);
        }
    }

    public class DamageTextPool : MonoBehaviour
    {
        public static DamageTextPool Instance { get; private set; }

        [SerializeField] private GameObject customPrefab; // Optional custom prefab
        [SerializeField] private int initialPoolSize = 10;

        private Queue<DamageText> pool = new Queue<DamageText>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializePool();
        }

        private void InitializePool()
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                CreateNewInstance();
            }
        }

        private DamageText CreateNewInstance()
        {
            GameObject go;
            if (customPrefab != null)
            {
                go = Instantiate(customPrefab, transform);
            }
            else
            {
                go = new GameObject("DamageText_Instance");
                go.transform.SetParent(transform);
                TextMesh tm = go.AddComponent<TextMesh>();
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.fontSize = 28;
                tm.characterSize = 0.08f;
                // Add a bold standard font if possible, default works fine
            }

            DamageText textComp = go.GetComponent<DamageText>() ?? go.AddComponent<DamageText>();
            go.SetActive(false);
            pool.Enqueue(textComp);
            return textComp;
        }

        public void SpawnText(Vector3 position, string text, Color color)
        {
            DamageText instance;
            if (pool.Count > 0)
            {
                instance = pool.Dequeue();
            }
            else
            {
                instance = CreateNewInstance();
                pool.Dequeue(); // remove the newly added one from the queue
            }

            instance.transform.position = position;
            instance.gameObject.SetActive(true);
            instance.Initialize(text, color, ReturnToPool);
        }

        private void ReturnToPool(DamageText instance)
        {
            instance.gameObject.SetActive(false);
            pool.Enqueue(instance);
        }
    }
}
