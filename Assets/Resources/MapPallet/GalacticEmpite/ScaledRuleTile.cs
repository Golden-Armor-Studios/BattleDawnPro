using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "ScaledRuleTile", menuName = "Tiles/Scaled Rule Tile")]
public class ScaledRuleTile : RuleTile<ScaledRuleTile.Neighbor>
{
    [Header("Scale Settings")]
    [Tooltip("Runtime scale applied to the tile when rendered in a Tilemap.")]
    [SerializeField]
    private Vector3 scale = new Vector3(10f, 10f, 1f);

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        base.GetTileData(position, tilemap, ref tileData);
        tileData.transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, scale);
    }

    public class Neighbor : RuleTile.TilingRuleOutput.Neighbor {}
}
