using System;
using System.Diagnostics;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Threading;
using Sandbox;

public sealed class ProcMesh : Component
{
	/*[RequireComponent]
	ModelRenderer Render {get;set;}
	[RequireComponent]
	ModelCollider Collider {get;set;}*/

	//public Vector2[] Path;
	//public Vector3[] PathRoad;

	public Vertex[] Vertices;
	public int[] Indices;
	public Material Material;

	public bool DisablePhysics = true;

	[StructLayout( LayoutKind.Sequential )]
	public struct Vertex {
		public Vector3 Pos;
		public Vector3 Normal;
		public Vector3 Tangent;
		public Vector2 TexCoord;
		public Vector3 Color;

		public Vertex(Vector3 pos, Vector3 normal, Vector3 tangent, Vector2 tc, Vector3 color) {
			Pos = pos;
			Normal = normal;
			Tangent = tangent;
			TexCoord = tc;
			Color = color;
		}
	}
	private VertexAttribute[] GetVertexLayout() {
		return [
			new ( VertexAttributeType.Position, VertexAttributeFormat.Float32, 3 ),
			new ( VertexAttributeType.Normal, VertexAttributeFormat.Float32, 3 ),
			new ( VertexAttributeType.Tangent, VertexAttributeFormat.Float32, 3 ),
			new ( VertexAttributeType.TexCoord, VertexAttributeFormat.Float32, 2 ),
			new ( VertexAttributeType.Color, VertexAttributeFormat.Float32, 3 )
		];
	}

	private static Vector3[] HOUSE_COLORS = new Vector3[] {
		// boring colors 3x weight
		Color32.FromRgb(0xf2f2f2).ToColor(),
		Color32.FromRgb(0xa69b8d).ToColor(),
		Color32.FromRgb(0xd6d1c5).ToColor(),
		Color32.FromRgb(0xede6c7).ToColor(),

		Color32.FromRgb(0xf2f2f2).ToColor(),
		Color32.FromRgb(0xa69b8d).ToColor(),
		Color32.FromRgb(0xd6d1c5).ToColor(),
		Color32.FromRgb(0xede6c7).ToColor(),

		Color32.FromRgb(0xf2f2f2).ToColor(),
		Color32.FromRgb(0xa69b8d).ToColor(),
		Color32.FromRgb(0xd6d1c5).ToColor(),
		Color32.FromRgb(0xede6c7).ToColor(),

		// browns
		Color32.FromRgb(0x616161).ToColor(),
		Color32.FromRgb(0x57422e).ToColor(),
		Color32.FromRgb(0xa68250).ToColor(),
		Color32.FromRgb(0xba9561).ToColor(),
		Color32.FromRgb(0xe0c399).ToColor(),

		// pastels
		Color32.FromRgb(0xaeb8eb).ToColor(),
		Color32.FromRgb(0x818dcc).ToColor(),
		Color32.FromRgb(0x99b394).ToColor(),
		Color32.FromRgb(0xbab073).ToColor(),
		Color32.FromRgb(0xb88772).ToColor(),
	};

	private static Vector3[] ROOF_COLORS = new Vector3[] {
		Color32.FromRgb(0xb08254).ToColor(),
		Color32.FromRgb(0x996633).ToColor(),
		Color32.FromRgb(0x754e27).ToColor(),
		Color32.FromRgb(0x57422e).ToColor(),
		Color32.FromRgb(0x8f8fba).ToColor(),
		Color32.FromRgb(0x9595a3).ToColor(),
		Color32.FromRgb(0x6d6d78).ToColor(),
		Color32.FromRgb(0x4d4d54).ToColor(),
		Color32.FromRgb(0xcfcfcf).ToColor(),
		Color32.FromRgb(0x858077).ToColor(),
		Color32.FromRgb(0xcfa65f).ToColor(),
	};

	const bool USE_THREADED_MESH_GEN = false;

