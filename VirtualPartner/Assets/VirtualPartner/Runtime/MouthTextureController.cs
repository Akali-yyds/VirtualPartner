using UnityEngine;

namespace VirtualPartner.Runtime
{
    public enum MouthControlSource
    {
        Default,
        Expression,
        Speech,
        Debug
    }

    [DisallowMultipleComponent]
    public sealed class MouthTextureController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Renderer mouthRenderer;
        [SerializeField] private Texture mouthTexture;

        [Header("Mouth Tile")]
        [SerializeField] private int mouthMaterialIndex;
        [SerializeField] private int tileColumns = 8;
        [SerializeField] private int tileRows = 8;

        [Header("Runtime Status")]
        [SerializeField] private bool initialized;
        [SerializeField] private MouthControlSource currentSource = MouthControlSource.Default;
        [SerializeField] private int currentMouthIndex = -1;
        [SerializeField] private bool debugOverrideActive;
        [SerializeField] private bool speechMouthActive;
        [SerializeField] private bool expressionMouthActive;
        [SerializeField] private string lastMessage = "Not initialized.";

        private static readonly int MouthTileTexStId = Shader.PropertyToID("_MouthTileTex_ST");
        private static readonly int MouthTileTexId = Shader.PropertyToID("_MouthTileTex");
        private static readonly int MouthTileEnabledId = Shader.PropertyToID("_MouthTileEnabled");

        private MaterialPropertyBlock propertyBlock;
        private Vector4 defaultSt;
        private float defaultEnabled = 1f;
        private Texture defaultTexture;
        private int debugMouthIndex = -1;
        private int speechMouthIndex = -1;
        private int expressionMouthIndex = -1;

        public bool Initialized => initialized;
        public MouthControlSource CurrentSource => currentSource;
        public int CurrentMouthIndex => currentMouthIndex;
        public bool DebugOverrideActive => debugOverrideActive;
        public bool SpeechMouthActive => speechMouthActive;
        public bool ExpressionMouthActive => expressionMouthActive;
        public string LastMessage => lastMessage;

        private void Awake()
        {
            if (mouthRenderer != null)
                Configure(mouthRenderer, mouthMaterialIndex, tileColumns, tileRows, mouthTexture);
        }

        public bool Configure(Renderer renderer, int materialIndex, int columns, int rows, Texture texture)
        {
            mouthRenderer = renderer;
            mouthMaterialIndex = materialIndex;
            tileColumns = Mathf.Max(1, columns);
            tileRows = Mathf.Max(1, rows);
            mouthTexture = texture;

            if (!CaptureDefaultState())
            {
                initialized = false;
                return false;
            }

            initialized = true;
            ApplyCurrentState();
            lastMessage = "Ready.";
            return true;
        }

        public bool Configure()
        {
            return Configure(mouthRenderer, mouthMaterialIndex, tileColumns, tileRows, mouthTexture);
        }

        public void SetDebugMouthIndex(int index)
        {
            debugMouthIndex = ClampIndex(index);
            debugOverrideActive = true;
            ApplyCurrentState();
        }

        public void ReleaseDebugOverride()
        {
            debugOverrideActive = false;
            ApplyCurrentState();
        }

        public void SetSpeechMouthIndex(int index)
        {
            speechMouthIndex = ClampIndex(index);
            speechMouthActive = true;
            ApplyCurrentState();
        }

        public void ClearSpeechMouth()
        {
            speechMouthActive = false;
            ApplyCurrentState();
        }

        public void SetExpressionMouthIndex(int index)
        {
            expressionMouthIndex = ClampIndex(index);
            expressionMouthActive = true;
            ApplyCurrentState();
        }

        public void ClearExpressionMouth()
        {
            expressionMouthActive = false;
            ApplyCurrentState();
        }

        public void ClearAllRuntimeMouth()
        {
            debugOverrideActive = false;
            speechMouthActive = false;
            expressionMouthActive = false;
            ApplyCurrentState();
        }

