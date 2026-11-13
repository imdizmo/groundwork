# Tunnels and caves

**Tunnels** on the rail digs tunnels and caves under the map's ground, kept in its caves file (`caves.json`).

The ground is a height map, so it cannot overhang; a tunnel is a tube of rock the editor draws under it, with the
ground left undrawn where the tube breaks the surface: its mouths. A tunnel is a line of points, each with a depth (how
far its roof lies under the ground there) and a radius. Its first point is always a mouth, with the floor at the
ground; so is its last, unless **Far end open** is off, which closes the far end into a cave. The tube follows a
smooth curve through the points, the floor flat, the roof arched, the walls bumpy; it is lit as the ground is and
clicks land on its walls, so you can walk the camera in and keep adding points from inside.

Pick a tunnel (or **New tunnel**), then click the ground to add a point, drag a point to move it, `Shift`+click a
point to delete it. A click near the line adds the point into it; far from it, at the nearer end. The sliders set the
picked point's **Depth** and **Radius**; a bigger radius at one point makes a chamber. Every change is a step in
History.

**Cut the mouths** paints the mouths into the ground's hole channel, as the [Hole brush](terrain.md#sculpt-brushes)
does, so that Unreal's landscape opens there (its material needs the visibility mask node, see [Unreal](unreal.md)).
It is one step in History; **Fill holes back** of the Hole brush takes it away again. Cut again after moving a
tunnel: the old mouths stay as holes until filled back.

The tube itself goes to Unreal as a model of the map's own, written at Save into its models folder as a kind of the
Tunnels category and placed as an object, so **Bring the models** carries it like any other.