	protected override void OnStart()
	{
		//Log.Info("pm");
		/*if (USE_THREADED_MESH_GEN) {
			GameTask.RunInThreadAsync(BuildModel);
		} else {
		}*/
		BuildModel();
	}

	private Mesh BuildMesh() {
		var m = new Mesh();
		m.CreateVertexBuffer(Vertices.Length,GetVertexLayout(),Vertices.AsSpan());
		m.CreateIndexBuffer(Indices.Length,Indices);
		return m;
	}

	//private static Object BuildMutex = new Object();

	private void BuildModel() {
		Mesh mesh = BuildMesh();
		mesh.Material = Material;

		// this can be called from a thread; using the static builder is a BAD idea
		// TODO pool builders?
		var builder = new ModelBuilder().AddMesh(mesh);

		if (!DisablePhysics) {
			var collision_verts = new Vector3[Vertices.Length];
			for (int i=0;i<collision_verts.Length;i++) {
				collision_verts[i] = Vertices[i].Pos;
			}
			builder.AddCollisionMesh(collision_verts,Indices);
		}

		//Log.Info("building"); 

		try {
			//lock (BuildMutex) {
			//Log.Info("enter");
			var model = builder.Create();
			var render = AddComponent<ModelRenderer>();
			render.Model = model;

			//Log.Info("pre-create");
			var collider = AddComponent<ProcCollider>(false);
			//Log.Info("post-create");
			collider.Indices = Indices;
			{
				var collision_verts = new Vector3[Vertices.Length];
				for (int i=0;i<collision_verts.Length;i++) {
					collision_verts[i] = Vertices[i].Pos;
				}
				collider.Vertices = collision_verts;
			}
			var s = Stopwatch.StartNew();
			collider.Enabled = true;
			/*if (GameObject.Name == "Terrain") {
				Log.Info("ee "+Time.Now+" "+s.ElapsedMilliseconds);
			}*/
		} catch (Exception e) {
			Log.Info("fail "+e);
		}
	}

	public void SetTerrain(TerrainVertex[] vertices, int[] indices, Material material, Color color) {
		Vertices = new Vertex[vertices.Length];
		Indices = indices;
		Material = material;

		for (int i=0;i<vertices.Length;i++) {
			var pos = vertices[i].Position;
			var normal = vertices[i].Normal;
			var tangent = normal.Cross(Vector3.Forward);
			Vector2 tc = pos / 100;
			tc.y = -tc.y;
			Vertices[i] = new Vertex(pos,normal,tangent,tc,color);
		}
	}

	public void SetBuilding(BuildingInfo info, int seed, Material material) {
		Material = material;

		var verts = new List<Vertex>();
		var indices = new List<int>();

		var rng = new Random(seed);

		Vector3 side_color = rng.FromArray(HOUSE_COLORS);
		Vector3 roof_color = rng.FromArray(ROOF_COLORS);

		float scale = Region.ScaleDistance(1);

		float top = Region.ScaleDistance(info.Height);
		float bottom = -Region.ScaleElevation(info.GroundHigh - info.GroundLow);

		// sides
		var path = info.Nodes;
		for (int i=0;i<path.Length;i++) {
			int index_1 = verts.Count;
			var v1 = Region.ScalePos(path[i]);
			var v2 = Region.ScalePos(path[(i + 1) % path.Length]);

			var tangent = (v2-v1).Normal;
			var normal = new Vector2(tangent.y,-tangent.x);

			float width = (v2-v1).Length/100;
			float height = (top - bottom)/100;

			verts.Add(new Vertex(new Vector3(v1,bottom),normal,tangent,new Vector2(0,0),side_color));
			verts.Add(new Vertex(new Vector3(v2,bottom),normal,tangent,new Vector2(width,0),side_color));

			verts.Add(new Vertex(new Vector3(v1,top),normal,tangent,new Vector2(0,height),side_color));
			verts.Add(new Vertex(new Vector3(v2,top),normal,tangent,new Vector2(width,height),side_color));

			indices.Add(index_1 + 3);
			indices.Add(index_1 + 0);
			indices.Add(index_1 + 1);

			indices.Add(index_1 + 2);
			indices.Add(index_1 + 0);
			indices.Add(index_1 + 3);
		}
		// top
		{
			int index_1 = verts.Count;
			var top_points = new Vector3[path.Length];
			for (int i=0;i<path.Length;i++) {
				var v = Region.ScalePos(path[i]);
				top_points[i] = v;
				verts.Add(new Vertex(new Vector3(v,top),Vector3.Up,Vector3.Left,v/100,roof_color));
			}
			var new_indices = Mesh.TriangulatePolygon(top_points);
			foreach (var index in new_indices) {
				indices.Add(index_1 + index);
			}
		}

		Vertices = verts.ToArray();
		Indices = indices.ToArray();
		Material = material;
		//DisablePhysics = true;
	}

