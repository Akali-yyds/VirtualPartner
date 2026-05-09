using UnityEngine;

namespace ModelRepairRuntime.Toki_CH0187
{
    [ExecuteAlways]
    public sealed class Toki_CH0187_CharacterMouthEventReceiver : MonoBehaviour
    {
        public Renderer MouthRenderer;
        public int MouthMaterialIndex;
        public Vector2 MouthDefaultUV;
        public Texture2D MouthTexture;
        [Range(-1, 63)]
        public int MouthIndex = -1;

        private const int MouthTileColumns = 8;
        private const int MouthTileRows = 8;
        private MaterialPropertyBlock propertyBlock;
        private static readonly int MouthTileTexStId = Shader.PropertyToID("_MouthTileTex_ST");
        private static readonly int MouthTileTexId = Shader.PropertyToID("_MouthTileTex");
        private static readonly int MouthTileEnabledId = Shader.PropertyToID("_MouthTileEnabled");

        private void OnEnable()
        {
            ApplyMouth();
        }

        private void OnValidate()
        {
            MouthIndex = Mathf.Clamp(MouthIndex, -1, 63);
            ApplyMouth();
        }

        public void SetMouthTile(int tile)
        {
            MouthIndex = Mathf.Clamp(tile, -1, 63);
            ApplyMouth();
        }

        public void SetMouthTexture(Texture2D texture)
        {
            MouthTexture = texture;
            ApplyMouth();
        }

        private void ApplyMouth()
        {
            ApplyMouthSettings(MouthIndex >= 0 ? TileToUv(MouthIndex) : MouthDefaultUV);
        }

        private Vector2 TileToUv(int tile)
        {
            tile = Mathf.Clamp(tile, 0, MouthTileColumns * MouthTileRows - 1);
            var row = tile / MouthTileColumns;
            var column = tile % MouthTileColumns;

            return new Vector2(column / (float)MouthTileColumns, row / (float)MouthTileRows);
        }

        private Material ResolveMouthMaterial()
        {
            if (MouthRenderer == null)
                return null;
            var materials = MouthRenderer.sharedMaterials;
            if (materials == null || MouthMaterialIndex < 0 || MouthMaterialIndex >= materials.Length)
                return null;
            return materials[MouthMaterialIndex];
        }

        private void ApplyMouthSettings(Vector2 uv)
        {
            if (MouthRenderer == null)
                return;

            var material = ResolveMouthMaterial();
            if (material == null)
                return;

            var scale = material.HasProperty(MouthTileTexId) ? material.GetTextureScale(MouthTileTexId) : new Vector2(0.5f, 0.5f);
            if (scale.x <= 0.0001f || scale.y <= 0.0001f)
                scale = new Vector2(0.5f, 0.5f);

            if (!Application.isPlaying)
            {
                material.SetTextureScale(MouthTileTexId, scale);
                material.SetTextureOffset(MouthTileTexId, uv);
                if (material.HasProperty(MouthTileEnabledId))
                    material.SetFloat(MouthTileEnabledId, 1f);
            }

            if (propertyBlock == null)
                propertyBlock = new MaterialPropertyBlock();

            MouthRenderer.GetPropertyBlock(propertyBlock, MouthMaterialIndex);
            var texture = MouthTexture != null ? MouthTexture : material.HasProperty(MouthTileTexId) ? material.GetTexture(MouthTileTexId) : null;
            if (texture != null)
                propertyBlock.SetTexture(MouthTileTexId, texture);
            propertyBlock.SetVector(MouthTileTexStId, new Vector4(scale.x, scale.y, uv.x, uv.y));
            propertyBlock.SetFloat(MouthTileEnabledId, 1f);
            MouthRenderer.SetPropertyBlock(propertyBlock, MouthMaterialIndex);
        }
    }
}
