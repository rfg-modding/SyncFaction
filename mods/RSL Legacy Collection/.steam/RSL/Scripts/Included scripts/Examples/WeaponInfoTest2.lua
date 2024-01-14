
-- Finds the assault rifle (named m16) on the WeaponInfos list and edits it

for i=0, rfg.WeaponInfos:Length(), 1 do
    local Info = rfg.WeaponInfos[i]

    if(Info.Name == "m16") then
        rsl.Log("Found M16, editing!\n")
        Info.MagazineSize = 20000
        Info.MagazineStartNum = 20000
        Info.MaxRounds = 200000
        Info.MaxRoundsUpgrade = 200000
        Info.AmmoBoxRestock = 2000000
        Info.TracerFrequency = 255
        Info.ShotsPerRound = 10
        Info.FireConeDot = 0.99
        Info.MaxSpread = 0.021
        Info.MinSpread = 0.0035

        Info.FiringSoundRadius = 1.0
        Info.Flags.SilentBullets = true

        Info.ExplosionInfo = rfg.GetExplosionInfo("remote_charge")
    end
end
