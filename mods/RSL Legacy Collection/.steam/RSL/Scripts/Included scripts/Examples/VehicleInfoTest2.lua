
-- Example script that shows how to log info about all VehicleInfos
for i=0, rfg.VehicleInfos:Length(), 1 do
    local Info = rfg.VehicleInfos[i]
    if(Info ~= nil) then
        if(Info.Name == "EDF_StaffCar_NTurret2") then
            rsl.Log("Found EDF_StaffCar_NTurret2! Editing...\n")
            Info.EngineTorque = 3000000
            Info.EnforcedMaxSpeed = 200
        end
    end
end
