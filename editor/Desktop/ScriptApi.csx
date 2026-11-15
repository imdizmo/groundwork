// The editor's API for scripts (⋯ → Script…), compiled together with each script. Everything is in metres on the
// map: x east, z north (the 3D view's; Unreal's Y is -z), heights above the map's zero. The changes are kept until the
// script ends, then go into the map as one step in History.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

// Filled by the editor before the script runs.
public static class __Host
{
	public static Func<float, float, float> Height = null!, Original = null!;
	public static Action<float, float, float> Set = null!;
	public static Action<float, float, string, float> Paint = null!;
	public static Action<float, float, string, float, float, float, float, int> Mountain = null!;
	public static Func<(int Id, string Kind, float X, float Y, float Z, string Group, float Pitch, float Yaw, float Roll, float Scale)[]> Objects = null!;
	public static Action<int> Remove = null!;
	public static Action<string, float, float, float, float, float, float, float> Place = null!;
	public static Action<int, float, float, float, float, float, float, float> Move = null!;
	public static Func<string[]> Kinds = null!, Layers = null!;
	public static Func<float, float, int, float> Noise = null!;
	public static Action<string> Print = null!;
	public static Action Check = null!;
	public static Func<float, float, float, float, (int From, int To)> Offsets = null!;
	public static float MinX, MinZ, MaxX, MaxZ, Sea;
	public static string Name = "";
}

// The map: its corners (the ground a script may change), its middle and its sea level.
public static class Map
{
	public static float MinX => __Host.MinX;
	public static float MinZ => __Host.MinZ;
	public static float MaxX => __Host.MaxX;
	public static float MaxZ => __Host.MaxZ;
	public static float CenterX => (__Host.MinX + __Host.MaxX) / 2;
	public static float CenterZ => (__Host.MinZ + __Host.MaxZ) / 2;
	public static float Sea => __Host.Sea;
	public static string Name => __Host.Name;

	// Every ground point of the map (one per metre, or every step metres), as (x, z), for loops over the whole map.
	public static IEnumerable<(float X, float Z)> Points(float step = 1)
	{
		if (!float.IsFinite(step) || step < 0.5f)
		{
			throw new ArgumentException($"a step of {step}: give 0.5 or more (1: every metre, 4: every 4 metres)");
		}
		// Counted rows and points: a float added up can stop growing.
		for (int j = 0; MinZ + j * step <= MaxZ; j++)
		{
			float z = MinZ + j * step;
			for (int i = 0; MinX + i * step <= MaxX; i++)
			{
				__Host.Check();
				yield return (MinX + i * step, z);
			}
		}
	}

	public static bool Inside(float x, float z) => x >= MinX && z >= MinZ && x <= MaxX && z <= MaxZ;
}

// The ground: one point a metre.
public static class Ground
{
	// The ground's height now (with the script's changes so far), between points as the ground is drawn.
	public static float Height(float x, float z) => __Host.Height(x, z);

	// The ground as the map's heightmap has it, before any edit.
	public static float Original(float x, float z) => __Host.Original(x, z);

	public static void Set(float x, float z, float height) => __Host.Set(x, z, height);

	public static void Raise(float x, float z, float metres) => __Host.Set(x, z, __Host.Height(x, z) + metres);

	public static void Lower(float x, float z, float metres) => Raise(x, z, -metres);

	// The map's ground layers, by name ("Grass", "Sand"...: the Paint tool's).
	public static IReadOnlyList<string> Layers => __Host.Layers();

	// Paints a layer at the point (by name, as Layers lists them), strength 0..1.
	public static void Paint(float x, float z, string layer, float strength = 1) => __Host.Paint(x, z, layer, strength);

	// Adds metres(dx, dz) to the ground within radius of (x, z) (dx, dz: metres from the middle).
	public static void Shape(float x, float z, float radius, Func<float, float, float> metres)
	{
		var (fromZ, toZ) = __Host.Offsets(z, radius, Map.MinZ, Map.MaxZ);
		var (fromX, toX) = __Host.Offsets(x, radius, Map.MinX, Map.MaxX);
		for (int dz = fromZ; dz <= toZ; dz++)
		{
			__Host.Check();
			for (int dx = fromX; dx <= toX; dx++)
			{
				if ((float)dx * dx + (float)dz * dz <= radius * radius && Map.Inside(x + dx, z + dz))
				{
					float m = metres(dx, dz);
					if (m != 0)
					{
						Raise(x + dx, z + dz, m);
					}
				}
			}
		}
	}

