local function EveryoneHostile()
    Team = rfg.HumanTeams.EDF
    for i=0, World.Objects:Size(), 1 do
        Object = World.Objects[i]
        
        if Object.Type == rfg.ObjectTypes.Human then
            if Object.AllIndex ~= Player.AllIndex then
                Human = Object:CastToHuman()
                Human.CurrentTeam = Team
            end
        end
    end
    rfg.AddUiMessage ("TEAMS: Everyone on Mars hates you perpetually!", 3, true)
end

local function OnLoad()
    EveryoneHostile()
end
local function Initialized()
    EveryoneHostile()
end
local function FrameUpdate()
    EveryoneHostile()
end
rfg.RegisterEvent("Initialized", Initialized, "[EveryoneHostile]")
rfg.RegisterEvent("Load", OnLoad, "[EveryoneHostile]")
rfg.RegisterEvent("FrameUpdate", FrameUpdate, "[EveryoneHostile]")




