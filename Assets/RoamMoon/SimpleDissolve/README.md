# Simple Dissolve
Six dissolve effects for 2D sprites in Unity URP. One component, no material to set up.

Add the component to a GameObject with a SpriteRenderer, pick a mode and drag Amount from 0 to 1. The sprite dissolves. The component builds its own small material and keeps each sprite's settings in a property block.
Both shaders are Shader Graph assets built from plain nodes, one for Sprite Unlit and one for Sprite Lit. The nodes sit in labelled groups, so they are easy to read and change. All the noise is procedural, there are no textures.

## Requirements
- Unity 6000.0 or newer with the Universal Render Pipeline and Shader Graph.
- A 2D Renderer in your URP asset and at least one Light 2D, if you want Lit 2D. Unlit works with any renderer.
- SpriteRenderer only. Built-in and HDRP are not supported.

## Quick start
1. Select a GameObject that has a SpriteRenderer.
2. Add Component > Roam Moon > Simple Dissolve.
3. Pick a Mode. Changing it loads that mode's preset: edge color, width, softness and pattern size. Change anything you like afterwards.
4. Drag Amount, or press Dissolve in the Inspector.

## Demo scenes

| Item | What it is |
|---|---|
| `SimpleDissolveDemo.unity` | All six modes at once, unlit. |
| `SimpleDissolveDemoLit.unity` | The same six under Light 2D, with a dim global light and two point lights. |
| `Prefabs` | Two ready ducks with the Burn mode. One uses the component, the other uses a material from the Materials folder. |

The demo scenes use the pack's own URP asset and 2D Renderer (`Settings/Simple Dissolve Pipeline`). Opening a demo scene switches the project to it, and opening any other scene switches back. You can also do it by hand from Tools > Roam Moon > Simple Dissolve.

## The modes

| Mode | What it looks like |
|---|---|
| Noise | The classic organic dissolve. Soft irregular patches eat into the sprite from every side. |
| Burn | The same patches, but the edge runs white hot to ember to soot. Paper catching fire. |
| Directional | A clean sweep along the Direction vector, with a noisy edge so it does not look like a wipe. |
| Radial | Expands out from Center and clears the sprite in a circle. Good for teleports and spawns. |
| Pixel | Square blocks pop out in random order. Retro and hard edged. |
| Cells | Irregular cells break apart one by one, like shattering glass. |

Each mode has its own edge color, so you get six visibly different effects and not six shades of one.

## Settings

| Setting | What it does |
|---|---|
| Mode, Lighting | Which of the six effects, and Unlit or Lit 2D. |
| Amount | 0 is untouched, 1 is gone. This is the value you animate. |
| Softness | How blurry the cut is. 0 is a razor edge, high values fade like fog. |
| Noise Scale | Pattern size. Lower means bigger shapes. |
| Direction | Sweep direction for Directional. Other modes ignore it. |
| Center, Radius | Where Radial starts, in 0 to 1 sprite space, and how far it reaches. |
| Edge Width, Edge Color, Edge Intensity | The glowing band behind the cut. The color is HDR, values above 1 bloom. |
| Play On Enable, Play Speed, Ping Pong, Loop | Animation. Dissolve, Restore and Rewind buttons work outside Play mode. |
| Base Color | Tints the sprite. |

Settings the current mode does not read are greyed out in the Inspector.

## Unlit and Lit 2D
- Unlit draws the sprite at full brightness and ignores 2D lights. It is the default, and the right choice for UI or a scene with no Light 2D.
- Lit 2D goes through URP's 2D lighting like Sprite-Lit-Default. Light 2D objects tint and dim the sprite, and the edge still glows on top.
- Lit 2D needs at least one Light 2D in the scene. With none, the sprite is black. Add a Global Light 2D, or open the lit demo to see a working setup.

## Good to know

**Noise Scale.** It is how many times the pattern repeats across the sprite, so lower values give bigger shapes. On a sprite a few hundred pixels tall, a high value turns into static. The presets use 1.3 to 2.0. Pixel is the exception: there Noise Scale sets the block count, four blocks per unit, so 2.0 gives 8 blocks across.

**Bloom.** Edge Color is HDR and the demo uses a Bloom threshold of 0.9. An Edge Intensity of 2 to 2.5 reads as a hot cut. Above 4 the edge smears into a blob, so lower Intensity first. Without Bloom everything still works, it just does not glow.

**Timeline.** Every setting is a serialized field, so you can key Amount on a Timeline Animation track or in an Animator clip. The component pushes changes to the GPU once per frame.

## Scripting
```csharp
var fx = GetComponent<SimpleDissolveEffect>();
fx.Mode = SimpleDissolveMode.Burn;
fx.ApplyModePreset();
fx.PlaySpeed = 1.5f;
fx.Play();

while (fx.IsPlaying) yield return null;
Destroy(gameObject);
```
`Play()` sweeps Amount up to 1, `PlayReverse()` back to 0, `Stop()` freezes it and `Rewind()` snaps back to 0. `IsPlaying` is true while a sweep runs. `ApplyModePreset()` loads the current mode's values, and `Lighting` switches between Unlit and Lit 2D.
Every setting is a property with the same name as the Inspector field, and it applies right away, so it is safe to change per frame.

## Performance
- The mode is a node branch, not a keyword, so there are no shader variants. Each mode evaluates a few noise nodes per pixel.
- Each sprite has its own small material, and settings go through a MaterialPropertyBlock, so these sprites are not SRP batched.
- Nothing allocates per frame, and an idle component has no Update cost.

## Problems

| You see | Try |
|---|---|
| Shader not found | URP or Shader Graph is missing. Install both from the Package Manager. |
| The sprite disappears when I add the component | Amount is not 0. Press Rewind. |
| The effect looks like grey static | Noise Scale is too high for the sprite size. Lower it. |
| The edge does not glow | There is no Bloom. Add a Global Volume with a Bloom override. |
| The edge is a white blob | Edge Intensity is too high for your Bloom. Go toward 2. |
| Direction or Center does nothing | The current mode does not read them. |
| The sprite is black in Lit 2D | There is no Light 2D in the scene. Add a Global one, or switch to Unlit. |
| My original material came back | That is intended. The component restores the material the renderer had before. |

## Folder
Shaders (the two Shader Graphs), Scripts (the component and the presets), Editor (the Inspector and the pipeline switch for the demos), Settings (URP asset and 2D Renderer for the demos), Materials and Prefabs (the demo ducks), Demo Scene and Sprites.

## More
Advanced Dissolve is the larger package from the same author: 36 modes including Fire, Ice, Electric, Hologram, Glitch, Portal and Melting, a second edge color, edge shaping, distortion and world space sweeps.

## Support
Roam Moon. When you write, include your Unity and URP version, the mode you use and a screenshot if you can.