	// A mountain of the Mountain tool: preset "Lone peak", "Ridge", "Mountain range", "Mesa", "Volcano" or
	// "Rolling hills"; 0 (or less) for a value takes a random one within the preset's (from seed).
	public static void Mountain(float x, float z, string preset = "Lone peak", float height = 0, float radius = 0, float rough = -1, float turn = -1, int seed = 0) =>
		__Host.Mountain(x, z, preset, height, radius, rough, turn, seed);
}

// An object on the map. Kind: its model (the catalog's); Group: the drawer's group (Trees, Rocks...). Yaw, Pitch and
// Roll are its turn and tilts in degrees; Scale its size (1: the model's own).
public sealed record Obj(int Id, string Kind, float X, float Y, float Z, string Group)
{
	public float Yaw { get; init; }
	public float Pitch { get; init; }
	public float Roll { get; init; }
	public float Scale { get; init; } = 1;
}

public static class Objects
{
	private static List<Obj>? _all;

	// Everything on the map (as it was when the script started).
	public static IReadOnlyList<Obj> All => _all ??= __Host.Objects()
		.Select(o => new Obj(o.Id, o.Kind, o.X, o.Y, o.Z, o.Group) { Yaw = o.Yaw, Pitch = o.Pitch, Roll = o.Roll, Scale = o.Scale }).ToList();

	// The kinds a script can place: the catalog's models (the drawer's).
	public static IReadOnlyList<string> Kinds => __Host.Kinds();

	// Groups: Trees, Rocks, Bushes, Plants, Props, Buildings, Markers, Other (the drawer's).
	public static IEnumerable<Obj> OfGroup(string group) => All.Where(o => string.Equals(o.Group, group, StringComparison.OrdinalIgnoreCase));

	public static IEnumerable<Obj> OfKind(string kind) => All.Where(o => string.Equals(o.Kind, kind, StringComparison.OrdinalIgnoreCase));

	public static IEnumerable<Obj> Near(float x, float z, float radius) => All.Where(o => (o.X - x) * (o.X - x) + (o.Z - z) * (o.Z - z) <= radius * radius);

	public static void Remove(Obj o) => __Host.Remove(o.Id);

	// A new object of a kind (its model or label, as the drawer shows them). y: its height (by default on the ground
	// there, as the ground is when the script ends). yaw: its turn in degrees; pitch, roll: its tilts; scale: its size
	// (1: the model's own). A point off the map, a value that is not a number or a size not above 0 stops the script.
	public static void Place(string kind, float x, float z, float yaw = 0, float scale = 1, float y = float.NaN, float pitch = 0, float roll = 0) =>
		__Host.Place(kind, x, z, yaw, scale, y, pitch, roll);

	// Moves, turns or sizes an object that is there; what is left out (NaN) stays as it is. It keeps its data.
	public static void Move(Obj o, float x = float.NaN, float z = float.NaN, float y = float.NaN, float yaw = float.NaN, float pitch = float.NaN, float roll = float.NaN, float scale = float.NaN) =>
		__Host.Move(o.Id, x, z, y, yaw, pitch, roll, scale);
}

public static class Noise
{
	// Smooth natural noise, about -1..1; scale is the size of its bumps in metres.
	public static float At(float x, float z, float scale = 32, int seed = 0) => __Host.Noise(x / scale, z / scale, seed);
}

// Random numbers from a seed (Rnd.Seed = 42 for the same results each run).
public static class Rnd
{
	private static Random _r = new();

	public static int Seed { set => _r = new Random(value); }

	public static float Next() => (float)_r.NextDouble();

	public static float Range(float min, float max) => min + (float)_r.NextDouble() * (max - min);

	public static int Int(int min, int maxExclusive) => _r.Next(min, maxExclusive);

	public static bool Chance(float p) => _r.NextDouble() < p;

	public static T Pick<T>(params T[] items) => items[_r.Next(items.Length)];
}

public static class Globals
{
	// A line in the script window's output.
	public static void Print(object? o) => __Host.Print(o?.ToString() ?? "null");
}
