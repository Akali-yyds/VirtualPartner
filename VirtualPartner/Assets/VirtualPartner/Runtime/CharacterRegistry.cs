using System;
using System.Collections.Generic;
using UnityEngine;

namespace VirtualPartner.Runtime
{
    public static class CharacterRegistry
    {
        private static readonly Dictionary<string, CharacterRuntimeContext> contexts =
            new Dictionary<string, CharacterRuntimeContext>(StringComparer.OrdinalIgnoreCase);

        public static int RegisteredCount => contexts.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearForPlayInitialization()
        {
            contexts.Clear();
            Debug.Log("[VirtualPartner] CharacterRegistry cleared for Play initialization.");
        }

        public static bool TryRegister(CharacterRuntimeContext context, out string failureReason)
        {
            if (context == null)
                return Fail("CharacterRuntimeContext is missing.", out failureReason);

            var characterId = context.CharacterId;
            if (string.IsNullOrWhiteSpace(characterId))
                return Fail("CharacterRuntimeContext characterId is empty.", out failureReason);

            if (contexts.ContainsKey(characterId))
                return Fail($"Character '{characterId}' is already registered.", out failureReason);

            contexts.Add(characterId, context);
            failureReason = string.Empty;
            Debug.Log($"[VirtualPartner] Character registered: {characterId}.");
            return true;
        }

        public static bool TryGet(string characterId, out CharacterRuntimeContext context)
        {
            if (string.IsNullOrWhiteSpace(characterId))
            {
                context = null;
                return false;
            }

            return contexts.TryGetValue(characterId, out context);
        }

        public static void GetRegisteredContexts(List<CharacterRuntimeContext> results)
        {
            if (results == null)
                return;

            results.Clear();
            foreach (var pair in contexts)
                results.Add(pair.Value);
        }

        public static void Unregister(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return;

            if (contexts.Remove(characterId))
                Debug.Log($"[VirtualPartner] Character unregistered: {characterId}.");
        }

        private static bool Fail(string message, out string failureReason)
        {
            failureReason = message;
            return false;
        }
    }
}
