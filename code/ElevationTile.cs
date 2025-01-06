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

	private TerrainStorage CreateStorage() {
		var storage = new TerrainStorage();
		if (storage.Resolution != 512) {
			storage.SetResolution(512);
		}
		storage.Materials.Add(TerrainMat);
		return storage;
	}

	private async void FetchTerrain() {
		var empty = new System.Net.Http.ByteArrayContent(new byte[0]);
		var headers = new Dictionary<string, string>();
		var token = new System.Threading.CancellationToken();
		var bytes = await Http.RequestBytesAsync("http://localhost:8080/donkey_west/e/"+TileNumber, "GET", empty, headers, token );

		var stream = new System.IO.MemoryStream(bytes);
		var reader = new System.IO.BinaryReader(stream);

		var width = reader.ReadUInt16();
		var height = reader.ReadUInt16();

		var elev_base = reader.ReadSingle();
		var elev_range = reader.ReadSingle();

		var terrain = Components.GetOrCreate<Terrain>();
		terrain.EnableCollision = true;
		//terrain.ClipMapLodLevels = 5;
		//terrain.ClipMapLodExtentTexels = 128;

		var storage = CreateStorage();

		float tile_size = Region.ScaleMetersXY(512);

		storage.TerrainSize = tile_size;
		storage.TerrainHeight = Region.ScaleElevation(elev_range);

		{
			//float y_offset = Region.ScaleMetersXY(REGION_SIZE);

			float z_pos = Region.ScaleElevation(elev_base - BASE_ELEVATION);
			float x_pos = tile_size * (TileNumber % 20);
			float y_pos = -tile_size * MathX.Floor(1 + TileNumber / 20);

			/*Transform.*/
			LocalPosition = new Vector3(x_pos,y_pos,z_pos);
		}

		if (width == 512 && height == 512) {
			for (int i=0;i<512*512;i++) {
				storage.HeightMap[i] = reader.ReadUInt16();
			}
		} else {
			int y_offset = 512 - height;
			for (int y=0;y<height;y++) {
				for (int x=0;x<width;x++) {
					storage.HeightMap[(y_offset + y) * 512 + x] = reader.ReadUInt16();
				}
			}
		}
		// set storage
		terrain.Storage = storage;
		Terrain = terrain;
	}
}
