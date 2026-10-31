# ReShade Bridge (mod for MFG SUPER MOTION)

ReShade runs **inside the frame-generation engine**, so its effects and its overlay apply to every frame you see, the real
ones and the generated ones. The mod also makes the overlay usable: **Home** over the game opens the ReShade overlay,
**Home** again gives the keyboard and mouse back to the game.

## Build

Needs the mod kit's `sdk\MfgSuperMotion.ModSdk.dll` two folders up (this folder is `ModKit\examples\ReShadeBridge`).

    dotnet build ModKit\examples\ReShadeBridge -c Release

The zip is `ModKit\examples\ReShadeBridge\bin\Release\ReShadeBridge.zip`. On GitHub, put this tree in a repo and the workflow
`.github\workflows\build-reshade-mod.yml` builds it and keeps the zip as an artifact.

## Use

1. App Mods -> **Add mod** -> pick `ReShadeBridge.zip`. A **ReShade** page appears under More (and a small card on Super Motion).
2. Press START MOTION once so the mod can see the engine, then on the ReShade page press **Detect** (it fills in the engine folder).
3. **ReShade DLL**: pick `ReShade64.dll` (or the `dxgi.dll` of a ReShade you already use). **ReShade folder** (optional): the folder
   with `reshade-shaders`, your `ReShadePreset.ini` and `ReShade.ini`; the mod points ReShade at it, so presets you save in the overlay
   land there.
4. **Install in engine**, then **stop and start motion** (ReShade loads when the engine starts).
5. In the game, press **Home**. The status card says whether ReShade is loaded inside the engine.

## How it works

* **Install** copies the ReShade dll into the engine's folder as `dxgi.dll` (or `d3d11.dll` / `d3d12.dll`), the normal way ReShade is
  installed for a program, and writes a `ReShade.ini` there (shader/texture/preset paths of your folder, and the overlay key). It never
  overwrites a dll in that folder that is not ReShade, and Remove deletes only ReShade dlls.
* The engine's output window lets clicks and keys through to the game, so ReShade (inside the engine) never sees the overlay key. The
  bridge watches the key from outside. Pressed over the game: it makes the engine window take the keyboard and mouse, brings it to the
  front and gives ReShade the key, so the overlay opens. Pressed again: ReShade closes, the window goes back to letting everything through,
  and the game gets the focus back. If you switch away on your own, it lets go after about a second.
* It only opens when ReShade is really loaded in the engine (it checks), so it can never take the keyboard from the game for nothing.
* Change the overlay key here, not inside ReShade (the mod writes it to `ReShade.ini` on Install).

## If it does not work

* Status says **ReShade not loaded**: Install, then stop and start motion. Still not: press **Show ReShade log** (it reads `ReShade.log`
  from the engine folder). If the engine does not use `dxgi.dll`, try loading it as `d3d11.dll` or `d3d12.dll`.
* "No permission to write": the engine folder is somewhere protected; run the app as administrator once, or use a folder you can write to.
* "The file is in use": stop motion first, then Install / Remove.
* The overlay opens but nothing reacts to the mouse: tell me what the log shows; the window style the engine uses decides this.
* Everything the mod does is in `%LOCALAPPDATA%\MFG SUPER MOTION\Mods\mods.log`.

Nothing is sent anywhere. The mod reads only the files you point it at, and writes only the dll and `ReShade.ini` in the engine folder
and its own settings in its data folder. **Get ReShade** just opens reshade.me in your browser.
