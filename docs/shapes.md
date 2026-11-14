# Shapes and mountains

Two tools in the rail's **Shape** section put a whole shape into the ground with one click. Each click is one step in
History (`Ctrl+Z` takes it back), and the [Mask](masks.md) limits both, as it does the brushes.

## Shape

A mound, a cone, a mesa, a crater, a moat, a bowl or a ridged hill, or any shape you write as a formula.

1. Pick a shape in **Shape**, and set its **Radius** and **Height** (how high it rises, or how deep a crater, moat or
   bowl digs).
2. The shape shows under the pointer, a see-through surface over the ground, with a ring at its radius.
3. Click the ground where its middle goes.

**Formula** shows the chosen shape's formula; change it, or write your own. It gives the metres to add to the ground at
each point within the radius (a negative number digs). A mistake shows under the box, and nothing is put in until it
is fixed.

| Name | Meaning |
|---|---|
| `x`, `z` | Metres east and north of the click. |
| `d`, `r`, `h` | Distance from the click, the radius, the height. |
| `n(x, z)` | Smooth noise from about -1 to 1, the brushes' pattern. `n(x / 10, z / 10)` makes wide bumps. |
| `pi` | 3.14159… |

Functions: `abs`, `atan2(y, x)`, `bell(t)` (1 at 0, fading out by about ±1), `ceil`, `clamp(v, a, b)`, `cos`, `exp`,
`floor`, `log`, `max(…)` and `min(…)`, `pow(a, b)`, `round`, `sign`, `sin`, `smooth(t)` (0 below 0, 1 above 1, a smooth
S between), `sqrt`, `tan`. Operators: `+ - * / %`, `^` (power), comparisons `< > <= >= == !=` (1 or 0), `&&`, `||`,
`!`, and `a ? b : c`.

| Shape | Formula |
|---|---|
| **Mound** | `h * smooth(1 - d / r)` |
| **Cone** | `h * max(0, 1 - d / r)` |
| **Mesa** | `h * smooth((r - d) / (0.35 * r))` |
| **Moat** | `-h * bell((d - 0.75 * r) / (0.12 * r))`: a ditch at three quarters of the radius. |
| **Bowl** | `-h * smooth(1 - (d / r) ^ 2)` |

Two more to start from: `h * smooth(1 - d / r) * (1 + 0.3 * n(x / 8, z / 8))` is a bumpy hill, and
`abs(x) < 3 ? -h : 0` digs a trench 6 m wide across the circle.

## Mountain

A whole mountain with natural detail: its outline warped so it is never a circle, ridges and gullies down its slopes.

| Kind | What it makes |
|---|---|
| **Lone peak** | One mountain rising to a peak. |
| **Ridge** | A long crest, along **Turn**. |
| **Mountain range** | Several peaks along a line, along **Turn**, joined by saddles. |
| **Mesa** | A flat top with steep sides (detail only on its sides). |
| **Volcano** | A cone with a crater at the top. |
| **Rolling hills** | A few gentle hills together. |

Picking a kind, or **New mountain**, rolls one: its **Height**, **Radius**, **Rough** (how much detail, 0 to 1) and
**Turn** picked within the kind's usual sizes, from a new seed. Change any of them; the preview under the pointer
follows. Each click puts one in, and the next comes out different (a new seed, the same sizes), so clicking along a
line makes a range of its own.
