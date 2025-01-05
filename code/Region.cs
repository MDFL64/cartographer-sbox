using Sandbox;
using System;

public class BuildingInfo {
	public Vector2 BasePos;
	public float BaseLow;
	public float Height;
	public Vector2[] Nodes;
}

public sealed class Region : Component
{
	int CurrentX = Int32.MaxValue;
	int CurrentY = Int32.MaxValue;

	int TileViewDistance = 1;

	const float SCALE_XY = 1;
	const float SCALE_Z = 1;

	const double UTM_X = 449993.99997825973;
	const double UTM_Y = 4910006.000037198 - 10012;

	const int TILE_COUNT = 20;

	public List<BuildingInfo> Buildings;

	[Property]
	GameObject Spectator;
	[Property]
	GameObject BuildingPrefab;
	[Property]
	TerrainMaterial TerrainMat;

	public static float ScaleMetersXY(float meters) {
		return meters * SCALE_XY * 39.3701f;
	}

	public static float ScaleMetersZ(float meters) {
		return meters * SCALE_Z * 39.3701f;
	}

	protected override void OnStart()
	{
		Vector2 SpawnPoint = new Vector2(7655.545f,4544.221f);
		//var pos = UTMToLocal(457649.545, 4904538.221) * ScaleMetersXY(1);
		//Log.Info("!?!? "+UTMToLocal(457649.545, 4904538.221));
		Spectator.WorldPosition = SpawnPoint * ScaleMetersXY(1);
		FetchOSM();
		//DebugPos(457649.545, 4904538.221);
		//SpawnBuilding();
	}

	private async void FetchOSM() {
		var empty = new System.Net.Http.ByteArrayContent(new byte[0]);
		var headers = new Dictionary<string, string>();
		var token = new System.Threading.CancellationToken();
		var bytes = await Http.RequestBytesAsync("http://localhost:8080/donkey_west/osm", "GET", empty, headers, token );

		var stream = new System.IO.MemoryStream(bytes);
		var reader = new System.IO.BinaryReader(stream);

		var list = new List<BuildingInfo>();

		while (stream.Position < stream.Length) {
			var base_x = reader.ReadSingle();
			var base_y = reader.ReadSingle();
			var elevation = reader.ReadSingle();
			var height = reader.ReadSingle();
			var base_pos = new Vector2(base_x,base_y);
			var nodes = new Vector2[reader.ReadUInt16()];
			for (int i=0;i<nodes.Length;i++) {
				var x = reader.ReadSingle();
				var y = reader.ReadSingle();
				var pos = new Vector2(x,y);
				nodes[i] = pos;
			}
			var building = new BuildingInfo() {
				BasePos = base_pos,
				BaseLow = elevation,
				Height = height,
				Nodes = nodes
			};
			list.Add(building);
		}

		Buildings = list;
	}

	private void DebugPos(Vector2 pos) {
		float local_x = ScaleMetersXY(pos.x);
		float local_y = ScaleMetersXY(pos.y);

		Gizmo.Draw.Line(new Vector3(local_x,local_y,-2000), new Vector3(local_x,local_y,0) );
	}

	public void SpawnBuilding(BuildingInfo info, GameObject parent, int index) {
		//var xform = new Transform(info.BasePos * ScaleMetersXY(1));
		var building = BuildingPrefab.Clone();
		building.Parent = parent;

		float z_pos = ScaleMetersZ(info.BaseLow - ElevationTile.BASE_ELEVATION);

		building.WorldPosition = new Vector3(info.BasePos * ScaleMetersXY(1),z_pos);
		var mesh = building.GetComponent<ProcMesh>();
		var points = new Vector2[info.Nodes.Length];
		for (int i=0;i<points.Length;i++) {
			points[i] = info.Nodes[i] * ScaleMetersXY(1);
		}
		mesh.Path = points;
		mesh.ColorSeed = index;
		// BUILDING height is scaled using xy -- todo rename this
		mesh.Top = ScaleMetersXY(info.Height);
	}

	public int MapTileFromMeters(Vector2 pos) {
		float tile_size = 512;
		int x = (int)Math.Floor(pos.x / tile_size);
		int y = (int)Math.Floor((10012 - pos.y) / tile_size);
		return MapTile(x,y);
	}

	protected override void OnUpdate() {
		float tile_size = ScaleMetersXY(512);

		var cam_pos = Scene.Camera.WorldPosition;

		var base_pos = WorldPosition + new Vector3(0,ScaleMetersXY(10012),0);
		var local_pos = cam_pos - base_pos;
		int tile_x = (int)Math.Floor(local_pos.x / tile_size);
		int tile_y = (int)Math.Floor(-local_pos.y / tile_size);

		if (CurrentX != tile_x || CurrentY != tile_y) {
			CurrentX = tile_x;
			CurrentY = tile_y;

			RefreshTiles();
		}
	}

	private int MapTile(int x, int y) {
		if (x < 0 || x >= TILE_COUNT || y < 0 || y >= TILE_COUNT) {
			return -1;
		}
		return y * TILE_COUNT + x;
	}

	private void RefreshTiles() {
		var wanted_ids = new List<int>();

		for (int y = CurrentY - TileViewDistance; y <= CurrentY + TileViewDistance; y++) {
			for (int x = CurrentX - TileViewDistance; x <= CurrentX + TileViewDistance; x++) {			
				int tile_id = MapTile(x,y);
				if (tile_id >= 0) {
					wanted_ids.Add(tile_id);
				}
			}
		}

		foreach (var child in GameObject.Children) {
			var tile = child.GetComponent<ElevationTile>();
			if (tile != null) {
				int tile_id = tile.TileNumber;
				if (!wanted_ids.Remove(tile_id)) {
					child.Destroy();
					Log.Info("todo kill old");
				}
			}
		}

		foreach (var id in wanted_ids) {
			Log.Info("spawn "+id);
			AddTile(id);
		}
	}

	private void AddTile(int id) {
		var child = new GameObject();
		child.Name = "Tile";
		child.Parent = this.GameObject;
		var tile = child.Components.GetOrCreate<ElevationTile>();
		tile.TerrainMat = TerrainMat;
		//Log.Info("AAA "+tile.TerrainMat);
		tile.TileNumber = id;
		tile.ParentRegion = this;
	}
}
