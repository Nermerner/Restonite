# Restonite

A [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader)
mod for [Resonite](https://resonite.com/) that allows easy installation of the
Statufication Remaster system for your avatar. It will appear as an editor you
can create with a dev tool.

To setup your avatar follow the procedure below:

1. Equip a Dev Tool and from the context menu select Create -> Editors -> Statue
   System Wizard (Mod)
2. Drag the avatar root slot to the wizard.
3. Drag a material of your choice to the wizard. This will be used as default
   when you statuefy. You can optionally choose whether to use the material
   as-is or to allow the installer to merge the material with your avatar's
   materials.
4. Select what type of transition you'd like for your avatar. Note that Alpha
   Cutout requires special alpha textures to be created for your avatar to work.
5. Check that the correct MeshRenderers are found for your avatar on the right
   side.
6. Click the Install button and the mod will setup your avatar.

If everything worked you'll see a green success message in the log below. If
not, an error message will be visible which tells you what went wrong.

## Installation

1. Install [ResoniteModLoader](https://github.com/resonite-modding-group/ResoniteModLoader).
2. Place
   [Restonite.dll](https://github.com/Nermerner/Restonite/releases/latest/download/Restonite.dll)
   into your rml_mods folder. This folder should be at C:\Program Files
   (x86)\Steam\steamapps\common\Resonite\rml_mods for a default install.
3. Start the game. If you want to verify that the mod is working you can check
   your Resonite logs.

## Contributing and Building from source

See [CONTRIBUTING.md](./CONTRIBUTING.md).
