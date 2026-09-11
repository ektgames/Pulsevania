using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Pulsevania.Core
{
    public enum AnimState
    {
        Idle,
        Walk,
        Jump,
        Attack,
        Hurt,
        Death,
        Cast,
        Spell
    }

    [Preserve]
    public class SpriteAnimator : MonoBehaviour
    {
        // Do not name this AnimationClip — it collides with UnityEngine.AnimationClip
        // and can abort iOS IL2CPP scene load with CachedReader::OutOfBoundsError.
        [Serializable]
        [Preserve]
        public class SpriteAnimationClip
        {
            public AnimState state;
            public Sprite[] frames;
            public float frameRate;
            public bool loop;
        }

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private List<SpriteAnimationClip> clips = new List<SpriteAnimationClip>();

        private Dictionary<AnimState, SpriteAnimationClip> clipDictionary = new Dictionary<AnimState, SpriteAnimationClip>();
        private AnimState currentState = AnimState.Idle;
        private int currentFrame;
        private float frameTimer;
        private bool isLocked;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            
            InitializeDictionary();
        }

        public void InitializeDictionary()
        {
            clipDictionary.Clear();
            foreach (var clip in clips)
            {
                if (!clipDictionary.ContainsKey(clip.state))
                {
                    clipDictionary[clip.state] = clip;
                }
            }
        }

        private void Update()
        {
            if (clipDictionary.Count == 0 || !clipDictionary.ContainsKey(currentState)) return;

            SpriteAnimationClip currentClip = clipDictionary[currentState];
            if (currentClip.frames == null || currentClip.frames.Length <= 1) return;

            frameTimer += Time.deltaTime;
            float interval = 1f / currentClip.frameRate;

            if (frameTimer >= interval)
            {
                frameTimer -= interval;
                currentFrame++;

                if (currentFrame >= currentClip.frames.Length)
                {
                    if (currentClip.loop)
                    {
                        currentFrame = 0;
                    }
                    else
                    {
                        currentFrame = currentClip.frames.Length - 1;
                        isLocked = false; // Release lock at the end of non-looping animation
                    }
                }

                spriteRenderer.sprite = currentClip.frames[currentFrame];
            }
        }

        public void PlayState(AnimState state, bool lockAnim = false)
        {
            // If currently locked, ignore different states except death and hurt
            if (isLocked && state != AnimState.Death && state != AnimState.Hurt)
            {
                if (state != currentState)
                {
                    return;
                }
            }

            if (currentState == state && clipDictionary.ContainsKey(state) && clipDictionary[state].loop) return;

            // Make sure the clip exists
            if (!clipDictionary.ContainsKey(state)) return;

            currentState = state;
            currentFrame = 0;
            frameTimer = 0f;
            isLocked = lockAnim;

            SpriteAnimationClip currentClip = clipDictionary[state];
            if (currentClip.frames != null && currentClip.frames.Length > 0)
            {
                spriteRenderer.sprite = currentClip.frames[0];
            }
        }

        // Editor helper method to build clips
        public void SetClips(List<SpriteAnimationClip> newClips)
        {
            clips = newClips;
            InitializeDictionary();
        }

        public bool TryGetClip(AnimState state, out SpriteAnimationClip clip)
        {
            if (clipDictionary != null && clipDictionary.TryGetValue(state, out clip))
            {
                return true;
            }
            foreach (var c in clips)
            {
                if (c.state == state)
                {
                    clip = c;
                    return true;
                }
            }
            clip = default;
            return false;
        }
    }
}
