
--[[
    This script is a simple example of modifying the strength
    of every explosion. Each time this is run, each explosions
    strength is multiplied by Multiplier.
  ]]

local Multiplier = 2.0

for i = 0, rfg.ExplosionInfos:Length(), 1 do
    IndexExp = rfg.ExplosionInfos[i];
    IndexExp.Radius = IndexExp.Radius * Multiplier
    IndexExp.SecondaryRadius = IndexExp.SecondaryRadius * Multiplier
    IndexExp.ImpulseMagnitude = IndexExp.ImpulseMagnitude * Multiplier
end


