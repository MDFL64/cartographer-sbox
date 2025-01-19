using Sandbox;
using System;
using System.IO.Compression;
using System.Threading.Tasks;

public class BuildingInfo {
	public Vector2 BasePos;
	public float GroundLow;
	public float GroundHigh;
	public float Height;
	public Vector2[] Nodes;
}

public class RoadInfo {
	public Vector3 BasePos;
	public RoadNode[] Nodes;
	public RoadKind Kind;
	public byte LaneCount;
}

public struct RoadNode {
	public Vector3 Left;
	public Vector3 Right;
	public Vector3 Normal;
	public Vector3 Direction;
}

public struct TerrainVertex {
	public Vector3 Position;
	public Vector3 Normal;
}

public enum RoadKind : byte {
	Sidewalk = 0,
	TwoWay = 1,
	OneWay = 2
}

public sealed class Region : Component
{
	#if DEBUG
	public string HOST = "localhost:8080";
	#else
	public string HOST = "cart.tmp.bz"; //"localhost:8080";
	#endif

	int CurrentX = Int32.MaxValue;
	int CurrentY = Int32.MaxValue;

	int TileViewDistance = 2;

	const float SCALE_XY = 1;
	const float SCALE_Z = 2;

	const int TILE_COUNT = 20;

	public List<BuildingInfo> Buildings;
	public List<RoadInfo> Roads;

	[Property]
	public string RegionName;

	[Property]
	GameObject Spectator;

	[Property]
	public Material MatGround;
	[Property]
	public Material MatBuilding;
	[Property]
	public Material MatRoad;
	[Property]
	public Material MatSidewalk;

	public Color GroundColor = Color32.FromRgb(0xd1b87b); //Color32.FromRgb(0x6b8c5a);

	public static float ScaleDistance(float meters) {
		return meters * SCALE_XY * 39.3701f;
	}

	public static float ScaleElevation(float meters) {
		return meters * SCALE_Z * 39.3701f;
	}

	public static Vector3 ScalePos(Vector3 pos) {
		pos.x *= SCALE_XY * 39.3701f;
		pos.y *= -SCALE_XY * 39.3701f;
		pos.z *= SCALE_Z * 39.3701f;
		return pos;
	}

	protected override void OnStart()
	{
		var spawn_point = new Vector3(160173.8f,-104372.9f,11884.41f);
		Spectator.WorldPosition = spawn_point;// * ScaleDistance(1);
		FetchOSM();
	}

	public async Task<System.IO.BinaryReader> FetchData(string name) {
		var empty = new System.Net.Http.ByteArrayContent([]);
		var headers = new Dictionary<string, string>();
		var token = new System.Threading.CancellationToken();
		var bytes = await Http.RequestBytesAsync("http://"+HOST+"/"+RegionName+"/"+name+".bin.gz", "GET", empty, headers, token );

		var stream = new System.IO.MemoryStream(bytes);
		var gzip = new GZipStream(stream,CompressionMode.Decompress,false);
		return new System.IO.BinaryReader(gzip);
	}

	private async void FetchOSM() {
		var reader = await FetchData("map");

		var building_list = new List<BuildingInfo>();
		var road_list = new List<RoadInfo>();

		while (true) {
			byte kind;
			try {
				kind = reader.ReadByte();
			} catch (Exception) {
				break;
			}
			if (kind == 0) {
				// building
				var base_x = reader.ReadSingle();
				var base_y = reader.ReadSingle();
				var ground_low = reader.ReadSingle();
				var ground_high = reader.ReadSingle();
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
					GroundLow = ground_low,
					GroundHigh = ground_high,
					Height = height,
					Nodes = nodes
				};
				building_list.Add(building);
			} else if (kind == 1) {
				// road
				var base_x = reader.ReadSingle();
				var base_y = reader.ReadSingle();
				var base_z = reader.ReadSingle();
				var base_pos = new Vector3(base_x,base_y,base_z);
				var road_kind = (RoadKind)reader.ReadByte();
				var lanes = reader.ReadByte();

				var nodes = new RoadNode[reader.ReadUInt16()];
				for (int i=0;i<nodes.Length;i++) {
					RoadNode node;
					{
						var x = reader.ReadSingle();
						var y = reader.ReadSingle();
						var z = reader.ReadSingle();
						node.Left = new Vector3(x,y,z);
					}
					{
						var x = reader.ReadSingle();
						var y = reader.ReadSingle();
						var z = reader.ReadSingle();
						node.Right = new Vector3(x,y,z);
					}
					{
						var x = reader.ReadSingle();
						var y = reader.ReadSingle();
						var z = reader.ReadSingle();
						node.Normal = new Vector3(x,y,z);
					}
					{
						var x = reader.ReadSingle();
						var y = reader.ReadSingle();
						var z = reader.ReadSingle();
						node.Direction = new Vector3(x,y,z);
					}
					nodes[i] = node;
				}
				var road = new RoadInfo() {
					BasePos = base_pos,
					Nodes = nodes,
					Kind = road_kind,
					LaneCount = lanes
				};
				road_list.Add(road);
			}
		}

		Roads = road_list;
		Buildings = building_list;
	}

	public static int MapTileFromMeters(Vector2 pos) {
		float tile_size = 512;
		int x = (int)Math.Floor(pos.x / tile_size);
		int y = (int)Math.Floor(pos.y / tile_size);
		return MapTile(x,y);
	}

	protected override void OnUpdate() {
		float tile_size = ScaleDistance(512);

		var cam_pos = Scene.Camera.WorldPosition;

		var local_pos = Transform.World.PointToLocal(cam_pos);
		int tile_x = (int)Math.Floor(local_pos.x / tile_size);
		int tile_y = (int)Math.Floor(-local_pos.y / tile_size);

		if (CurrentX != tile_x || CurrentY != tile_y) {
			CurrentX = tile_x;
			CurrentY = tile_y;

			RefreshTiles();
		}
	}

	private static int MapTile(int x, int y) {
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
			var tile = child.GetComponent<MapTile>();
			if (tile != null) {
				int tile_id = tile.TileNumber;
				if (!wanted_ids.Remove(tile_id)) {
					child.Destroy();
				}
			}
		}

		foreach (var id in wanted_ids) {
			//Log.Info("spawn "+id);
			AddTile(id);
		}
	}

	private void AddTile(int id) {
		var child = new GameObject();
		child.Name = "Tile";
		child.Parent = this.GameObject;
		var tile = child.Components.GetOrCreate<MapTile>();
		tile.TileNumber = id;
		tile.ParentRegion = this;
	}
}
