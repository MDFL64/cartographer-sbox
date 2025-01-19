using System.Diagnostics;
using System.Threading.Tasks;

public sealed class MapTile : Component
{	
	public const float BASE_ELEVATION = 600;
	//const float REGION_SIZE = 10012;

	public int TileNumber = 0;
	public Region ParentRegion;

	private bool HasBuildings = false;

	public ProcMesh TrySpawnMesh(string name, Vector3 pos) {
		if (GameObject == null || !GameObject.IsValid) {
			return null;
		}
		var obj = new GameObject();
		obj.Parent = GameObject;
		obj.LocalPosition = pos;
		obj.Name = name;

		var mesh = obj.AddComponent<ProcMesh>();
		return mesh;
	}

	protected override void OnStart()
	{	
		float tile_size = Region.ScaleDistance(512);
		float x_pos = tile_size * (TileNumber % 20);
		float y_pos = -tile_size * MathX.Floor(TileNumber / 20);
		LocalPosition = new Vector3(x_pos,y_pos,0);
		
		FetchTerrain();
	}

	protected override void OnUpdate()
	{
		var buildings = ParentRegion.Buildings;
		var roads = ParentRegion.Roads;
		if (!HasBuildings && buildings != null && roads != null) {
			HasBuildings = true;
			SpawnMapItems(buildings,roads);
		}
	}

	private async void SpawnMapItems(List<BuildingInfo> buildings, List<RoadInfo> roads) {
		int seed = 0;
		foreach (var b in buildings) {
			seed++;
			if (TileNumber == Region.MapTileFromMeters(b.BasePos)) {
				var pos = new Vector3(b.BasePos.x % 512, b.BasePos.y % 512, b.GroundHigh - BASE_ELEVATION);
				var building = TrySpawnMesh("Building", Region.ScalePos(pos));
				if (building == null) {
					return;
				}
				building.SetBuilding(b, seed, ParentRegion.MatBuilding);
			}
			await WaitStep();
		}
		foreach (var r in roads) {
			if (TileNumber == Region.MapTileFromMeters(r.BasePos)) {
				var pos = new Vector3(r.BasePos.x % 512, r.BasePos.y % 512, r.BasePos.z - BASE_ELEVATION);
				var road = TrySpawnMesh("Road", Region.ScalePos(pos));
				if (road == null) {
					return;
				}
				road.SetRoad(r, ParentRegion.MatRoad, ParentRegion.MatSidewalk);
			}
			await WaitStep();
		}
	}

	// quota shared between all tiles
	private static int StepCount = 0;
	private async Task WaitStep() {
		StepCount++;
		if (StepCount > 50) {
			StepCount = 0;
			await GameTask.Yield();
		}
	}

	private async void FetchTerrain() {
		var reader = await ParentRegion.FetchData("tile"+TileNumber);

		var elev_base = reader.ReadSingle() - BASE_ELEVATION;
		var elev_range = reader.ReadSingle();

		float tile_size = Region.ScaleDistance(512);

		var vertex_count = reader.ReadUInt16();
		var vertices = new TerrainVertex[vertex_count];

		// just transform to sbox coords here
		for (int i=0;i<vertices.Length;i++) {
			float x = (float)reader.ReadUInt16() / 65535 * tile_size;
			float y = (float)reader.ReadUInt16() / 65535 * tile_size;
			float z = Region.ScaleElevation((float)reader.ReadUInt16() / 65535 * elev_range);
			vertices[i].Position = new Vector3(x,-y,z);

			float nx = (float)reader.ReadSByte() / 127;
			float ny = (float)reader.ReadSByte() / 127;
			float nz = (float)reader.ReadSByte() / 127;
			vertices[i].Normal = new Vector3(nx,-ny,nz);
		}

		var index_count = reader.ReadUInt16();
		var indices = new int[index_count*3];

		for (int i=0;i<indices.Length;i++) {
			indices[i] = reader.ReadUInt16();
		}

		var mesh = TrySpawnMesh("Terrain",Vector3.Up * Region.ScaleElevation(elev_base));
		if (mesh != null) {
			mesh.SetTerrain(vertices,indices,ParentRegion.MatGround,ParentRegion.GroundColor);
		}
	}
}
