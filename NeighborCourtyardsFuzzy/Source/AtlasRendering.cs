using UnityEngine;
using Verse;

namespace FuzzyNeighbor
{
    public sealed class AtlasNodeProperties : PawnRenderNodeProperties
    {
        public int atlasRow;
        public Vector2 meshSize = Vector2.one;
        internal readonly Mesh[] directionalMeshes = new Mesh[4];

        public AtlasNodeProperties()
        {
            nodeClass = typeof(AtlasNode);
        }
    }

    // The one texture contains front/right/back bodies above front/right/back heads.
    // Native body/head workers retain the game's bed, posture and head-offset rules.
    public sealed class AtlasNode : PawnRenderNode
    {
        public AtlasNode(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree) { }

        public override Graphic GraphicFor(Pawn pawn) =>
            GraphicDatabase.Get<Graphic_Single>(Props.texPath, ShaderDatabase.Cutout,
                Vector2.one, ColorFor(pawn));

        public override GraphicMeshSet MeshSetFor(Pawn pawn) => MeshPool.GetMeshSetForSize(1f, 1f);

        public override Mesh GetMesh(PawnDrawParms parms)
        {
            int direction = parms.facing.AsInt;
            var properties = (AtlasNodeProperties)Props;
            if (properties.directionalMeshes[direction] != null) return properties.directionalMeshes[direction];
            int column = parms.facing == Rot4.North ? 2 : parms.facing.IsHorizontal ? 1 : 0;
            float left = column / 3f;
            float right = (column + 1) / 3f;
            float bottom = (1 - properties.atlasRow) / 2f;
            float top = bottom + .5f;
            if (parms.facing == Rot4.West)
            {
                float swap = left;
                left = right;
                right = swap;
            }
            float halfWidth = properties.meshSize.x / 2;
            float halfHeight = properties.meshSize.y / 2;
            var mesh = new Mesh { name = "Fuzzy neighbor " + properties.atlasRow + "/" + direction };
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, 0, -halfHeight),
                new Vector3(-halfWidth, 0, halfHeight),
                new Vector3(halfWidth, 0, halfHeight),
                new Vector3(halfWidth, 0, -halfHeight)
            };
            mesh.uv = new[] { new Vector2(left, bottom), new Vector2(left, top), new Vector2(right, top), new Vector2(right, bottom) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            properties.directionalMeshes[direction] = mesh;
            return mesh;
        }
    }
}
