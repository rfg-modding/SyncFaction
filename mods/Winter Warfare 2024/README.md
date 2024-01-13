# Winter Warfare 2024

![screenshot.png]

Experimental winter-themed mod with texture replacements and weapon tweaks

## Textures and models

* Grenade = snowball
* Remote charge = gift box
* Singularity bomb = gift bag
* Thermobaric rocket = winter tree and fireworks

Textures were replaced with new SF feature: support for .cpeg_pc/.gpeg_pc files. At the time of mod release SF did not support texture mods directly, so mod contains prepared PEG files. Not included here because of their size.

## Weapons

### Snowballs

Hand grenade was replaced with snowball

* 3D model of a small round-ish object, RPG clip was OK
* Behavior changed to resemble a snowball: damage on direct hit, several hits to kill, minor structure damage. High impulse for funny ragdolls
* Explosion that resembles ice splash
* Replaced a decal which is not used in MP with snow and added as a bullet hole for this weapon

### Thermobaric rockets

Changed behavior to fly up with negative gravity and explosion effect to resemble fireworks. Tried to make rocket fall down after some time but it despawns on timer, no matter what i try. Maybe long-living rockets cause game crash in MP.