	public void SetRoad(RoadInfo road, Material mat_road, Material mat_sidewalk) {
		var verts = new List<Vertex>();
		var indices = new List<int>();

		Vector3 road_color = Color.FromRgb(0xFFFFFF);

		float v_coord = 0;
		Vector3 last_pos = default;

		var vertical_offset = Vector3.Up * 12;

		// front end cap
		/*{
			int index_1 = verts.Count;
			var node1 = path[0];
			var tangent = Vector3.Forward;

			verts.Add(new Vertex(Region.ScalePos(node1.Left) - vertical_offset,node1.Normal,tangent,new Vector2(0,0),road_color));
			verts.Add(new Vertex(Region.ScalePos(node1.Left) + vertical_offset,node1.Normal,tangent,new Vector2(0,0.1f),road_color));
			verts.Add(new Vertex(Region.ScalePos(node1.Right) - vertical_offset,node1.Normal,tangent,new Vector2(1,0),road_color));
			verts.Add(new Vertex(Region.ScalePos(node1.Right) + vertical_offset,node1.Normal,tangent,new Vector2(1,0.1f),road_color));

			indices.Add(index_1 + 0);
			indices.Add(index_1 + 2);
			indices.Add(index_1 + 1);

			indices.Add(index_1 + 2);
			indices.Add(index_1 + 3);
			indices.Add(index_1 + 1);
		}*/

		bool is_sidewalk = road.Kind == RoadKind.Sidewalk;

		for (int i=0;i<road.Nodes.Length;i++) {
			int index_1 = verts.Count-2;
			int index_2 = verts.Count;
			var node1 = road.Nodes[i];
			//var node2 = path[i+1];

			var tangent = Vector3.Forward; // not correct

			if (i != 0) {
				float texture_len = is_sidewalk ? 10 : 50;
				v_coord += node1.Left.Distance(last_pos) / texture_len;
			}
			last_pos = node1.Left;

			float lane_w_half = 0.05f * road.LaneCount;
			if (is_sidewalk) {
				lane_w_half = 0.11f;
			}

			float u_min = 0.5f - lane_w_half;
			float u_max = 0.5f + lane_w_half;

			if (road.Kind == RoadKind.OneWay) {
				u_min = 0.5f;
				u_max = 0.5f + lane_w_half * 2;
			}

			verts.Add(new Vertex(Region.ScalePos(node1.Left) + vertical_offset,node1.Normal,tangent,new Vector2(u_min,v_coord),road_color));
			verts.Add(new Vertex(Region.ScalePos(node1.Right) + vertical_offset,node1.Normal,tangent,new Vector2(u_max,v_coord),road_color));

			if (i != 0) {
				indices.Add(index_2 + 1);
				indices.Add(index_1 + 0);
				indices.Add(index_1 + 1);

				indices.Add(index_2 + 0);
				indices.Add(index_1 + 0);
				indices.Add(index_2 + 1);
			}
		}

		Vertices = verts.ToArray();
		Indices = indices.ToArray();
		Material = is_sidewalk ? mat_sidewalk : mat_road;
	}
}
