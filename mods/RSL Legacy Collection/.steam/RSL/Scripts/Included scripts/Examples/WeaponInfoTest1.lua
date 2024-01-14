
-- Example script that shows how to log info about all weapon_infos
for i=0, rfg.WeaponInfos:Length(), 1 do
    local Info = rfg.WeaponInfos[i]
    if(Info ~= nil) then
        rsl.Log("{}, Name: {}\n", i, Info.Name)
        rsl.Log("FireConeDot: {}\n", Info.FireConeDot)
        rsl.Log("MaxSpread: {}\n", Info.MaxSpread)
        rsl.Log("MinSpread: {}\n", Info.MinSpread)
        rsl.Log("ShotsPerRound: {}\n\n", Info.ShotsPerRound)
    end
end