        private bool CaptureDefaultState()
        {
            if (mouthRenderer == null)
            {
                lastMessage = "Mouth renderer is missing.";
                return false;
            }

            var materials = mouthRenderer.sharedMaterials;
            if (materials == null || mouthMaterialIndex < 0 || mouthMaterialIndex >= materials.Length)
            {
                lastMessage = "Mouth material index is invalid.";
                return false;
            }

            var material = materials[mouthMaterialIndex];
            if (material == null)
            {
                lastMessage = "Mouth material is missing.";
                return false;
            }

            var scale = material.HasProperty(MouthTileTexId)
                ? material.GetTextureScale(MouthTileTexId)
                : Vector2.one;
            var offset = material.HasProperty(MouthTileTexId)
                ? material.GetTextureOffset(MouthTileTexId)
                : Vector2.zero;
            defaultSt = new Vector4(scale.x, scale.y, offset.x, offset.y);
            defaultTexture = material.HasProperty(MouthTileTexId) ? material.GetTexture(MouthTileTexId) : null;
            defaultEnabled = material.HasProperty(MouthTileEnabledId) ? material.GetFloat(MouthTileEnabledId) : 1f;

            EnsurePropertyBlock();
            mouthRenderer.GetPropertyBlock(propertyBlock, mouthMaterialIndex);
            var blockSt = propertyBlock.GetVector(MouthTileTexStId);
            if (Mathf.Abs(blockSt.x) > 0.0001f || Mathf.Abs(blockSt.y) > 0.0001f)
                defaultSt = blockSt;

            var blockTexture = propertyBlock.GetTexture(MouthTileTexId);
            if (blockTexture != null)
                defaultTexture = blockTexture;

            if (mouthTexture == null)
                mouthTexture = defaultTexture;

            return true;
        }

        private void ApplyCurrentState()
        {
            if (!initialized && !CaptureDefaultState())
                return;

            if (debugOverrideActive)
            {
                ApplyIndex(debugMouthIndex, MouthControlSource.Debug);
                return;
            }

            if (speechMouthActive)
            {
                ApplyIndex(speechMouthIndex, MouthControlSource.Speech);
                return;
            }

            if (expressionMouthActive)
            {
                ApplyIndex(expressionMouthIndex, MouthControlSource.Expression);
                return;
            }

            ApplyDefault();
        }

        private void ApplyIndex(int index, MouthControlSource source)
        {
            if (index < 0)
            {
                ApplyDefault();
                currentSource = source;
                currentMouthIndex = -1;
                return;
            }

            var clamped = ClampIndex(index);
            var uv = TileToUv(clamped);
            var st = new Vector4(defaultSt.x, defaultSt.y, uv.x, uv.y);
            ApplySt(st, Mathf.Max(0f, defaultEnabled), mouthTexture != null ? mouthTexture : defaultTexture);
            currentSource = source;
            currentMouthIndex = clamped;
            lastMessage = $"{source} mouth index {clamped}.";
        }

        private void ApplyDefault()
        {
            ApplySt(defaultSt, defaultEnabled, defaultTexture);
            currentSource = MouthControlSource.Default;
            currentMouthIndex = -1;
            lastMessage = "Default mouth.";
        }

        private void ApplySt(Vector4 st, float enabled, Texture texture)
        {
            if (mouthRenderer == null)
                return;

            EnsurePropertyBlock();
            mouthRenderer.GetPropertyBlock(propertyBlock, mouthMaterialIndex);
            propertyBlock.SetTexture(MouthTileTexId, texture);
            propertyBlock.SetVector(MouthTileTexStId, st);
            propertyBlock.SetFloat(MouthTileEnabledId, enabled);
            mouthRenderer.SetPropertyBlock(propertyBlock, mouthMaterialIndex);
        }

        private Vector2 TileToUv(int tile)
        {
            tile = Mathf.Clamp(tile, 0, tileColumns * tileRows - 1);
            var row = tile / tileColumns;
            var column = tile % tileColumns;
            return new Vector2(column / (float)tileColumns, row / (float)tileRows);
        }

        private int ClampIndex(int index)
        {
            return Mathf.Clamp(index, -1, tileColumns * tileRows - 1);
        }

        private void EnsurePropertyBlock()
        {
            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();
        }
    }
}
