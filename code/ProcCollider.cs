
public sealed class ProcCollider : Collider
{
    public Vector3[] Vertices;
    public int[] Indices;

	protected override IEnumerable<PhysicsShape> CreatePhysicsShapes( PhysicsBody targetBody )
	{
        //PhysicsGroupDescription.BodyPart.MeshPart x;
        //Terrain t;
        
        if (Vertices != null && Indices != null) {
            yield return targetBody.AddMeshShape(Vertices,Indices);
            // release references
            Vertices = null;
            Indices = null;
        } else {
            Log.Error("mesh is missing collider");
        }
	}
}
