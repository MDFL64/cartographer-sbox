public sealed class MapTile : Component
{	
	public const float BASE_ELEVATION = 1450;
	//const float REGION_SIZE = 10012;

	public int TileNumber = 0;
	public Region ParentRegion;

	private bool HasBuildings = false;

	public ProcMesh SpawnMesh(string name, Vector3 pos) {
		var obj = ParentRegion.MeshPrefab.Clone();
		obj.Parent = GameObject;
		obj.LocalPosition = pos;
		obj.Name = name;

		var mesh = obj.GetComponent<ProcMesh>();
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
			int seed = 0;
			foreach (var b in buildings) {
				seed++;
				if (TileNumber == Region.MapTileFromMeters(b.BasePos)) {
					var pos = new Vector3(b.BasePos.x % 512, b.BasePos.y % 512, b.GroundHigh - BASE_ELEVATION);
					var building = SpawnMesh("Building", Region.ScalePos(pos));
					building.SetBuilding(b, seed, ParentRegion.MatBuilding);
				}
			}
			int rc = 0;
			foreach (var r in roads) {
				if (TileNumber == Region.MapTileFromMeters(r.BasePos)) {
					var pos = new Vector3(r.BasePos.x % 512, r.BasePos.y % 512, r.BasePos.z - BASE_ELEVATION);
					var road = SpawnMesh("Road", Region.ScalePos(pos));
					road.SetRoad(r.Nodes ,ParentRegion.MatBuilding);
					rc++;
					//ParentRegion.SpawnRoad(r,this.GameObject);
				}
			}
			Log.Info("roads = "+rc);
			HasBuildings = true;
		}
	}

	private async void FetchTerrain() {
		var empty = new System.Net.Http.ByteArrayContent(new byte[0]);
		var headers = new Dictionary<string, string>();
		var token = new System.Threading.CancellationToken();
		var bytes = await Http.RequestBytesAsync("http://localhost:8080/donkey_west/tile"+TileNumber, "GET", empty, headers, token );

		var stream = new System.IO.MemoryStream(bytes);
		var reader = new System.IO.BinaryReader(stream);

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

		var mesh = SpawnMesh("Terrain",Vector3.Up * Region.ScaleElevation(elev_base));
		mesh.SetTerrain(vertices,indices,ParentRegion.MatGround);
	}
}
