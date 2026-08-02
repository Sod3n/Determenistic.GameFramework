namespace Deterministic.GameFramework.Detour
{
    public class DtDetourBuilder
    {
        public DtMeshData Build(DtNavMeshCreateParams option, int tileX, int tileY)
        {
            DtMeshData data = DtNavMeshBuilder.CreateNavMeshData(option);
            if (data != null)
            {
                data.header.x = tileX;
                data.header.y = tileY;
            }

            return data;
        }
    }
}
