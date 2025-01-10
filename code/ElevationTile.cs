using System.Diagnostics;
using Sandbox;

public sealed class ElevationTile : Component
{	
	public const float BASE_ELEVATION = 1450;
	const float REGION_SIZE = 10012;

	public int TileNumber = 0;
	public TerrainMaterial TerrainMat;
	public Region ParentRegion;
	private bool HasBuildings = false;
	private Terrain Terrain;

	protected override void OnStart()
	{
		FetchTerrain();
	}

	protected override void OnUpdate()
	{
		var buildings = ParentRegion.Buildings;
		var roads = ParentRegion.Roads;
		if (!HasBuildings && Terrain != null && buildings != null && roads != null) {
			int seed = 0;
			foreach (var b in buildings) {
				seed++;
				if (TileNumber == ParentRegion.MapTileFromMeters(b.BasePos)) {
					ParentRegion.SpawnBuilding(b,this.GameObject,seed);
				}
			}
			int rc = 0;
			foreach (var r in roads) {
				if (TileNumber == ParentRegion.MapTileFromMeters(r.BasePos)) {
					rc++;
					ParentRegion.SpawnRoad(r,this.GameObject);
				}
			}
			Log.Info("roads = "+rc);
			HasBuildings = true;
		}
	}

	static Stack<TerrainStorage> StoragePool = new Stack<TerrainStorage>();

	private TerrainStorage CreateStorage() {
		if (StoragePool.Count>0) {
			return StoragePool.Pop();
		}
		Log.Info("NEW storage");

		var storage = new TerrainStorage();
		if (storage.Resolution != 512) {
			storage.SetResolution(512);
		}
		storage.Materials.Add(TerrainMat);
		return storage;
	}
 
	public void ReleaseStorage() {
		var storage = Terrain?.Storage;
		if (storage != null) {
			StoragePool.Push(storage);
		}
	}

	private async void FetchTerrain() {
		var empty = new System.Net.Http.ByteArrayContent(new byte[0]);
		var headers = new Dictionary<string, string>();
		var token = new System.Threading.CancellationToken();
		var bytes = await Http.RequestBytesAsync("http://localhost:8080/donkey_west/file/tile1", "GET", empty, headers, token );

		var stream = new System.IO.MemoryStream(bytes);
		var reader = new System.IO.BinaryReader(stream);

		var elev_base = reader.ReadSingle();
		var elev_range = reader.ReadSingle();

		float tile_size = Region.ScaleMetersXY(512);
		{
			float z_pos = Region.ScaleElevation(elev_base - BASE_ELEVATION);
			float x_pos = tile_size * (TileNumber % 20);
			float y_pos = -tile_size * MathX.Floor(TileNumber / 20);
			LocalPosition = new Vector3(x_pos,y_pos,z_pos);
		}

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

		ParentRegion.SpawnTerrain(vertices,indices,GameObject);
	}
}
