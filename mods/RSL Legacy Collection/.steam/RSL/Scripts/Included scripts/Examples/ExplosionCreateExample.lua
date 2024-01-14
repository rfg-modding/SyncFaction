
local ExpInfo = rfg.GetExplosionInfo("remote_charge")

local Position = rfg.Vector:new(Player.Position)
Position.y = Position.y + 10
Position.x = Position.x + 20
rfg.ExplosionCreate(ExpInfo, nil, nil, Position, Player.Orientation, nil)


