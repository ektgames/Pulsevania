using System;
using System.Collections.Generic;
using UnityEngine;

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

    public class SpriteAnimator : MonoBehaviour
    {
        [System.Serializable]
        public struct AnimationClip
        {
            public AnimState state;
            public Sprite[] frames;
            public float frameRate;
            public bool loop;
        }

        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private List<AnimationClip> clips = new List<AnimationClip>();

        private Dictionary<AnimState, AnimationClip> clipDictionary = new Dictionary<AnimState, AnimationClip>();
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

            AnimationClip currentClip = clipDictionary[currentState];
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

            AnimationClip currentClip = clipDictionary[state];
            if (currentClip.frames != null && currentClip.frames.Length > 0)
            {
                spriteRenderer.sprite = currentClip.frames[0];
            }
        }

        // Editor helper method to build clips
        public void SetClips(List<AnimationClip> newClips)
        {
            clips = newClips;
            InitializeDictionary();
        }

        public bool TryGetClip(AnimState state, out AnimationClip clip)
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
