using System;
using System.Runtime.InteropServices;
using Sandbox;

public sealed class ProcMesh : Component
{
	[RequireComponent]
	ModelRenderer render {get;set;}

	public Vector2[] Path;
	public float Bottom = -1000;
	public float Top = 200;
	public int ColorSeed = 0;

	[StructLayout( LayoutKind.Sequential )]
	struct Vertex {
		Vector3 Pos;
		Vector3 Normal;
		Vector3 Tangent;
		Vector2 TexCoord;
		Vector3 Color;

		public Vertex(Vector3 pos, Vector3 normal, Vector3 tangent, Vector2 tc, Vector3 color) {
			Pos = pos;
			Normal = normal;
			Tangent = tangent;
			TexCoord = tc;
			Color = color;
			//Log.Info(">>> "+normal);
		}
	}
	private VertexAttribute[] GetVertexLayout() {
		return new VertexAttribute[] {
			new ( VertexAttributeType.Position, VertexAttributeFormat.Float32, 3 ),
			new ( VertexAttributeType.Normal, VertexAttributeFormat.Float32, 3 ),
			new ( VertexAttributeType.Tangent, VertexAttributeFormat.Float32, 3 ),
			new ( VertexAttributeType.TexCoord, VertexAttributeFormat.Float32, 2 ),
			new ( VertexAttributeType.Color, VertexAttributeFormat.Float32, 3 )
			//new ( VertexAttributeType.TexCoord, VertexAttributeFormat.Float32, 4, 4 )
		};
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

	private Mesh BuildFromPath(Span<Vector2> path, int color_seed) {
		var verts = new List<Vertex>();
		var indices = new List<int>();

		var rng = new Random(color_seed);

		Vector3 side_color = rng.FromArray(HOUSE_COLORS);
		Vector3 roof_color = rng.FromArray(ROOF_COLORS);

		// sides
		for (int i=0;i<path.Length;i++) {
			int index_1 = verts.Count;
			var v1 = path[i];
			var v2 = path[(i + 1) % path.Length];

			var tangent = (v2-v1).Normal;
			var normal = new Vector2(tangent.y,-tangent.x);

			verts.Add(new Vertex(new Vector3(v1,Bottom),normal,tangent,Vector2.Zero,side_color));
			verts.Add(new Vertex(new Vector3(v2,Bottom),normal,tangent,Vector2.Zero,side_color));

			verts.Add(new Vertex(new Vector3(v1,Top),normal,tangent,Vector2.Zero,side_color));
			verts.Add(new Vertex(new Vector3(v2,Top),normal,tangent,Vector2.Zero,side_color));

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
			var top = new Vector3[path.Length];
			for (int i=0;i<path.Length;i++) {
				var v = path[i];
				top[i] = v;
				verts.Add(new Vertex(new Vector3(v,Top),Vector3.Up,Vector3.Left,Vector2.Zero,roof_color));
			}
			var new_indices = Mesh.TriangulatePolygon(top);
			foreach (var index in new_indices) {
				indices.Add(index_1 + index);
			}
		}

		var m = new Mesh();
		//m.PrimitiveType = MeshPrimitiveType.Triangles;
		m.CreateVertexBuffer(verts.Count,GetVertexLayout(),verts);
		m.CreateIndexBuffer(indices.Count,indices);

		return m;
	}

	protected override void OnStart()
	{
		//var path = new[] {new Vector2(100,0),new Vector2(100,100),new Vector2(0,100),new Vector2(0,0)};
		var mesh = BuildFromPath(Path,ColorSeed);

		var model = Model.Builder.AddMesh(mesh).Create();
		render.Model = model;
	}
	protected override void OnUpdate()
	{

	}
}
