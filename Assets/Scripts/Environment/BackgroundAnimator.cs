using UnityEngine;

namespace Pulsevania.Core
{
    public class BackgroundAnimator : MonoBehaviour
    {
        public enum AnimationType { Rotate, ScrollVertical }
        public AnimationType type = AnimationType.Rotate;
        public float speed = 1.0f;
        
        private MeshRenderer meshRenderer;

        private void Start()
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        private void Update()
        {
            if (type == AnimationType.Rotate)
            {
                transform.Rotate(0f, 0f, speed * Time.deltaTime);
            }
            else if (type == AnimationType.ScrollVertical)
            {
                if (meshRenderer != null && meshRenderer.material != null)
                {
                    meshRenderer.material.mainTextureOffset += new Vector2(0f, speed * Time.deltaTime);
                }
            }
        }
    }
}
