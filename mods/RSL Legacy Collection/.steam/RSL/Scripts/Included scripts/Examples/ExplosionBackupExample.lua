
-- This is a script that shows how you could backup an explosions values
-- Edit them, and then later restore them.

-- Script starts
-- Backup default values
local DefaultExplosionValues = {} --Make any empty table to store the values
for i = 0, rfg.ExplosionInfos:Size(), 1 do
    local IndexExp = rfg.ExplosionInfos[i]
    DefaultExplosionValues[i] = {}
    DefaultExplosionValues[i].Name = IndexExp.Name
    DefaultExplosionValues[i].Radius = IndexExp.Radius
    DefaultExplosionValues[i].SecondaryRadius = IndexExp.SecondaryRadius
    DefaultExplosionValues[i].ImpulseMagnitude = IndexExp.ImpulseMagnitude
end

-- Do whatever the script needs to do 

-- Now restore the defaults
for i = 0, rfg.ExplosionInfos:Size(), 1 do
    local IndexExp = rfg.ExplosionInfos[i]
    local DefaultValue = DefaultExplosionValues[i]
    
    IndexExp.Radius = DefaultValue.Radius
    IndexExp.SecondaryRadius = DefaultValue.SecondaryRadius
    IndexExp.ImpulseMagnitude = DefaultValue.ImpulseMagnitude
end

for i = 0, rfg.ExplosionInfos:Size(), 1 do
    local DefaultValue = DefaultExplosionValues[i]
    rsl.Log("Index: {}, Name: {}\n", i, DefaultValue.Name)
    rsl.Log("\tRadius: {}\n", tostring(DefaultValue.Radius)) -- Need to fix this needing tostring
    rsl.Log("\tSecondaryRadius: {}\n", tostring(DefaultValue.SecondaryRadius))
    rsl.Log("\tImpulseMagnitude: {}\n\n", DefaultValue.ImpulseMagnitude)
end