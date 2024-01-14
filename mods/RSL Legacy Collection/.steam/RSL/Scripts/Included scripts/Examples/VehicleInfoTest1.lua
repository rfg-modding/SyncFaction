
-- Example script that shows how to log info about all VehicleInfos
for i=0, rfg.VehicleInfos:Length(), 1 do
    local Info = rfg.VehicleInfos[i]
    if(Info ~= nil) then
        rsl.Log("\n{}, Name: {}\n", i, Info.Name)
        rsl.Log("DisplayName: {}\n", Info.DisplayName)
        rsl.Log("SpawnSize: {}\n", Info.SpawnSize)
        rsl.Log("EnforcedMaxSpeed: {}\n", Info.EnforcedMaxSpeed)
        rsl.Log("EngineTorque: {}\n", Info.EngineTorque)
        rsl.Log("AiEngineTorque: {}\n", Info.AiEngineTorque)
    end
end

