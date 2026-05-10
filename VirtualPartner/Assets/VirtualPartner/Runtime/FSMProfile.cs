using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    public enum FSMActionType
    {
        PresetAnimation,
        Locomotion
    }

    [CreateAssetMenu(menuName = "VirtualPartner/FSM Profile")]
    public sealed class FSMProfile : ScriptableObject
    {
        [SerializeField] private float idleWaitMin = 10f;
        [SerializeField] private float idleWaitMax = 20f;
        [SerializeField] private FSMActionEntry[] actions = Array.Empty<FSMActionEntry>();

        public float IdleWaitMin => Mathf.Max(0f, Mathf.Min(idleWaitMin, idleWaitMax));
        public float IdleWaitMax => Mathf.Max(IdleWaitMin, Mathf.Max(idleWaitMin, idleWaitMax));
        public IReadOnlyList<FSMActionEntry> Actions => actions ?? Array.Empty<FSMActionEntry>();

        public float GetRandomWaitDuration()
        {
            return UnityEngine.Random.Range(IdleWaitMin, IdleWaitMax);
        }

        public bool TryPickAction(out FSMActionEntry action)
        {
            action = null;

            var entries = Actions;
            var totalWeight = 0f;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.Enabled || entry.Weight <= 0f)
                    continue;

                totalWeight += entry.Weight;
            }

            if (totalWeight <= 0f)
                return false;

            var roll = UnityEngine.Random.Range(0f, totalWeight);
            var accumulated = 0f;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null || !entry.Enabled || entry.Weight <= 0f)
                    continue;

                accumulated += entry.Weight;
                if (roll <= accumulated)
                {
                    action = entry;
                    return true;
                }
            }

            return false;
        }
    }

    [Serializable]
    public sealed class FSMActionEntry
    {
        [SerializeField] private string actionName;
        [SerializeField] private bool enabled = true;
        [SerializeField] private float weight = 1f;
        [SerializeField] private float duration = 1f;
        [SerializeField] private float durationMin;
        [SerializeField] private float durationMax;
        [SerializeField] private FSMActionType actionType;
        [SerializeField] private string animationName;
        [SerializeField] private string locomotionMode;

        public string ActionName => actionName;
        public bool Enabled => enabled;
        public float Weight => Mathf.Max(0f, weight);
        public float Duration => Mathf.Max(0f, duration);
        public float DurationMin => Mathf.Max(0f, Mathf.Min(durationMin, durationMax));
        public float DurationMax => Mathf.Max(DurationMin, Mathf.Max(durationMin, durationMax));
        public FSMActionType ActionType => actionType;
        public string AnimationName => animationName;
        public string LocomotionMode => locomotionMode;

        public float GetDuration()
        {
            if (durationMin > 0f && durationMax > 0f)
                return UnityEngine.Random.Range(DurationMin, DurationMax);

            return Duration;
        }
    }
}
